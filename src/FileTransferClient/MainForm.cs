using System.Diagnostics;
using TransferCore;

namespace FileTransferClient;

/// <summary>
/// 客户端主窗口。
///
/// 这里实现了一个符合 <see cref="ProgressCallback"/> 签名的**进度回调函数**
/// <see cref="OnTransferProgress"/>，把它交给 TransferCore；核心层每传完一段数据
/// 就回调一次，界面据此刷新进度条。
/// </summary>
public partial class MainForm : Form
{
    private readonly TcpFileClient _client = new();

    /// <summary>★ 进度回调函数实例（在构造函数里绑定）。</summary>
    private readonly ProgressCallback _progressCallback;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<string> _logLines = new();

    private CancellationTokenSource? _cancellationSource;
    private volatile bool _cancelRequested;
    private bool _busy;
    private string? _selectedFile;
    private int _sentFiles;
    private long _sentBytes;

    public MainForm()
    {
        InitializeComponent();

        // ============ ★ 核心：准备好"回调函数" ============
        _progressCallback = OnTransferProgress;

        // 客户端自己的日志事件也在后台线程触发，同样需要切回 UI 线程
        _client.Log += message => SafeInvoke(() => AppendLog(message));

        AppendLog("就绪。选择要发送的文件，填好服务器 IP 与端口，点击“连接并发送”。");
    }

    // ==================================================================
    //  ★ 进度回调函数
    // ==================================================================
    /// <summary>
    /// ★ 进度回调函数：由 TransferCore 在**后台线程**上反复调用（大约每 80 ms 一次）。
    /// 返回值 true = 继续发送；false = 立即停止发送（"取消"按钮就是靠这个实现的）。
    /// </summary>
    private bool OnTransferProgress(TransferProgress progress)
    {
        if (IsDisposed || Disposing)
        {
            return false;
        }

        try
        {
            // 回调函数运行在后台线程 —— 刷新控件必须切回 UI 线程
            BeginInvoke(new Action<TransferProgress>(ApplyProgress), progress);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        // 用户在界面上点了"取消"→ 返回 false，核心层会中止本次传输
        return !_cancelRequested;
    }

    /// <summary>在 UI 线程上真正刷新进度条与文本。</summary>
    private void ApplyProgress(TransferProgress progress)
    {
        prgBar.Value = Math.Clamp((int)(progress.Percent * 10), prgBar.Minimum, prgBar.Maximum);
        lblPercent.Text = $"{progress.Percent:0.0}%";
        lblFileName.Text = $"文件名：{progress.FileName}";

        string stage = progress.Stage switch
        {
            TransferStage.Connecting => "连接中",
            TransferStage.Transferring => "传输中",
            TransferStage.Verifying => "等待服务端回执",
            TransferStage.Completed => "已完成",
            TransferStage.Cancelled => "已取消",
            TransferStage.Failed => "失败",
            _ => "-"
        };

        lblBytes.Text = $"已发送：{NetOrder.FormatSize(progress.BytesTransferred)} / {NetOrder.FormatSize(progress.TotalBytes)}（{stage}）";
        lblSpeed.Text = $"速度：{NetOrder.FormatSize((long)progress.BytesPerSecond)}/s";
        lblTime.Text = $"已用：{NetOrder.FormatDuration(progress.Elapsed.TotalSeconds)}    剩余：{NetOrder.FormatDuration(progress.Remaining.TotalSeconds)}";
        lblCallback.Text = $"进度回调函数已被调用 {progress.CallbackIndex} 次（每 {FileSender.CallbackIntervalMs} ms 最多一次）";
    }

    // ==================================================================
    //  按钮事件
    // ==================================================================
    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择要发送的文件（建议选一个大文件，例如几百 MB 的视频/镜像）",
            Filter = "所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetSelectedFile(dialog.FileName);
        }
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (!_busy && e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            SetSelectedFile(files[0]);
        }
    }

    private void SetSelectedFile(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "文件不存在。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _selectedFile = path;
        txtFile.Text = path;
        var info = new FileInfo(path);
        lblFileSize.Text = $"文件大小：{NetOrder.FormatSize(info.Length)}（{info.Length:N0} 字节）";
    }

    private async void BtnSend_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (_selectedFile is null || !File.Exists(_selectedFile))
        {
            MessageBox.Show(this, "请先选择要发送的文件。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string host = txtHost.Text.Trim();
        if (host.Length == 0)
        {
            MessageBox.Show(this, "请填写服务器 IP。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(txtPort.Text.Trim(), out int port) || port < 1 || port > 65535)
        {
            MessageBox.Show(this, "端口必须是 1 ~ 65535 之间的整数。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string file = _selectedFile;
        var info = new FileInfo(file);

        _cancelRequested = false;
        _cancellationSource = new CancellationTokenSource();
        CancellationToken token = _cancellationSource.Token;
        SetBusy(true);

        prgBar.Value = 0;
        lblPercent.Text = "0.0%";
        lblCallback.Text = "进度回调函数已被调用 0 次";
        lblStatus.Text = $"状态：正在向 {host}:{port} 发送";

        AppendLog($"===== 开始发送 {info.Name}（{NetOrder.FormatSize(info.Length)}）=====");

        try
        {
            // 把阻塞式发送放到后台线程，UI 线程继续响应消息（否则进度条根本不会动）
            TransferResult result = await Task.Run(
                () => _client.SendFile(host, port, file, _progressCallback, token));

            if (result.Status == TransferStatus.Success)
            {
                _sentFiles++;
                _sentBytes += result.BytesTransferred;
                lblStatus.Text = "状态：发送成功";
                lblSent.Text = $"本次会话已发送 {_sentFiles} 个文件，共 {NetOrder.FormatSize(_sentBytes)}";
            }
            else if (result.Status == TransferStatus.Cancelled)
            {
                lblStatus.Text = "状态：已取消";
            }
            else
            {
                lblStatus.Text = "状态：发送失败";
            }

            AppendLog(result.ToString());
            AppendLog($"----- {result.Message} -----");
        }
        finally
        {
            SetBusy(false);
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (!_busy)
        {
            return;
        }

        // 只置标志位：回调函数下一次被调用时会返回 false，核心层随即干净地中止传输
        _cancelRequested = true;
        btnCancel.Enabled = false;
        AppendLog("已请求取消，将在当前数据块发送完成后中止…");
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        btnSend.Enabled = !busy;
        btnBrowse.Enabled = !busy;
        btnCancel.Enabled = busy;
        txtHost.Enabled = !busy;
        txtPort.Enabled = !busy;
        txtFile.Enabled = !busy;
    }

    // ==================================================================
    //  日志
    // ==================================================================
    private void AppendLog(string message)
    {
        _logLines.Add($"[{_clock.Elapsed:hh\\:mm\\:ss}] {message}");
        if (_logLines.Count > 500)
        {
            _logLines.RemoveRange(0, _logLines.Count - 500);
        }

        txtLog.Lines = _logLines.ToArray();
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    /// <summary>把动作安全地排到 UI 线程执行（窗口已销毁时忽略）。</summary>
    private void SafeInvoke(Action action)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // 句柄已销毁，忽略
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy &&
            MessageBox.Show(this, "正在发送文件，确定要退出吗？本次传输会被中断。",
                "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        _cancelRequested = true;
        _client.Cancel();   // 兜底：直接中断套接字，别让后台线程挂着
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _client.Dispose();
        base.OnFormClosed(e);
    }
}

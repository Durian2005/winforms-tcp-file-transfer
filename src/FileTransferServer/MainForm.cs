using System.Diagnostics;
using TransferCore;

namespace FileTransferServer;

/// <summary>
/// 服务端主窗口。
///
/// 界面上所有控件都在 UI 线程创建，而文件接收在后台线程执行，
/// 两者之间靠 <see cref="OnTransferProgress"/> 这个**进度回调函数** + BeginInvoke 通信。
/// </summary>
public partial class MainForm : Form
{
    private readonly TcpFileServer _server = new();

    /// <summary>★ 进度回调函数实例（在构造函数里绑定到核心层）。</summary>
    private readonly ProgressCallback _progressCallback;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<string> _logLines = new();
    private int _receivedFiles;
    private long _receivedBytes;

    public MainForm()
    {
        InitializeComponent();

        // ============ ★ 核心：把"回调函数"注册给核心层 ============
        // OnTransferProgress 是一个符合 ProgressCallback 签名的方法，
        // 直接以方法组的形式转成委托交给 TcpFileServer，
        // 之后每收到一段数据，核心层就会回调它一次。
        _progressCallback = OnTransferProgress;
        _server.ProgressCallback = _progressCallback;

        _server.Log += OnServerLog;
        _server.ClientConnected += OnClientConnected;
        _server.TransferCompleted += OnTransferCompleted;
        _server.RunningChanged += OnRunningChanged;

        txtSaveDir.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FileTransferReceived");

        AppendLog("就绪。请设置端口与接收目录，然后点击“开始监听”。");
    }

    // ==================================================================
    //  ★ 进度回调函数
    // ==================================================================
    /// <summary>
    /// ★ 进度回调函数：由 TransferCore 在**后台线程**上反复调用。
    /// 这里只做一件事——把进度"转发"回 UI 线程去刷新界面。
    /// 返回值含义：true = 继续传输，false = 请求取消（停止监听时用）。
    /// </summary>
    private bool OnTransferProgress(TransferProgress progress)
    {
        if (IsDisposed || Disposing)
        {
            return false;   // 窗口都销毁了，别再传了
        }

        try
        {
            // 关键：回调发生在后台线程，绝对不能直接 set 控件属性，
            // 必须用 BeginInvoke 把"刷新动作"排进 UI 线程的消息队列。
            BeginInvoke(new Action<TransferProgress>(ApplyProgress), progress);
        }
        catch (InvalidOperationException)
        {
            return false;   // 窗口句柄已销毁
        }

        return _server.IsRunning;   // 停掉监听后，回调返回 false，接收端主动中止
    }

    /// <summary>在 UI 线程上真正刷新进度条与文本。</summary>
    private void ApplyProgress(TransferProgress progress)
    {
        prgBar.Value = Math.Clamp((int)(progress.Percent * 10), prgBar.Minimum, prgBar.Maximum);
        lblPercent.Text = $"{progress.Percent:0.0}%";
        lblFileName.Text = $"文件名：{progress.FileName}";

        string stage = progress.Stage switch
        {
            TransferStage.Transferring => "传输中",
            TransferStage.Verifying => "校验中",
            TransferStage.Completed => "已完成",
            TransferStage.Cancelled => "已取消",
            TransferStage.Failed => "失败",
            _ => "连接中"
        };

        lblBytes.Text = $"已接收：{NetOrder.FormatSize(progress.BytesTransferred)} / {NetOrder.FormatSize(progress.TotalBytes)}（{stage}）";
        lblSpeed.Text = $"速度：{NetOrder.FormatSize((long)progress.BytesPerSecond)}/s";
        lblTime.Text = $"已用：{NetOrder.FormatDuration(progress.Elapsed.TotalSeconds)}    剩余：{NetOrder.FormatDuration(progress.Remaining.TotalSeconds)}";
        lblCallback.Text = $"进度回调函数已被调用 {progress.CallbackIndex} 次（每 {FileReceiver.CallbackIntervalMs} ms 最多一次）";
    }

    // ==================================================================
    //  核心层事件（也都在后台线程上触发，同样需要 BeginInvoke）
    // ==================================================================
    private void OnServerLog(string message)
        => SafeInvoke(() => AppendLog(message));

    private void OnClientConnected(string remote)
        => SafeInvoke(() => lblPeer.Text = $"客户端：{remote}");

    private void OnTransferCompleted(TransferResult result)
    {
        SafeInvoke(() =>
        {
            if (result.Status == TransferStatus.Success)
            {
                _receivedFiles++;
                _receivedBytes += result.BytesTransferred;
                AppendLog($"★ 接收成功，已保存：{result.SavedPath}");
                AppendLog($"  累计 {_receivedFiles} 个文件，共 {NetOrder.FormatSize(_receivedBytes)}");
                lblPeer.Text = "客户端：传输结束（等待下一个连接）";
            }
            else
            {
                AppendLog($"★ 本次传输结束：{result.Status} —— {result.Message}");
            }
        });
    }

    private void OnRunningChanged(bool running)
    {
        SafeInvoke(() =>
        {
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
            txtPort.Enabled = !running;
            txtSaveDir.Enabled = !running;
            btnBrowse.Enabled = !running;
            lblStatus.Text = running ? $"状态：正在监听 {_server.Port}" : "状态：未监听";
            lblStatus.ForeColor = running
                ? Color.FromArgb(0, 130, 60)
                : Color.FromArgb(180, 60, 0);
        });
    }

    // ==================================================================
    //  按钮事件
    // ==================================================================
    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择接收文件的保存目录",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(txtSaveDir.Text) ? txtSaveDir.Text : string.Empty,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtSaveDir.Text = dialog.SelectedPath;
        }
    }

    private void BtnStart_Click(object? sender, EventArgs e)
    {
        if (!int.TryParse(txtPort.Text.Trim(), out int port) || port < 1 || port > 65535)
        {
            MessageBox.Show(this, "端口必须是 1 ~ 65535 之间的整数。", "参数错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtPort.Focus();
            return;
        }

        string directory = txtSaveDir.Text.Trim();
        if (directory.Length == 0)
        {
            MessageBox.Show(this, "请先选择接收目录。", "参数错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _server.Start(port, directory);
            prgBar.Value = 0;
            lblPercent.Text = "0.0%";
            lblFileName.Text = "文件名：--";
            lblBytes.Text = "已接收：0 B / 0 B";
            lblSpeed.Text = "速度：--";
            lblTime.Text = "已用：--    剩余：--";
            lblCallback.Text = "进度回调函数已被调用 0 次";
            lblPeer.Text = "客户端：尚未连接";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"启动监听失败：{ex.Message}\n\n常见原因：端口已被占用（换一个端口），或权限不足。",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _server.Stop();
        prgBar.Value = 0;
        lblPercent.Text = "0.0%";
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
        if (_server.IsRunning &&
            MessageBox.Show(this,
                "服务器仍在监听中，确定要退出吗？进行中的传输会被中断。",
                "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // 先摘掉回调引用，再停止服务器，避免关闭过程中还有回调进来
        _server.ProgressCallback = null;
        _server.Dispose();
        base.OnFormClosed(e);
    }
}

namespace TransferCore;

/// <summary>传输方向。</summary>
public enum TransferDirection
{
    /// <summary>本机作为发送方。</summary>
    Send,

    /// <summary>本机作为接收方。</summary>
    Receive
}

/// <summary>传输所处阶段。</summary>
public enum TransferStage
{
    /// <summary>正在建立连接（客户端）或等待对端发来报文头（服务端）。</summary>
    Connecting,

    /// <summary>正在传输文件内容。</summary>
    Transferring,

    /// <summary>传输结束、正在校验 / 等待对端应答。</summary>
    Verifying,

    /// <summary>传输完成。</summary>
    Completed,

    /// <summary>传输失败。</summary>
    Failed,

    /// <summary>被用户取消。</summary>
    Cancelled
}

/// <summary>
/// 一次"进度回调"所携带的全部信息（不可变结构体，可安全跨线程传递）。
/// UI 层只需要读这个对象就能刷新进度条、速度、剩余时间。
/// </summary>
public readonly struct TransferProgress
{
    public TransferProgress(
        string fileName,
        TransferDirection direction,
        TransferStage stage,
        long bytesTransferred,
        long totalBytes,
        TimeSpan elapsed,
        int callbackIndex)
    {
        FileName = fileName;
        Direction = direction;
        Stage = stage;
        BytesTransferred = bytesTransferred;
        TotalBytes = totalBytes;
        Elapsed = elapsed;
        CallbackIndex = callbackIndex;
    }

    /// <summary>正在传输的文件名。</summary>
    public string FileName { get; }

    /// <summary>传输方向。</summary>
    public TransferDirection Direction { get; }

    /// <summary>当前阶段。</summary>
    public TransferStage Stage { get; }

    /// <summary>已传输字节数。</summary>
    public long BytesTransferred { get; }

    /// <summary>文件总字节数。</summary>
    public long TotalBytes { get; }

    /// <summary>从开始传输到本次回调所经过的时间。</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>本次传输中第几次回调（从 1 开始），可用于统计"回调被调用了多少次"。</summary>
    public int CallbackIndex { get; }

    /// <summary>进度百分比（0.0 ~ 100.0）。</summary>
    public double Percent
    {
        get
        {
            if (TotalBytes <= 0)
            {
                return 0;
            }

            double percent = BytesTransferred * 100.0 / TotalBytes;
            return percent > 100 ? 100 : percent;
        }
    }

    /// <summary>平均速度（字节/秒）。</summary>
    public double BytesPerSecond
    {
        get
        {
            double seconds = Elapsed.TotalSeconds;
            return seconds <= 0 ? 0 : BytesTransferred / seconds;
        }
    }

    /// <summary>预计剩余时间。</summary>
    public TimeSpan Remaining
    {
        get
        {
            double speed = BytesPerSecond;
            if (speed <= 0 || TotalBytes <= BytesTransferred)
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromSeconds((TotalBytes - BytesTransferred) / speed);
        }
    }
}

/// <summary>
/// ★ 进度回调函数类型（本实验的核心要求之一）。
///
/// 使用方式：由 **UI 层**（Form）实现一个符合该签名的方法并交给核心层，
/// 核心层在传输过程中反复调用它，把最新进度"回推"给界面去刷新进度条。
///
///   private bool OnTransferProgress(TransferProgress p)   // ← 回调函数
///   {
///       // 刷新进度条 ...
///       return true;    // 返回 false 表示请求取消传输
///   }
///
///   sender.SendFile(path, OnTransferProgress, ...);       // ← 注册回调
///
/// 为什么用"回调"而不是让核心层直接操作控件：核心层不认识 WinForms，
/// 它只负责"算进度"，界面怎么画与它无关（解耦）；同时也满足题目
/// "使用回调函数显示进度条" 的要求。
///
/// 注意：回调发生在**工作线程**上，回调实现里更新控件必须切回 UI 线程
/// （WinForms 用 Control.BeginInvoke / Invoke）。
/// </summary>
/// <param name="progress">当前进度信息。</param>
/// <returns>返回 true 继续传输；返回 false 请求取消本次传输。</returns>
public delegate bool ProgressCallback(TransferProgress progress);

namespace TransferCore;

/// <summary>一次传输的最终结果。</summary>
public enum TransferStatus
{
    /// <summary>成功（服务端还会额外报告"校验是否一致"）。</summary>
    Success,

    /// <summary>被调用方通过回调返回 false 取消。</summary>
    Cancelled,

    /// <summary>出错（网络中断、磁盘错误、协议不匹配等）。</summary>
    Failed
}

/// <summary>
/// 传输结果对象：把"结果 + 统计 + 落盘路径 + 校验结论"打包返回给调用方。
/// </summary>
public sealed class TransferResult
{
    public TransferResult(
        TransferStatus status,
        string fileName,
        long fileSize,
        long bytesTransferred,
        TimeSpan elapsed,
        string message,
        string? savedPath = null,
        ulong checksum = 0,
        bool? checksumMatched = null,
        int callbackCount = 0)
    {
        Status = status;
        FileName = fileName;
        FileSize = fileSize;
        BytesTransferred = bytesTransferred;
        Elapsed = elapsed;
        Message = message;
        SavedPath = savedPath;
        Checksum = checksum;
        ChecksumMatched = checksumMatched;
        CallbackCount = callbackCount;
    }

    /// <summary>结果状态。</summary>
    public TransferStatus Status { get; }

    /// <summary>文件名。</summary>
    public string FileName { get; }

    /// <summary>文件总字节数。</summary>
    public long FileSize { get; }

    /// <summary>实际传输字节数。</summary>
    public long BytesTransferred { get; }

    /// <summary>耗时。</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>给用户看的描述文本。</summary>
    public string Message { get; }

    /// <summary>接收端才有：文件保存到的完整路径。</summary>
    public string? SavedPath { get; }

    /// <summary>本机计算出的内容校验值（FNV-1a 64）。</summary>
    public ulong Checksum { get; }

    /// <summary>接收端才有：与发送端校验值是否一致；null 表示未校验。</summary>
    public bool? ChecksumMatched { get; }

    /// <summary>★ 本次传输中"进度回调函数"被调用的次数（用于证明回调确实在持续工作）。</summary>
    public int CallbackCount { get; }

    /// <summary>平均速度（字节/秒）。</summary>
    public double BytesPerSecond => Elapsed.TotalSeconds <= 0 ? 0 : BytesTransferred / Elapsed.TotalSeconds;

    public override string ToString()
    {
        string text = $"{Status} {FileName} ({NetOrder.FormatSize(BytesTransferred)}/{NetOrder.FormatSize(FileSize)}) " +
                      $"耗时 {Elapsed.TotalSeconds:0.00}s 速度 {NetOrder.FormatSize((long)BytesPerSecond)}/s 回调 {CallbackCount} 次";
        if (ChecksumMatched.HasValue)
        {
            text += ChecksumMatched.Value ? " 校验一致" : " 校验不一致";
        }

        return text;
    }
}

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace TransferCore;

/// <summary>
/// 发送端：把一个本地文件通过已连接的套接字发送出去，并在过程中反复调用
/// <see cref="ProgressCallback"/>（进度回调函数）让界面刷新进度条。
/// </summary>
public sealed class FileSender
{
    /// <summary>进度回调的最小间隔（毫秒）。太频繁会拖慢传输速度、也会让界面刷新不过来。</summary>
    public const int CallbackIntervalMs = 80;

    private readonly Socket _socket;

    public FileSender(Socket socket)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        try
        {
            _socket.NoDelay = true;   // 关闭 Nagle 算法：大文件传输下让每个块尽快发出
        }
        catch (SocketException)
        {
            // 某些套接字类型不支持设置该选项，忽略即可
        }
    }

    /// <summary>
    /// 发送一个文件（阻塞方法，请在工作线程上调用）。
    /// </summary>
    /// <param name="filePath">本地文件完整路径。</param>
    /// <param name="callback">★ 进度回调函数；传 null 表示不关心进度。</param>
    /// <param name="cancellationToken">外部取消标记；触发后按"已取消"返回。</param>
    public TransferResult SendFile(string filePath, ProgressCallback? callback, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var stopwatch = Stopwatch.StartNew();
        var hash = new Fnv1a64();
        var buffer = new byte[Protocol.BufferSize];
        string fileName = Path.GetFileName(filePath);
        long totalBytes = 0;
        long transferred = 0;
        int callbackCount = 0;
        long lastCallbackMs = 0;

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                Protocol.BufferSize,
                FileOptions.SequentialScan);   // 顺序扫描提示，大文件读盘更快

            totalBytes = stream.Length;

            // ---------- 第 1 步：把报文头发给服务端（文件名 + 文件长度） ----------
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);
            if (nameBytes.Length == 0 || nameBytes.Length > Protocol.MaxFileNameLength)
            {
                throw new InvalidDataException($"文件名长度非法（{nameBytes.Length} 字节）。");
            }

            byte[] header = new byte[Protocol.HeaderSize + nameBytes.Length];
            Buffer.BlockCopy(Protocol.Magic, 0, header, 0, Protocol.Magic.Length);
            NetOrder.WriteUInt32(header, 4, (uint)nameBytes.Length);
            NetOrder.WriteInt64(header, 8, totalBytes);
            Buffer.BlockCopy(nameBytes, 0, header, Protocol.HeaderSize, nameBytes.Length);
            SocketIo.SendAll(_socket, header, 0, header.Length);

            // ---------- 第 2 步：立即回调一次（0%），让界面立刻显示文件名与总大小 ----------
            callbackCount++;
            if (!Report(callback, fileName, TransferDirection.Send, TransferStage.Transferring,
                        0, totalBytes, stopwatch, callbackCount))
            {
                return Cancelled(fileName, totalBytes, 0, stopwatch, hash.Value, callbackCount);
            }

            // ---------- 第 3 步：循环读文件 → 发送 → 回调 ----------
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Cancelled(fileName, totalBytes, transferred, stopwatch, hash.Value, callbackCount);
                }

                SocketIo.SendAll(_socket, buffer, 0, read);
                hash.Append(buffer, 0, read);
                transferred += read;

                // 限流：至少间隔 CallbackIntervalMs 毫秒，或者已经传完，才回调一次
                long nowMs = stopwatch.ElapsedMilliseconds;
                if (nowMs - lastCallbackMs >= CallbackIntervalMs || transferred >= totalBytes)
                {
                    lastCallbackMs = nowMs;
                    callbackCount++;
                    if (!Report(callback, fileName, TransferDirection.Send, TransferStage.Transferring,
                                transferred, totalBytes, stopwatch, callbackCount))
                    {
                        return Cancelled(fileName, totalBytes, transferred, stopwatch, hash.Value, callbackCount);
                    }
                }
            }

            // ---------- 第 4 步：发送内容校验值（文件尾部追加 8 字节） ----------
            byte[] trailer = new byte[Protocol.ChecksumSize];
            NetOrder.WriteInt64(trailer, 0, unchecked((long)hash.Value));
            SocketIo.SendAll(_socket, trailer, 0, trailer.Length);

            // ---------- 第 5 步：等对方回执（确认收完并报告校验结果） ----------
            callbackCount++;
            Report(callback, fileName, TransferDirection.Send, TransferStage.Verifying,
                   transferred, totalBytes, stopwatch, callbackCount);

            (bool ok, string message) = ReadAcknowledgement(_socket);
            stopwatch.Stop();

            callbackCount++;
            Report(callback, fileName, TransferDirection.Send,
                   ok ? TransferStage.Completed : TransferStage.Failed,
                   transferred, totalBytes, stopwatch, callbackCount);

            return new TransferResult(
                ok ? TransferStatus.Success : TransferStatus.Failed,
                fileName, totalBytes, transferred, stopwatch.Elapsed,
                ok ? $"发送完成：{message}" : $"发送失败：{message}",
                savedPath: null,
                checksum: hash.Value,
                checksumMatched: ok ? true : null,
                callbackCount: callbackCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            callbackCount++;
            Report(callback, fileName, TransferDirection.Send, TransferStage.Failed,
                   transferred, totalBytes, stopwatch, callbackCount);

            return new TransferResult(
                TransferStatus.Failed, fileName, totalBytes, transferred, stopwatch.Elapsed,
                $"发送异常：{ex.Message}", null, hash.Value, null, callbackCount);
        }
    }

    /// <summary>读取服务端应答包：[状态 1 字节][消息长 4 字节][消息 msgLen 字节]。</summary>
    internal static (bool Ok, string Message) ReadAcknowledgement(Socket socket)
    {
        byte[] statusBuf = new byte[1];
        SocketIo.ReceiveExact(socket, statusBuf, 0, 1);

        byte[] lenBuf = new byte[4];
        SocketIo.ReceiveExact(socket, lenBuf, 0, 4);
        uint len = NetOrder.ReadUInt32(lenBuf, 0);
        if (len > 64 * 1024)
        {
            throw new InvalidDataException("应答消息长度非法。");
        }

        string message = string.Empty;
        if (len > 0)
        {
            byte[] msgBuf = new byte[len];
            SocketIo.ReceiveExact(socket, msgBuf, 0, (int)len);
            message = Encoding.UTF8.GetString(msgBuf);
        }

        return (statusBuf[0] == Protocol.StatusOk, message);
    }

    private static bool Report(
        ProgressCallback? callback,
        string fileName,
        TransferDirection direction,
        TransferStage stage,
        long transferred,
        long totalBytes,
        Stopwatch stopwatch,
        int callbackIndex)
    {
        if (callback is null)
        {
            return true;
        }

        var progress = new TransferProgress(
            fileName, direction, stage, transferred, totalBytes, stopwatch.Elapsed, callbackIndex);
        return callback(progress);
    }

    private static TransferResult Cancelled(
        string fileName, long totalBytes, long transferred, Stopwatch stopwatch, ulong checksum, int callbackCount)
    {
        stopwatch.Stop();
        return new TransferResult(
            TransferStatus.Cancelled, fileName, totalBytes, transferred, stopwatch.Elapsed,
            "已被用户取消。", null, checksum, null, callbackCount);
    }
}

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace TransferCore;

/// <summary>
/// 接收端：从已连接的套接字读出一个文件并落盘，过程中同样反复调用
/// <see cref="ProgressCallback"/>（进度回调函数）。
/// </summary>
public sealed class FileReceiver
{
    /// <summary>进度回调的最小间隔（毫秒）。</summary>
    public const int CallbackIntervalMs = 80;

    private readonly Socket _socket;
    private readonly string _saveDirectory;

    public FileReceiver(Socket socket, string saveDirectory)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _saveDirectory = saveDirectory ?? throw new ArgumentNullException(nameof(saveDirectory));

        try
        {
            _socket.NoDelay = true;
        }
        catch (SocketException)
        {
            // 忽略
        }
    }

    /// <summary>
    /// 接收一个文件（阻塞方法，请在工作线程上调用）。
    /// </summary>
    /// <param name="callback">★ 进度回调函数；返回 false 表示请求取消。</param>
    /// <param name="cancellationToken">外部取消标记。</param>
    public TransferResult ReceiveFile(ProgressCallback? callback, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var hash = new Fnv1a64();
        var buffer = new byte[Protocol.BufferSize];
        string fileName = "(未识别)";
        long totalBytes = 0;
        long transferred = 0;
        int callbackCount = 0;
        long lastCallbackMs = 0;
        string? partialPath = null;

        try
        {
            // ---------- 第 1 步：读取报文头 ----------
            byte[] header = new byte[Protocol.HeaderSize];
            SocketIo.ReceiveExact(_socket, header, 0, header.Length);

            if (!Protocol.IsMagic(header, 0))
            {
                throw new InvalidDataException("报文魔数不正确：对方可能不是本程序的客户端（检查端口是否填错）。");
            }

            uint nameLength = NetOrder.ReadUInt32(header, 4);
            if (nameLength == 0 || nameLength > Protocol.MaxFileNameLength)
            {
                throw new InvalidDataException($"文件名长度非法：{nameLength}");
            }

            totalBytes = NetOrder.ReadInt64(header, 8);
            if (totalBytes < 0)
            {
                throw new InvalidDataException($"文件长度非法：{totalBytes}");
            }

            // ---------- 第 2 步：读取文件名 ----------
            byte[] nameBytes = new byte[nameLength];
            SocketIo.ReceiveExact(_socket, nameBytes, 0, (int)nameLength);
            fileName = Encoding.UTF8.GetString(nameBytes);

            // 安全处理：只取文件名部分并过滤非法字符，防止对端发来 "..\..\x" 之类的路径穿越
            fileName = SanitizeFileName(fileName);
            Directory.CreateDirectory(_saveDirectory);
            partialPath = BuildUniquePath(_saveDirectory, fileName);

            // ---------- 第 3 步：立即回调一次（0%），让界面显示文件名与总大小 ----------
            callbackCount++;
            if (!Report(callback, fileName, TransferDirection.Receive, TransferStage.Transferring,
                        0, totalBytes, stopwatch, callbackCount))
            {
                return Cancelled(fileName, totalBytes, transferred, stopwatch, hash.Value, callbackCount, null);
            }

            // ---------- 第 4 步：循环接收 → 写盘 → 回调 ----------
            using (var stream = new FileStream(
                       partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       Protocol.BufferSize, FileOptions.SequentialScan))
            {
                while (transferred < totalBytes)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        stream.Flush();
                        return Cancelled(fileName, totalBytes, transferred, stopwatch, hash.Value, callbackCount, partialPath);
                    }

                    int want = (int)Math.Min(buffer.Length, totalBytes - transferred);
                    int received = SocketIo.ReceiveSome(_socket, buffer, 0, want);
                    if (received <= 0)
                    {
                        throw new IOException("对端提前关闭了连接，文件不完整。");
                    }

                    stream.Write(buffer, 0, received);
                    hash.Append(buffer, 0, received);
                    transferred += received;

                    long nowMs = stopwatch.ElapsedMilliseconds;
                    if (nowMs - lastCallbackMs >= CallbackIntervalMs || transferred >= totalBytes)
                    {
                        lastCallbackMs = nowMs;
                        callbackCount++;
                        if (!Report(callback, fileName, TransferDirection.Receive, TransferStage.Transferring,
                                    transferred, totalBytes, stopwatch, callbackCount))
                        {
                            stream.Flush();
                            return Cancelled(fileName, totalBytes, transferred, stopwatch, hash.Value, callbackCount, partialPath);
                        }
                    }
                }
            }

            // ---------- 第 5 步：读取发送端的内容校验值并比对 ----------
            byte[] trailer = new byte[Protocol.ChecksumSize];
            SocketIo.ReceiveExact(_socket, trailer, 0, trailer.Length);
            ulong remoteChecksum = unchecked((ulong)NetOrder.ReadInt64(trailer, 0));
            bool matched = remoteChecksum == hash.Value;

            callbackCount++;
            Report(callback, fileName, TransferDirection.Receive, TransferStage.Verifying,
                   transferred, totalBytes, stopwatch, callbackCount);

            // ---------- 第 6 步：回送应答 ----------
            string ackMessage = matched
                ? $"已保存为 {Path.GetFileName(partialPath)}，校验一致。"
                : $"已保存为 {Path.GetFileName(partialPath)}，但校验不一致！";
            SendAcknowledgement(_socket, matched, ackMessage);

            stopwatch.Stop();
            callbackCount++;
            Report(callback, fileName, TransferDirection.Receive, TransferStage.Completed,
                   transferred, totalBytes, stopwatch, callbackCount);

            return new TransferResult(
                matched ? TransferStatus.Success : TransferStatus.Failed,
                fileName, totalBytes, transferred, stopwatch.Elapsed,
                ackMessage, partialPath, hash.Value, matched, callbackCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // 传输失败时把"没写完的半截文件"删掉，避免污染接收目录
            TryDelete(partialPath);

            callbackCount++;
            Report(callback, fileName, TransferDirection.Receive, TransferStage.Failed,
                   transferred, totalBytes, stopwatch, callbackCount);

            return new TransferResult(
                TransferStatus.Failed, fileName, totalBytes, transferred, stopwatch.Elapsed,
                $"接收异常：{ex.Message}", null, hash.Value, null, callbackCount);
        }
    }

    /// <summary>给发送端回送应答包：[状态 1 字节][消息长 4 字节][消息 msgLen 字节]。</summary>
    internal static void SendAcknowledgement(Socket socket, bool ok, string message)
    {
        byte[] msgBytes = Encoding.UTF8.GetBytes(message ?? string.Empty);
        byte[] packet = new byte[1 + 4 + msgBytes.Length];
        packet[0] = ok ? Protocol.StatusOk : Protocol.StatusFail;
        NetOrder.WriteUInt32(packet, 1, (uint)msgBytes.Length);
        Buffer.BlockCopy(msgBytes, 0, packet, 5, msgBytes.Length);
        SocketIo.SendAll(socket, packet, 0, packet.Length);
    }

    /// <summary>把对端给的文件名清理成"安全的纯文件名"。</summary>
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "unnamed.bin";
        }

        // 只保留最后一段，去掉目录成分（防止路径穿越）
        string safe = Path.GetFileName(name.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(safe) ? "unnamed.bin" : safe;
    }

    /// <summary>若同名文件已存在，自动加 (1)、(2) 后缀，绝不覆盖已有文件。</summary>
    public static string BuildUniquePath(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int index = 1; ; index++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 删不掉就算了，不要用清理失败去覆盖真正的错误信息
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
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
        string fileName, long totalBytes, long transferred, Stopwatch stopwatch,
        ulong checksum, int callbackCount, string? partialPath)
    {
        stopwatch.Stop();
        TryDelete(partialPath);
        return new TransferResult(
            TransferStatus.Cancelled, fileName, totalBytes, transferred, stopwatch.Elapsed,
            "已被用户取消，未完成的临时文件已删除。", null, checksum, null, callbackCount);
    }
}

using System.Net.Sockets;

namespace TransferCore;

/// <summary>
/// TCP 文件发送客户端：连接服务器 → 发送一个本地文件 → 等待回执。
///
/// <see cref="SendFile"/> 是阻塞方法，界面上请放到后台线程执行，
/// 进度通过传入的回调函数（<see cref="ProgressCallback"/>）回推给界面。
/// </summary>
public sealed class TcpFileClient : IDisposable
{
    private readonly object _sync = new();
    private Socket? _currentSocket;
    private bool _disposed;

    /// <summary>日志事件。</summary>
    public event Action<string>? Log;

    /// <summary>连接超时（毫秒）。</summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 连接服务器并发送一个文件（阻塞）。
    /// </summary>
    /// <param name="host">服务器 IP 或主机名。</param>
    /// <param name="port">服务器端口。</param>
    /// <param name="filePath">本地文件路径。</param>
    /// <param name="callback">★ 进度回调函数（返回 false 请求取消）。</param>
    /// <param name="cancellationToken">外部取消标记。</param>
    public TransferResult SendFile(
        string host,
        int port,
        string filePath,
        ProgressCallback? callback,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fileName = Path.GetFileName(filePath);
        Socket? socket = null;

        try
        {
            // ---------- 第 1 步：建立 TCP 连接（带超时） ----------
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            lock (_sync)
            {
                _currentSocket = socket;
            }

            Log?.Invoke($"正在连接 {host}:{port} ...");
            ConnectWithTimeout(socket, host, port, ConnectTimeoutMs);
            Log?.Invoke($"已连接 {socket.RemoteEndPoint}，开始发送 {fileName}");

            // ---------- 第 2 步：交给 FileSender 完成实际传输 ----------
            var sender = new FileSender(socket);
            TransferResult result = sender.SendFile(filePath, callback, cancellationToken);

            Log?.Invoke($"发送结束：{result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            return new TransferResult(
                TransferStatus.Failed, fileName, 0, 0, TimeSpan.Zero,
                $"发送失败：{ex.Message}", null, 0, null, 0);
        }
        finally
        {
            lock (_sync)
            {
                _currentSocket = null;
            }

            if (socket is not null)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                    // 对端可能已经先关了，忽略
                }
                catch (ObjectDisposedException)
                {
                    // 被 Cancel() 处理过了，忽略
                }

                try
                {
                    socket.Close();
                }
                catch (SocketException)
                {
                    // 忽略
                }
            }
        }
    }

    /// <summary>
    /// 强制中断正在进行的连接/传输（比如用户点了"取消"或关了窗口）。
    /// 使用 LingerState(true, 0) 让系统直接发 RST 而不是走正常四次挥手，
    /// 这样服务端能**立刻**收到错误、而不是傻等到超时。
    /// </summary>
    public void Cancel()
    {
        Socket? socket;
        lock (_sync)
        {
            socket = _currentSocket;
        }

        if (socket is null)
        {
            return;
        }

        try
        {
            socket.LingerState = new LingerOption(true, 0);
            socket.Close();
        }
        catch (SocketException)
        {
            // 忽略
        }
    }

    /// <summary>用 APM（BeginConnect）给 Connect 加上超时，避免对端没开机时界面一直卡着。</summary>
    private static void ConnectWithTimeout(Socket socket, string host, int port, int timeoutMs)
    {
        IAsyncResult asyncResult = socket.BeginConnect(host, port, null, null);
        if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs))
        {
            socket.Close();
            throw new TimeoutException($"连接 {host}:{port} 超时（{timeoutMs} 毫秒）。请确认服务端已开始监听、IP/端口正确、防火墙放行。");
        }

        socket.EndConnect(asyncResult);   // 没连上时这里会抛出真正的错误原因
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }
}

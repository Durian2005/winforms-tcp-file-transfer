using System.Net;
using System.Net.Sockets;

namespace TransferCore;

/// <summary>
/// TCP 文件接收服务器（可复用：能连续接受多个客户端，不必重启）。
///
/// 设计要点：
///   * <see cref="Start"/> 里只做 bind + listen，然后丢一个后台线程去循环 Accept，
///     因此**不会阻塞 UI 线程**；
///   * 每个客户端再单独开一个后台任务处理，处理完立刻回到 Accept，
///     所以"接收大文件的同时还能接受下一个连接"；
///   * 进度通过 <see cref="ProgressCallback"/>（回调函数）对外抛出，
///     由 UI 层负责画进度条。
/// </summary>
public sealed class TcpFileServer : IDisposable
{
    private readonly object _sync = new();
    private readonly List<Socket> _activeClients = new();
    private readonly CancellationTokenSource _stopSource = new();

    private Socket? _listener;
    private Thread? _acceptThread;
    private bool _disposed;

    /// <summary>★ 进度回调函数：每收到一段数据就会被调用一次（在工作线程上执行）。</summary>
    public ProgressCallback? ProgressCallback { get; set; }

    /// <summary>运行状态发生变化（true = 已启动，false = 已停止）。</summary>
    public event Action<bool>? RunningChanged;

    /// <summary>日志事件（在工作线程上触发）。</summary>
    public event Action<string>? Log;

    /// <summary>有客户端接入（参数为 "IP:端口"）。</summary>
    public event Action<string>? ClientConnected;

    /// <summary>一个文件接收结束（成功/失败/取消都会触发）。</summary>
    public event Action<TransferResult>? TransferCompleted;

    /// <summary>当前是否正在监听。</summary>
    public bool IsRunning { get; private set; }

    /// <summary>当前监听端口。</summary>
    public int Port { get; private set; }

    /// <summary>接收文件的保存目录。</summary>
    public string SaveDirectory { get; private set; } = string.Empty;

    /// <summary>累计成功接收的文件个数。</summary>
    public int ReceivedFileCount { get; private set; }

    /// <summary>累计接收的字节数。</summary>
    public long ReceivedByteCount { get; private set; }

    /// <summary>开始监听。会自行创建保存目录。</summary>
    /// <param name="port">监听端口。</param>
    /// <param name="saveDirectory">文件保存目录。</param>
    public void Start(int port, string saveDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            throw new InvalidOperationException("服务器已经在监听中。");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);

        Directory.CreateDirectory(saveDirectory);

        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        // 端口被 TIME_WAIT 占用时仍可复用，避免"改个端口才能重启"的尴尬
        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.Listen(16);   // 半连接/完成连接队列长度（backlog）

        _listener = listener;
        Port = port;
        SaveDirectory = saveDirectory;
        IsRunning = true;

        _acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "FileServer-Accept"
        };
        _acceptThread.Start();

        Log?.Invoke($"已开始监听 0.0.0.0:{port}，接收目录：{saveDirectory}");
        RunningChanged?.Invoke(true);
    }

    /// <summary>停止监听，并中断正在进行的传输。</summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        _stopSource.Cancel();

        try
        {
            _listener?.Close();
        }
        catch (SocketException)
        {
            // 忽略
        }

        // 关闭所有活动连接，让阻塞在 Receive 上的工作线程马上返回
        List<Socket> clients;
        lock (_sync)
        {
            clients = new List<Socket>(_activeClients);
            _activeClients.Clear();
        }

        foreach (Socket client in clients)
        {
            Abort(client);
        }

        _acceptThread?.Join(2000);
        _acceptThread = null;
        _listener = null;

        Log?.Invoke("已停止监听。");
        RunningChanged?.Invoke(false);
    }

    /// <summary>Accept 循环：运行在后台线程上。</summary>
    private void AcceptLoop()
    {
        while (IsRunning)
        {
            Socket client;
            try
            {
                client = _listener!.Accept();   // 阻塞在此，直到有新客户端
            }
            catch (SocketException ex)
            {
                if (!IsRunning)
                {
                    break;   // 是我们自己 Stop() 关掉监听套接字导致的，正常退出
                }

                Log?.Invoke($"接受连接失败：{ex.SocketErrorCode} {ex.Message}");
                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            string remote = client.RemoteEndPoint?.ToString() ?? "未知";
            Log?.Invoke($"客户端已连接：{remote}");
            ClientConnected?.Invoke(remote);

            lock (_sync)
            {
                _activeClients.Add(client);
            }

            Socket captured = client;
            _ = Task.Run(() => HandleClient(captured, remote));
        }
    }

    /// <summary>处理一个客户端：接收一个文件后关闭它（运行在工作线程/任务上）。</summary>
    private void HandleClient(Socket client, string remote)
    {
        try
        {
            // 安全网：对端卡死（网线拔掉等）时不要永久阻塞，2 分钟后报错退出
            client.ReceiveTimeout = 120_000;

            var receiver = new FileReceiver(client, SaveDirectory);
            TransferResult result = receiver.ReceiveFile(ProgressCallback, _stopSource.Token);

            if (result.Status == TransferStatus.Success)
            {
                ReceivedFileCount++;
                ReceivedByteCount += result.BytesTransferred;
            }

            Log?.Invoke($"[{remote}] {result.Message}（{NetOrder.FormatSize(result.BytesTransferred)}，" +
                        $"回调 {result.CallbackCount} 次）");
            TransferCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[{remote}] 处理失败：{ex.Message}");
        }
        finally
        {
            lock (_sync)
            {
                _activeClients.Remove(client);
            }

            Abort(client);
        }
    }

    private static void Abort(Socket socket)
    {
        try
        {
            socket.Close();
        }
        catch (SocketException)
        {
            // 忽略
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _stopSource.Dispose();
    }
}

using System.Net.Sockets;

namespace TransferCore;

/// <summary>
/// 套接字收发辅助方法。
///
/// 教学要点（实验里最容易翻车的地方）：
///   1. <c>Send</c> 返回的字节数可能**小于**请求发送的字节数（发送缓冲区满、
///      大文件传输时非常常见）。所以必须循环发送，直到 count 归零。
///   2. <c>Receive</c> 同理：一次 Receive 拿到的可能只是报文的一部分（"粘包/拆包"），
///      要读满 n 个字节必须循环。
///   3. Receive 返回 0 表示对端已正常关闭连接（FIN），不是错误，但对我们来说
///      就是"数据没传完"，需要按异常处理。
/// </summary>
internal static class SocketIo
{
    /// <summary>循环发送，直到 count 个字节全部发出。</summary>
    public static void SendAll(Socket socket, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int sent = socket.Send(buffer, offset, count, SocketFlags.None);
            if (sent <= 0)
            {
                throw new IOException("发送失败：连接已被对端关闭。");
            }

            offset += sent;
            count -= sent;
        }
    }

    /// <summary>循环接收，直到读满 count 个字节为止。</summary>
    public static void ReceiveExact(Socket socket, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int received = socket.Receive(buffer, offset, count, SocketFlags.None);
            if (received <= 0)
            {
                throw new IOException("接收失败：对端提前关闭了连接。");
            }

            offset += received;
            count -= received;
        }
    }

    /// <summary>接收最多 count 个字节，返回实际收到的字节数（0 表示对端关闭）。</summary>
    public static int ReceiveSome(Socket socket, byte[] buffer, int offset, int count)
        => socket.Receive(buffer, offset, count, SocketFlags.None);
}

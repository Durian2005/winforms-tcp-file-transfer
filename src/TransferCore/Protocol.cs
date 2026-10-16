namespace TransferCore;

/// <summary>
/// 自定义应用层文件传输协议（FTF1）的常量与报文布局说明。
///
/// 报文结构（所有多字节整数一律使用 **网络字节序 = 大端序**，与实验二"字节序"内容呼应）：
///
///   +--------+--------+----------+--------------+--------------+----------+
///   | 魔数   | 名字长 | 文件长度 | 文件名(UTF-8)|  文件内容    | 校验值   |
///   | 4 字节 | 4 字节 | 8 字节   | nameLen 字节 | fileSize 字节| 8 字节   |
///   +--------+--------+----------+--------------+--------------+----------+
///
///   - 魔数    : "FTF1"，用于识别本协议，防止连错端口时把垃圾数据当报文头
///   - 名字长  : 文件名字段的字节数（UTF-8 编码后），上限 MaxFileNameLength
///   - 文件长度: Int64，因此支持超过 4GB 的大文件（32 位 int 只能到 2GB）
///   - 校验值  : 发送端对**文件内容**逐块计算 FNV-1a 64，作为报文尾部发出，
///               接收端一边落盘一边计算，最后比对，实现"内容完整性校验"
///
/// 服务端处理完一个文件后回送一个应答包：
///
///   +--------+--------+------------------+
///   | 状态   | 消息长 | 消息(UTF-8)      |
///   | 1 字节 | 4 字节 | msgLen 字节      |
///   +--------+--------+------------------+
/// </summary>
public static class Protocol
{
    /// <summary>默认监听端口。</summary>
    public const int DefaultPort = 6000;

    /// <summary>单次收发使用的缓冲区大小（256 KB，兼顾吞吐与内存占用）。</summary>
    public const int BufferSize = 256 * 1024;

    /// <summary>文件名长度上限，防止恶意/异常报文造成内存膨胀。</summary>
    public const int MaxFileNameLength = 512;

    /// <summary>报文头固定部分长度：魔数(4) + 名字长(4) + 文件长度(8) = 16 字节。</summary>
    public const int HeaderSize = 16;

    /// <summary>文件内容校验值长度（FNV-1a 64）。</summary>
    public const int ChecksumSize = 8;

    /// <summary>协议魔数 "FTF1"。</summary>
    public static readonly byte[] Magic = { (byte)'F', (byte)'T', (byte)'F', (byte)'1' };

    /// <summary>应答：成功。</summary>
    public const byte StatusOk = 1;

    /// <summary>应答：失败。</summary>
    public const byte StatusFail = 0;

    /// <summary>判断收到的 4 个字节是否为本协议魔数。</summary>
    public static bool IsMagic(byte[] buffer, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || offset + Magic.Length > buffer.Length)
        {
            return false;
        }

        for (int i = 0; i < Magic.Length; i++)
        {
            if (buffer[offset + i] != Magic[i])
            {
                return false;
            }
        }

        return true;
    }
}

using System.Buffers.Binary;

namespace TransferCore;

/// <summary>
/// 网络字节序（大端）读写工具。
///
/// 说明：x86/x64 是"小端"机器（低位字节存放在低地址），而 TCP/IP 协议栈规定
/// 网络字节序是"大端"。所以任何多字节整数在放进报文之前都必须转换，否则
/// 服务端把 1 读成 16777216（正是实验二 ByteOrder 演示的现象）。
///
/// C 语言里对应 htonl / htons / ntohl / ntohs；.NET 里对应 BinaryPrimitives 一族。
/// 这里额外提供了 64 位版本（C 语言标准库没有 htonll，需要自己拼）。
/// </summary>
public static class NetOrder
{
    /// <summary>把 32 位无符号整数按大端写入缓冲区。</summary>
    public static void WriteUInt32(byte[] buffer, int offset, uint value)
        => BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), value);

    /// <summary>从缓冲区按大端读出 32 位无符号整数。</summary>
    public static uint ReadUInt32(byte[] buffer, int offset)
        => BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));

    /// <summary>把 64 位整数按大端写入缓冲区（用于文件长度，支持 &gt; 4GB）。</summary>
    public static void WriteInt64(byte[] buffer, int offset, long value)
        => BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(offset, 8), value);

    /// <summary>从缓冲区按大端读出 64 位整数。</summary>
    public static long ReadInt64(byte[] buffer, int offset)
        => BinaryPrimitives.ReadInt64BigEndian(buffer.AsSpan(offset, 8));

    /// <summary>把字节长度格式化成人类易读的字符串，如 "1.20 GB"。</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.00} {units[unit]}";
    }

    /// <summary>把秒数格式化成 "1m 23s" 形式。</summary>
    public static string FormatDuration(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
        {
            return "--";
        }

        if (seconds < 60)
        {
            return $"{seconds:0.0}s";
        }

        int minutes = (int)(seconds / 60);
        double rest = seconds - minutes * 60;
        if (minutes < 60)
        {
            return $"{minutes}m {rest:0}s";
        }

        int hours = minutes / 60;
        minutes %= 60;
        return $"{hours}h {minutes}m";
    }
}

namespace TransferCore;

/// <summary>
/// FNV-1a 64 位散列（用作文件内容完整性校验）。
///
/// 选它的理由：算法只有几行、可**增量**计算（一边收发一边算，不需要为了校验
/// 再把文件读第二遍），对本实验"大文件传输"非常合适。
///
/// 算法：
///   hash = 0xCBF29CE484222325 (偏移基数)
///   对每个字节 b:  hash ^= b;  hash *= 0x100000001B3 (质数)
/// </summary>
public sealed class Fnv1a64
{
    private const ulong OffsetBasis = 14695981039346656037UL; // 0xCBF29CE484222325
    private const ulong Prime = 1099511628211UL;              // 0x00000100000001B3

    private ulong _hash = OffsetBasis;

    /// <summary>当前累计的散列值。</summary>
    public ulong Value => _hash;

    /// <summary>把一段内存追加进散列。</summary>
    public void Append(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "offset + count 超出缓冲区长度");
        }

        ulong h = _hash;
        for (int i = 0; i < count; i++)
        {
            h ^= buffer[offset + i];
            h *= Prime;
        }

        _hash = h;
    }

    /// <summary>把整个缓冲区追加进散列。</summary>
    public void Append(byte[] buffer, int count) => Append(buffer, 0, count);

    /// <summary>重置为初始状态，便于复用同一个对象。</summary>
    public void Reset() => _hash = OffsetBasis;

    /// <summary>便捷方法：一次性计算一段内存的散列值。</summary>
    public static ulong Compute(byte[] buffer, int offset, int count)
    {
        var h = new Fnv1a64();
        h.Append(buffer, offset, count);
        return h.Value;
    }
}

using System.Runtime.CompilerServices;
using NewLife;
using NewLife.Log;

namespace NewLife.MQTT.Clusters;

/// <summary>布隆过滤器。基于 MurmurHash3 + 多哈希函数，支持 Add/Contains/Clear 及序列化</summary>
/// <remarks>
/// 使用 k 个哈希函数将元素映射到位数组的 k 个位置。
/// 默认误判率 1%，元素数 100 万时占用约 1.2MB。
/// </remarks>
public class BloomFilter
{
    #region 属性
    /// <summary>位数组（每个 UInt64 存 64 位）</summary>
    private UInt64[] _bits;

    /// <summary>位数组长度（位数）</summary>
    public Int32 BitCount => _bits.Length * 64;

    /// <summary>哈希函数数量</summary>
    public Int32 HashCount { get; }

    /// <summary>已添加元素数（估算）</summary>
    public Int32 Count { get; private set; }

    /// <summary>预期最大元素数</summary>
    public Int32 ExpectedElements { get; }

    /// <summary>目标误判率</summary>
    public Double FalsePositiveRate { get; }
    #endregion

    #region 构造
    /// <summary>创建布隆过滤器</summary>
    /// <param name="expectedElements">预期元素数</param>
    /// <param name="falsePositiveRate">目标误判率，默认 0.01（1%）</param>
    public BloomFilter(Int32 expectedElements, Double falsePositiveRate = 0.01)
    {
        ExpectedElements = expectedElements;
        FalsePositiveRate = falsePositiveRate;

        // 位数组大小 m = -n*ln(p) / (ln2)^2
        var bitSize = (Int32)(-expectedElements * Math.Log(falsePositiveRate) / (Math.Log(2) * Math.Log(2)));
        // 对齐到 64 位边界
        bitSize = (bitSize + 63) / 64 * 64;
        if (bitSize < 64) bitSize = 64;

        _bits = new UInt64[bitSize / 64];

        // 哈希函数数量 k = (m/n) * ln2
        HashCount = Math.Max(1, (Int32)((Double)bitSize / expectedElements * Math.Log(2)));
    }

    /// <summary>从已有位数组还原布隆过滤器</summary>
    /// <param name="bits">序列化的位数组</param>
    /// <param name="hashCount">哈希函数数量</param>
    /// <param name="expectedElements">预期元素数</param>
    /// <param name="falsePositiveRate">目标误判率</param>
    public BloomFilter(UInt64[] bits, Int32 hashCount, Int32 expectedElements, Double falsePositiveRate = 0.01)
    {
        _bits = bits;
        HashCount = hashCount;
        ExpectedElements = expectedElements;
        FalsePositiveRate = falsePositiveRate;
    }
    #endregion

    #region 方法
    /// <summary>添加元素</summary>
    /// <param name="data">元素数据</param>
    public void Add(ReadOnlySpan<Byte> data)
    {
        var positions = ComputePositions(data);
        foreach (var pos in positions)
        {
            SetBit(pos);
        }

        Count++;
    }

    /// <summary>检查元素是否可能存在</summary>
    /// <param name="data">元素数据</param>
    /// <returns>true 表示可能存在（可能误判），false 表示一定不存在</returns>
    public Boolean Contains(ReadOnlySpan<Byte> data)
    {
        var positions = ComputePositions(data);
        foreach (var pos in positions)
        {
            if (!GetBit(pos)) return false;
        }

        return true;
    }

    /// <summary>清空过滤器</summary>
    public void Clear()
    {
        Array.Clear(_bits, 0, _bits.Length);
        Count = 0;
    }

    /// <summary>获取位数组的副本（用于序列化）</summary>
    public UInt64[] GetBits()
    {
        var copy = new UInt64[_bits.Length];
        Array.Copy(_bits, copy, _bits.Length);
        return copy;
    }
    #endregion

    #region 位操作
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetBit(Int32 index)
    {
        var bucket = index >> 6;  // index / 64
        var offset = index & 63;  // index % 64
        _bits[bucket] |= 1UL << offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Boolean GetBit(Int32 index)
    {
        var bucket = index >> 6;
        var offset = index & 63;
        return (_bits[bucket] & (1UL << offset)) != 0;
    }
    #endregion

    #region 哈希
    /// <summary>计算 k 个哈希位置</summary>
    private Int32[] ComputePositions(ReadOnlySpan<Byte> data)
    {
        var positions = new Int32[HashCount];
        var bitCount = BitCount;

        // 使用 MurmurHash3 计算基础哈希，再通过双重哈希生成 k 个位置
        var h1 = MurmurHash3.Hash32(data, 0);
        var h2 = MurmurHash3.Hash32(data, (UInt32)h1);

        for (var i = 0; i < HashCount; i++)
        {
            var combined = (UInt32)(h1 + i * h2);
            positions[i] = (Int32)(combined % (UInt32)bitCount);
            if (positions[i] < 0) positions[i] += bitCount;
        }

        return positions;
    }
    #endregion

    #region 日志
    private static readonly ILog Log = XTrace.Log;
    #endregion
}

/// <summary>MurmurHash3 32 位哈希实现</summary>
/// <remarks>零外部依赖，用于布隆过滤器的哈希计算</remarks>
internal static class MurmurHash3
{
    private const UInt32 C1 = 0xcc9e2d51;
    private const UInt32 C2 = 0x1b873593;

    /// <summary>计算 32 位 MurmurHash3</summary>
    /// <param name="data">输入数据</param>
    /// <param name="seed">种子值</param>
    public static Int32 Hash32(ReadOnlySpan<Byte> data, UInt32 seed)
    {
        var h1 = seed;
        var len = data.Length;

        // 按 4 字节块处理
        var i = 0;
#if NETSTANDARD2_0 || NETFRAMEWORK
        var array = data.ToArray();
        while (i + 4 <= len)
        {
            var k1 = BitConverter.ToUInt32(array, i);
#else
        while (i + 4 <= len)
        {
            var k1 = BitConverter.ToUInt32(data.Slice(i, 4));
#endif
            i += 4;

            k1 *= C1;
            k1 = RotateLeft(k1, 15);
            k1 *= C2;

            h1 ^= k1;
            h1 = RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
        }

        // 处理尾部
        UInt32 k2 = 0;
        var tail = len - i;
        if (tail >= 3) k2 ^= (UInt32)data[i + 2] << 16;
        if (tail >= 2) k2 ^= (UInt32)data[i + 1] << 8;
        if (tail >= 1)
        {
            k2 ^= data[i];
            k2 *= C1;
            k2 = RotateLeft(k2, 15);
            k2 *= C2;
            h1 ^= k2;
        }

        // 最终混淆
        h1 ^= (UInt32)len;
        h1 = FMix(h1);

        return (Int32)h1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32 RotateLeft(UInt32 x, Int32 r) => (x << r) | (x >> (32 - r));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32 FMix(UInt32 h)
    {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }
}

namespace DiagKit.FlashFiles.Common.Utilities;

/// <summary>
/// CRC-32 校验计算器，支持多种 CRC-32 变体。<br/>CRC-32 checksum calculator supporting multiple CRC-32 variants.
/// </summary>
public sealed class UdsCrc32
{
    /// <summary>
    /// CRC-32/MPEG-2（多项式 0x04C11DB7，无反射）<br/>CRC-32/MPEG-2 (polynomial 0x04C11DB7, no reflection)
    /// </summary>
    public static UdsCrc32 Mpeg2 { get; } = new(0x04C11DB7, 0xFFFFFFFF, 0x00000000, false, false);

    /// <summary>
    /// CRC-32 标准（多项式 0x04C11DB7，反射输入输出）<br/>CRC-32 Standard (polynomial 0x04C11DB7, reflected input and output)
    /// </summary>
    public static UdsCrc32 Standard { get; } = new(0x04C11DB7, 0xFFFFFFFF, 0xFFFFFFFF, true, true);

    /// <summary>
    /// CRC-32/POSIX（多项式 0x04C11DB7，无反射）<br/>CRC-32/POSIX (polynomial 0x04C11DB7, no reflection)
    /// </summary>
    public static UdsCrc32 Posix { get; } = new(0x04C11DB7, 0x00000000, 0xFFFFFFFF, false, false);

    /// <summary>
    /// CRC-32/AUTOSAR（多项式 0xF4ACFB13，反射输入输出）<br/>CRC-32/AUTOSAR (polynomial 0xF4ACFB13, reflected input and output)
    /// </summary>
    public static UdsCrc32 AutoSar { get; } = new(0xF4ACFB13, 0xFFFFFFFF, 0xFFFFFFFF, true, true);

    private readonly uint[] table;
    private readonly uint initialValue;
    private readonly uint xorOut;
    private readonly bool refIn;
    private readonly bool refOut;

    /// <summary>
    /// 使用指定的 CRC 参数构造 UdsCrc32 实例<br/>Constructs a UdsCrc32 instance with the specified CRC parameters
    /// </summary>
    /// <param name="poly">生成多项式 / generator polynomial</param>
    /// <param name="init">初始值 / initial value</param>
    /// <param name="xorOut">输出异或值 / output XOR value</param>
    /// <param name="refIn">是否反射输入 / whether to reflect input bytes</param>
    /// <param name="refOut">是否反射输出 / whether to reflect the output</param>
    public UdsCrc32(uint poly, uint init, uint xorOut, bool refIn, bool refOut)
    {
        initialValue = init;
        this.xorOut = xorOut;
        this.refIn = refIn;
        this.refOut = refOut;
        table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint r = refIn ? Reverse(i, 8) << 24 : i << 24;
            for (int j = 0; j < 8; j++)
            {
                r = (r & 0x80000000) != 0 ? (r << 1) ^ poly : (r << 1);
            }
            table[i] = refIn ? Reverse(r, 32) : r;
        }
    }

    /// <summary>
    /// 计算给定数据的 CRC-32 校验值<br/>Computes the CRC-32 checksum for the given data
    /// </summary>
    /// <param name="data">输入数据 / input data</param>
    /// <returns>CRC-32 校验值 / CRC-32 checksum value</returns>
    public uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = initialValue;
        foreach (byte b in data)
        {
            if (refIn)
                crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];
            else
                crc = (crc << 8) ^ table[((crc >> 24) ^ b) & 0xFF];
        }
        if (refIn != refOut)
            crc = Reverse(crc, 32);
        return crc ^ xorOut;
    }

    private static uint Reverse(uint value, int bits)
    {
        uint result = 0;
        for (int i = 0; i < bits; i++)
        {
            if ((value & (1u << i)) != 0)
                result |= (1u << (bits - 1 - i));
        }
        return result;
    }
}

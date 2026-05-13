using System.Runtime.CompilerServices;

namespace DiagKit.FlashFiles.Common.Utilities;

/// <summary>
/// 统一的校验和计算实现，消除各格式间的重复代码。
/// </summary>
internal static class ChecksumCalculator
{
    /// <summary>
    /// Intel HEX 校验和：所有字节（不含冒号）之和的补码。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte IntelChecksum(ReadOnlySpan<byte> recordPayload)
    {
        var sum = 0;
        foreach (var b in recordPayload)
            sum += b;
        return (byte)(0x100 - (sum & 0xFF));
    }

    /// <summary>
    /// Motorola S-Record 校验和：0xFF 减去所有字节之和的低 8 位。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte MotorolaChecksum(ReadOnlySpan<byte> rawData)
    {
        var sum = 0u;
        for (var i = 0; i < rawData.Length - 1; i++)
            sum += rawData[i];
        return (byte)(0xFF - (sum & 0xFF));
    }
}

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DiagKit.FlashFiles.Common.Utilities;

/// <summary>
/// 字节转换器<br/>Byte converter
/// </summary>
/// <remarks>
/// 避免创建数组以提高性能。<br/>Avoids array allocation for performance.
/// </remarks>
public static class ByteConverter
{
    /// <summary>
    /// 将单个十六进制字符转换为字节值<br/>Converts a single hex character to its byte value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte FromHexCharToByte(char c)
    {
        return (byte)(c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 0xA,
            >= 'A' and <= 'F' => c - 'A' + 0xA,
            _ => throw new FormatException($"无效的十六进制字符 '{c}'。")
        });
    }

    /// <summary>
    /// 将两个十六进制字符转换为一个字节<br/>Converts two hex characters to a single byte
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte FromHexCharsToByte(ReadOnlySpan<char> source)
    {
        return (byte)((FromHexCharToByte(source[0]) << 4) | FromHexCharToByte(source[1]));
    }

    /// <summary>
    /// 将十六进制字符串写入目标字节缓冲区（扩展方法）<br/>Writes hex string bytes into a destination span (extension method)
    /// </summary>
    public static void WriteBytesFromHexChars(this Span<byte> destination, ReadOnlySpan<char> source)
    {
        FromHexString(source, destination);
    }

    /// <summary>
    /// 将十六进制字符串转换为字节数组<br/>Converts a hex string to a byte array
    /// </summary>
    /// <remarks>
    /// 源长度必须为偶数，目标长度不能小于源长度的一半。<br/>Source length must be even; destination length must be at least half the source length.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FromHexString(ReadOnlySpan<char> source, Span<byte> destination)
    {
        if ((source.Length & 1) != 0)
        {
            throw new ArgumentException("源长度必须是偶数。");
        }
        if (destination.Length < source.Length / 2)
        {
            throw new ArgumentException("目标长度必须不小于源长度的一半。");
        }

        for (var i = 0; i < source.Length / 2; i++)
        {
            destination[i] = FromHexCharsToByte(source.Slice(i * 2, 2));
        }
    }

    /// <summary>
    /// 以大端序读取 24 位无符号整数<br/>Reads a 24-bit unsigned integer in big-endian order
    /// </summary>
    public static uint ReadUInt24BigEndian(ReadOnlySpan<byte> span)
    {
        return (uint)(span[0] << 16 | span[1] << 8 | span[2]);
    }

    /// <summary>
    /// 以大端序写入 16 位无符号整数<br/>Writes a 16-bit unsigned integer in big-endian order
    /// </summary>
    public static void FromUInt16ToBigEndian(ushort value, Span<byte> destination) => BinaryPrimitives.WriteUInt16BigEndian(destination, value);

    /// <summary>
    /// 以大端序写入 24 位无符号整数<br/>Writes a 24-bit unsigned integer in big-endian order
    /// </summary>
    public static void FromUInt24ToBigEndian(uint value, Span<byte> destination)
    {
        if (destination.Length < 3) throw new ArgumentException("目标长度不足3字节");
        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }

    /// <summary>
    /// 以大端序写入 32 位无符号整数<br/>Writes a 32-bit unsigned integer in big-endian order
    /// </summary>
    public static void FromUInt32ToBigEndian(uint value, Span<byte> destination) => BinaryPrimitives.WriteUInt32BigEndian(destination, value);

    /// <summary>
    /// 以大端序写入 64 位无符号整数<br/>Writes a 64-bit unsigned integer in big-endian order
    /// </summary>
    public static void FromUInt64ToBigEndian(ulong value, Span<byte> destination) => BinaryPrimitives.WriteUInt64BigEndian(destination, value);
}

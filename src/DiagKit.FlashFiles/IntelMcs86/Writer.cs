using DiagKit.FlashFiles.Common.Utilities;

using System.Buffers;
using System.Text;

namespace DiagKit.FlashFiles.IntelMcs86;

/// <summary>
/// Intel MCS-86 HEX 文件写入器。
/// </summary>
internal static class Writer
{
    private const int MaxDataBytesPerRecord = 16;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// 将连续地址块数据写入流（Intel HEX 格式）。
    /// </summary>
    /// <param name="stream">输出流。</param>
    /// <param name="blocks">数据块列表（需按地址排序）。</param>
    /// <param name="dataSize">每个地址的数据大小（字节）。</param>
    internal static void Write(Stream stream, IReadOnlyList<FlashBlock> blocks, byte dataSize)
    {
        if (dataSize == 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "地址数据大小必须大于 0。");

        using var writer = new StreamWriter(stream, Utf8NoBom, bufferSize: 8192, leaveOpen: true);
        ushort lastHighAddress = 0;
        var maxDataBytesPerRecord = Math.Min(byte.MaxValue, Math.Max(MaxDataBytesPerRecord, (int)dataSize));

        foreach (var block in blocks)
        {
            var data = block.Data.Span;
            if (data.Length % dataSize != 0)
                throw new InvalidOperationException("块数据长度必须是 DataSize 的整数倍。");

            int offset = 0;

            while (offset < data.Length)
            {
                var currentAddress = block.StartAddress + (ulong)(offset / dataSize);
                ushort highAddr = (ushort)(currentAddress >> 16);
                ushort lowAddr = (ushort)(currentAddress & 0xFFFF);

                if (highAddr != lastHighAddress)
                {
                    WriteExtendedLinearAddressRecord(writer, highAddr);
                    lastHighAddress = highAddr;
                }

                int bytesToWrite = Math.Min(maxDataBytesPerRecord, data.Length - offset);
                var maxBytesInSegment = (0x10000UL - lowAddr) * dataSize;
                bytesToWrite = (int)Math.Min((ulong)bytesToWrite, maxBytesInSegment);
                bytesToWrite -= bytesToWrite % dataSize;
                if (bytesToWrite <= 0)
                    throw new InvalidOperationException("无法在当前地址边界写入完整地址数据。");

                WriteDataRecord(writer, lowAddr, data.Slice(offset, bytesToWrite));
                offset += bytesToWrite;
            }
        }

        WriteEndOfFileRecord(writer);
        writer.Flush();
    }

    /// <summary>
    /// 将原始字节数据写入流（Intel HEX 格式）。
    /// </summary>
    /// <param name="stream">输出流。</param>
    /// <param name="address">起始地址。</param>
    /// <param name="data">数据。</param>
    internal static void Write(Stream stream, ulong address, ReadOnlySpan<byte> data)
    {
        using var writer = new StreamWriter(stream, Utf8NoBom, bufferSize: 8192, leaveOpen: true);
        ushort lastHighAddress = 0;
        int offset = 0;

        while (offset < data.Length)
        {
            var currentAddress = address + (ulong)offset;
            ushort highAddr = (ushort)(currentAddress >> 16);
            ushort lowAddr = (ushort)(currentAddress & 0xFFFF);

            if (highAddr != lastHighAddress)
            {
                WriteExtendedLinearAddressRecord(writer, highAddr);
                lastHighAddress = highAddr;
            }

            int bytesToWrite = Math.Min(MaxDataBytesPerRecord, data.Length - offset);
            if (lowAddr + bytesToWrite > 0x10000)
                bytesToWrite = 0x10000 - lowAddr;

            WriteDataRecord(writer, lowAddr, data.Slice(offset, bytesToWrite));
            offset += bytesToWrite;
        }

        WriteEndOfFileRecord(writer);
        writer.Flush();
    }

    private static void WriteExtendedLinearAddressRecord(StreamWriter writer, ushort highAddress)
    {
        Span<byte> record = stackalloc byte[6];
        record[0] = 0x02;
        record[1] = 0x00;
        record[2] = 0x00;
        record[3] = (byte)RecordType.ExtendedLinearAddressRecord;
        record[4] = (byte)(highAddress >> 8);
        record[5] = (byte)(highAddress & 0xFF);

        var checksum = ChecksumCalculator.IntelChecksum(record);
        writer.Write(':');
        WriteHexBytes(writer, record);
        WriteHexByte(writer, checksum);
        writer.WriteLine();
    }

    private static void WriteDataRecord(StreamWriter writer, ushort address, ReadOnlySpan<byte> data)
    {
        var totalLen = 4 + data.Length;
        using var mo = MemoryPool<byte>.Shared.Rent(totalLen);
        var record = mo.Memory[..totalLen].Span;
        record[0] = (byte)data.Length;
        record[1] = (byte)(address >> 8);
        record[2] = (byte)(address & 0xFF);
        record[3] = (byte)RecordType.DataRecord;
        data.CopyTo(record[4..]);

        var checksum = ChecksumCalculator.IntelChecksum(record);
        writer.Write(':');
        WriteHexBytes(writer, record);
        WriteHexByte(writer, checksum);
        writer.WriteLine();
    }

    private static void WriteEndOfFileRecord(StreamWriter writer)
    {
        writer.WriteLine(":00000001FF");
    }

    private static void WriteHexBytes(StreamWriter writer, ReadOnlySpan<byte> data)
    {
        Span<char> chars = stackalloc char[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            chars[i * 2] = ToHexChar(b >> 4);
            chars[i * 2 + 1] = ToHexChar(b & 0x0F);
        }
        writer.Write(chars);
    }

    private static void WriteHexByte(StreamWriter writer, byte value)
    {
        Span<char> chars = stackalloc char[2];
        chars[0] = ToHexChar(value >> 4);
        chars[1] = ToHexChar(value & 0x0F);
        writer.Write(chars);
    }

    private static char ToHexChar(int value) => value switch
    {
        >= 0 and <= 9 => (char)('0' + value),
        >= 10 and <= 15 => (char)('A' + value - 10),
        _ => '0'
    };
}

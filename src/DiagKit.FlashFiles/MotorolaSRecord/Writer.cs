using DiagKit.FlashFiles.Common.Utilities;

using System.Buffers;
using System.Text;

namespace DiagKit.FlashFiles.MotorolaSRecord;

/// <summary>
/// Motorola S-Record 子格式。
/// </summary>
internal enum SRecordFormat
{
    S19,
    S28,
    S37,
}

/// <summary>
/// Motorola S-Record 文件写入器。
/// </summary>
internal static class Writer
{
    private const int MaxDataBytesPerRecord = 16;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly byte[] HeaderData = Encoding.ASCII.GetBytes("DiagKit.FlashFiles");

    /// <summary>
    /// 自动选择最小可容纳地址范围的 S-Record 格式并写入流。
    /// </summary>
    internal static void Write(Stream stream, IReadOnlyList<FlashBlock> blocks, byte dataSize)
    {
        var format = SelectSmallestFormat(blocks);
        Write(stream, blocks, dataSize, format);
    }

    /// <summary>
    /// 使用指定 S-Record 子格式写入流。
    /// </summary>
    internal static void Write(Stream stream, IReadOnlyList<FlashBlock> blocks, byte dataSize, SRecordFormat format)
    {
        var dataRecordCount = ValidateCanWriteCore(blocks, dataSize, format);

        using var writer = new StreamWriter(stream, Utf8NoBom, bufferSize: 8192, leaveOpen: true);

        WriteRecord(writer, RecordType.S0, 0, HeaderData);

        foreach (var block in blocks)
        {
            var data = block.Data.Span;
            if (data.Length % dataSize != 0)
                throw new InvalidOperationException("块数据长度必须是 DataSize 的整数倍。");

            var offset = 0;
            while (offset < data.Length)
            {
                var currentAddress = block.StartAddress + (ulong)(offset / dataSize);
                var bytesToWrite = Math.Min(MaxDataBytesPerRecord, data.Length - offset);
                bytesToWrite -= bytesToWrite % dataSize;
                if (bytesToWrite <= 0)
                    throw new InvalidOperationException("无法写入完整地址数据。");

                WriteRecord(writer, GetDataRecordType(format), currentAddress, data.Slice(offset, bytesToWrite));
                offset += bytesToWrite;
            }
        }

        WriteCountRecord(writer, dataRecordCount);
        WriteRecord(writer, GetTerminationRecordType(format), 0, ReadOnlySpan<byte>.Empty);
        writer.Flush();
    }

    /// <summary>
    /// 验证指定 S-Record 子格式是否可写入。
    /// </summary>
    internal static void ValidateCanWrite(IReadOnlyList<FlashBlock> blocks, byte dataSize, SRecordFormat format)
    {
        _ = ValidateCanWriteCore(blocks, dataSize, format);
    }

    private static ulong ValidateCanWriteCore(IReadOnlyList<FlashBlock> blocks, byte dataSize, SRecordFormat format)
    {
        if (dataSize == 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "地址数据大小必须大于 0。");
        if (dataSize > MaxDataBytesPerRecord)
            throw new ArgumentOutOfRangeException(nameof(dataSize), $"地址数据大小不能超过单条记录的数据字节上限 {MaxDataBytesPerRecord}。");

        ValidateAddressRange(blocks, format);
        return CountDataRecords(blocks, dataSize);
    }

    private static SRecordFormat SelectSmallestFormat(IReadOnlyList<FlashBlock> blocks)
    {
        var highestAddress = 0ul;
        foreach (var block in blocks)
            highestAddress = Math.Max(highestAddress, block.EndAddress);

        return highestAddress switch
        {
            <= 0xFFFFUL => SRecordFormat.S19,
            <= 0xFFFFFFUL => SRecordFormat.S28,
            <= 0xFFFFFFFFUL => SRecordFormat.S37,
            _ => throw new InvalidOperationException($"Motorola S-Record 仅支持 32-bit 地址，最高地址 0x{highestAddress:X} 超出 0xFFFFFFFF。"),
        };
    }

    private static void ValidateAddressRange(IReadOnlyList<FlashBlock> blocks, SRecordFormat format)
    {
        var maxAddress = GetMaxAddress(format);
        foreach (var block in blocks)
        {
            if (block.EndAddress > maxAddress)
                throw new InvalidOperationException($"Motorola S-Record {format} 最高支持地址 0x{maxAddress:X}，数据块结束地址 0x{block.EndAddress:X} 超出范围。");
        }
    }

    private static ulong CountDataRecords(IReadOnlyList<FlashBlock> blocks, byte dataSize)
    {
        var dataRecordCount = 0ul;
        foreach (var block in blocks)
        {
            var dataLength = block.Data.Length;
            if (dataLength % dataSize != 0)
                throw new InvalidOperationException("块数据长度必须是 DataSize 的整数倍。");

            var offset = 0;
            while (offset < dataLength)
            {
                var bytesToWrite = Math.Min(MaxDataBytesPerRecord, dataLength - offset);
                bytesToWrite -= bytesToWrite % dataSize;
                if (bytesToWrite <= 0)
                    throw new InvalidOperationException("无法写入完整地址数据。");

                dataRecordCount++;
                if (dataRecordCount > ushort.MaxValue)
                    throw new InvalidOperationException("Motorola S-Record 数据记录数量超过 S5 计数记录范围。");

                offset += bytesToWrite;
            }
        }

        return dataRecordCount;
    }

    private static ulong GetMaxAddress(SRecordFormat format) => format switch
    {
        SRecordFormat.S19 => 0xFFFFUL,
        SRecordFormat.S28 => 0xFFFFFFUL,
        SRecordFormat.S37 => 0xFFFFFFFFUL,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static RecordType GetDataRecordType(SRecordFormat format) => format switch
    {
        SRecordFormat.S19 => RecordType.S1,
        SRecordFormat.S28 => RecordType.S2,
        SRecordFormat.S37 => RecordType.S3,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static RecordType GetTerminationRecordType(SRecordFormat format) => format switch
    {
        SRecordFormat.S19 => RecordType.S9,
        SRecordFormat.S28 => RecordType.S8,
        SRecordFormat.S37 => RecordType.S7,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static int GetAddressLength(RecordType recordType) => recordType switch
    {
        RecordType.S0 or RecordType.S1 or RecordType.S5 or RecordType.S9 => 2,
        RecordType.S2 or RecordType.S6 or RecordType.S8 => 3,
        RecordType.S3 or RecordType.S7 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(recordType)),
    };

    private static void WriteCountRecord(StreamWriter writer, ulong dataRecordCount)
    {
        if (dataRecordCount > ushort.MaxValue)
            throw new InvalidOperationException("Motorola S-Record 数据记录数量超过 S5 计数记录范围。");

        WriteRecord(writer, RecordType.S5, dataRecordCount, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteRecord(StreamWriter writer, RecordType recordType, ulong address, ReadOnlySpan<byte> data)
    {
        var addressLength = GetAddressLength(recordType);
        var count = addressLength + data.Length + 1;
        if (count > byte.MaxValue)
            throw new InvalidOperationException("Motorola S-Record 单条记录长度超过 Count 字段可表示范围。");

        var totalLength = count + 1;
        using var owner = MemoryPool<byte>.Shared.Rent(totalLength);
        var record = owner.Memory.Span[..totalLength];
        record[0] = (byte)count;
        WriteAddress(address, record.Slice(1, addressLength));
        data.CopyTo(record[(1 + addressLength)..^1]);
        record[^1] = ChecksumCalculator.MotorolaChecksum(record);

        writer.Write('S');
        writer.Write((char)('0' + (byte)recordType));
        WriteHexBytes(writer, record);
        writer.WriteLine();
    }

    private static void WriteAddress(ulong address, Span<byte> destination)
    {
        switch (destination.Length)
        {
            case 2:
                if (address > 0xFFFFUL)
                    throw new InvalidOperationException($"地址 0x{address:X} 超出 16-bit S-Record 范围。");
                destination[0] = (byte)(address >> 8);
                destination[1] = (byte)address;
                break;
            case 3:
                if (address > 0xFFFFFFUL)
                    throw new InvalidOperationException($"地址 0x{address:X} 超出 24-bit S-Record 范围。");
                destination[0] = (byte)(address >> 16);
                destination[1] = (byte)(address >> 8);
                destination[2] = (byte)address;
                break;
            case 4:
                if (address > 0xFFFFFFFFUL)
                    throw new InvalidOperationException($"地址 0x{address:X} 超出 32-bit S-Record 范围。");
                destination[0] = (byte)(address >> 24);
                destination[1] = (byte)(address >> 16);
                destination[2] = (byte)(address >> 8);
                destination[3] = (byte)address;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(destination), "S-Record 地址长度必须是 2、3 或 4 字节。");
        }
    }

    private static void WriteHexBytes(StreamWriter writer, ReadOnlySpan<byte> data)
    {
        Span<char> chars = stackalloc char[data.Length * 2];
        for (var i = 0; i < data.Length; i++)
        {
            var value = data[i];
            chars[i * 2] = ToHexChar(value >> 4);
            chars[i * 2 + 1] = ToHexChar(value & 0x0F);
        }

        writer.Write(chars);
    }

    private static char ToHexChar(int value) => value switch
    {
        >= 0 and <= 9 => (char)('0' + value),
        >= 10 and <= 15 => (char)('A' + value - 10),
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}

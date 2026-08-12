using DiagKit.FlashFiles.Common.Utilities;

using System.Text;

namespace DiagKit.FlashFiles.IntelMcs86;

/// <summary>
/// Intel MCS-86 HEX 文件解析器。
/// </summary>
internal static class Parser
{
    /// <summary>
    /// 从行序列解析 Intel HEX 数据，返回连续地址块列表。
    /// </summary>
    internal static List<FlashBlock> Parse(IEnumerable<string> lines, byte dataSize, bool checkValid)
        => Parse(lines, new FlashLoadOptions(dataSize) { ValidateChecksums = checkValid });

    /// <summary>
    /// 从行序列解析 Intel HEX 数据，返回连续地址块列表。
    /// </summary>
    internal static List<FlashBlock> Parse(IEnumerable<string> lines, FlashLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ParserBlockBuilder.ValidateDataSize(options.DataSize);
        return ParseCore(lines, options);
    }

    /// <summary>
    /// 从流解析 Intel HEX 数据。
    /// </summary>
    internal static List<FlashBlock> Parse(Stream stream, byte dataSize, bool checkValid)
        => Parse(stream, new FlashLoadOptions(dataSize) { ValidateChecksums = checkValid });

    /// <summary>
    /// 从流解析 Intel HEX 数据。
    /// </summary>
    internal static List<FlashBlock> Parse(Stream stream, FlashLoadOptions options)
    {
        var lines = ReadLines(stream);
        return Parse(lines, options);
    }

    private static List<FlashBlock> ParseCore(IEnumerable<string> lines, FlashLoadOptions options)
    {
        var segments = new List<ParsedDataSegment>();
        using var record = new RecordItem();
        ushort baseAddress = 0;
        var addressMode = AddressMode.ExtendedLinear;
        var lineNumber = 0;
        var eofSeen = false;

        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (eofSeen)
            {
                switch (options.RecordsAfterEndOfFileBehavior)
                {
                    case RecordsAfterEndOfFileBehavior.Reject:
                        throw new FormatException($"EOF 记录之后不能再出现有效记录，行数：{lineNumber}。");
                    case RecordsAfterEndOfFileBehavior.Ignore:
                        continue;
                    case RecordsAfterEndOfFileBehavior.Parse:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(options.RecordsAfterEndOfFileBehavior));
                }
            }

            record.Update(line, baseAddress);
            ValidateRecord(record, options, lineNumber);

            switch (record.RecordType)
            {
                case RecordType.ExtendedLinearAddressRecord:
                    if (!record.TryReadBaseAddress(out baseAddress))
                        throw new FormatException("获取扩展线性地址失败。");
                    addressMode = AddressMode.ExtendedLinear;
                    break;

                case RecordType.ExtendedSegmentAddressRecord:
                    if (!record.TryReadBaseAddress(out baseAddress))
                        throw new FormatException("获取扩展段地址失败。");
                    addressMode = AddressMode.ExtendedSegment;
                    break;

                case RecordType.StartSegmentAddressRecord:
                case RecordType.StartLinearAddressRecord:
                    // 入口点记录，不包含数据，跳过
                    break;

                case RecordType.DataRecord:
                    var fullAddress = ComputeFullAddress(baseAddress, record.Address, addressMode);
                    ParserBlockBuilder.AddSegment(
                        segments,
                        fullAddress,
                        record.Data,
                        options.DataSize,
                        lineNumber,
                        options.ValidateDataRecordLength,
                        options.DataRecordPaddingValue);
                    break;

                case RecordType.EndOfFileRecord:
                    eofSeen = true;
                    break;
            }
        }

        if (options.RequireEndOfFile && !eofSeen)
            throw new FormatException("Intel HEX 文件缺少 EOF 记录。");

        return ParserBlockBuilder.BuildBlocks(segments, options.DataSize);
    }

    private static ulong ComputeFullAddress(ushort baseAddr, ushort offset, AddressMode mode) =>
        mode switch
        {
            AddressMode.ExtendedLinear => ((ulong)baseAddr << 16) | offset,
            AddressMode.ExtendedSegment => ((ulong)baseAddr << 4) | offset,
            _ => ((ulong)baseAddr << 16) | offset,
        };

    private static void ValidateRecord(RecordItem record, FlashLoadOptions options, int lineNumber)
    {
        switch (record.RecordType)
        {
            case RecordType.DataRecord:
                break;
            case RecordType.EndOfFileRecord:
                if (options.ValidateRecordTypeLength)
                    EnsureCount(record, 0, lineNumber);
                if (options.ValidateNonDataRecordAddress)
                    EnsureAddressZero(record, lineNumber);
                break;
            case RecordType.ExtendedSegmentAddressRecord:
            case RecordType.ExtendedLinearAddressRecord:
                if (options.ValidateRecordTypeLength)
                    EnsureCount(record, 2, lineNumber);
                if (options.ValidateNonDataRecordAddress)
                    EnsureAddressZero(record, lineNumber);
                break;
            case RecordType.StartSegmentAddressRecord:
            case RecordType.StartLinearAddressRecord:
                if (options.ValidateRecordTypeLength)
                    EnsureCount(record, 4, lineNumber);
                if (options.ValidateNonDataRecordAddress)
                    EnsureAddressZero(record, lineNumber);
                break;
            default:
                throw new FormatException($"未知的 Intel HEX 记录类型：0x{(byte)record.RecordType:X2}，行数：{lineNumber}。");
        }

        if (options.ValidateChecksums && !record.IsValid)
            throw new FormatException($"校验和验证失败，行数：{lineNumber}。");
    }

    private static void EnsureCount(RecordItem record, byte expectedCount, int lineNumber)
    {
        if (record.Count != expectedCount)
            throw new FormatException($"Intel HEX 记录数据长度不符合记录类型要求，行数：{lineNumber}。");
    }

    private static void EnsureAddressZero(RecordItem record, int lineNumber)
    {
        if (record.Address != 0)
            throw new FormatException($"Intel HEX 非数据记录的地址字段必须为 0，行数：{lineNumber}。");
    }

    private static List<string> ReadLines(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        var lines = new List<string>(capacity: 16384);
        string? line;
        while ((line = reader.ReadLine()) is not null)
            lines.Add(line);
        return lines;
    }

    private enum AddressMode
    {
        ExtendedLinear,
        ExtendedSegment,
    }
}

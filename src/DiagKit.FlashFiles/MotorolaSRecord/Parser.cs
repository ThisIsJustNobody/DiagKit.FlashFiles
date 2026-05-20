using DiagKit.FlashFiles.Common.Utilities;

using System.Buffers;
using System.Text;

namespace DiagKit.FlashFiles.MotorolaSRecord;

/// <summary>
/// Motorola S-Record 文件解析器。
/// </summary>
internal static class Parser
{
    /// <summary>
    /// 从行序列解析 Motorola S-Record 数据，返回连续地址块列表。
    /// </summary>
    internal static List<FlashBlock> Parse(IEnumerable<string> lines, byte dataSize, bool checkValid)
        => Parse(lines, new FlashLoadOptions(dataSize) { ValidateChecksums = checkValid });

    /// <summary>
    /// 从行序列解析 Motorola S-Record 数据，返回连续地址块列表。
    /// </summary>
    internal static List<FlashBlock> Parse(IEnumerable<string> lines, FlashLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ParserBlockBuilder.ValidateDataSize(options.DataSize);
        return ParseCore(lines, options);
    }

    /// <summary>
    /// 从流解析 Motorola S-Record 数据。
    /// </summary>
    internal static List<FlashBlock> Parse(Stream stream, byte dataSize, bool checkValid)
        => Parse(stream, new FlashLoadOptions(dataSize) { ValidateChecksums = checkValid });

    /// <summary>
    /// 从流解析 Motorola S-Record 数据。
    /// </summary>
    internal static List<FlashBlock> Parse(Stream stream, FlashLoadOptions options)
    {
        var lines = ReadLines(stream);
        return Parse(lines, options);
    }

    private static List<FlashBlock> ParseCore(IEnumerable<string> lines, FlashLoadOptions options)
    {
        var segments = new List<ParsedDataSegment>();
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(RecordItemSlim.DefaultBufferSize);
        var recordLineBuffer = memoryOwner.Memory;
        recordLineBuffer.Span.Clear();

        var lineNumber = 0;
        var dataRecordCount = 0ul;
        var headerSeen = false;
        var dataSeen = false;
        var terminationSeen = false;

        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (terminationSeen)
            {
                switch (options.RecordsAfterEndOfFileBehavior)
                {
                    case RecordsAfterEndOfFileBehavior.Reject:
                        throw new FormatException($"结束记录之后不能再出现有效记录，行数：{lineNumber}。");
                    case RecordsAfterEndOfFileBehavior.Ignore:
                        continue;
                    case RecordsAfterEndOfFileBehavior.Parse:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(options.RecordsAfterEndOfFileBehavior));
                }
            }

            var record = new RecordItemSlim(line, recordLineBuffer.Span);
            ValidateRecord(record, options, lineNumber);

            switch (record.RecordType)
            {
                case RecordType.S0:
                    if (options.ValidateMotorolaHeaderPosition && (headerSeen || dataSeen))
                        throw new FormatException("S0 记录只能位于首行。");
                    headerSeen = true;
                    break;

                case RecordType.S1:
                case RecordType.S2:
                case RecordType.S3:
                    ParserBlockBuilder.AddSegment(
                        segments,
                        record.Address,
                        record.Data,
                        options.DataSize,
                        lineNumber,
                        options.ValidateDataRecordLength,
                        options.DataRecordPaddingValue);
                    dataRecordCount++;
                    dataSeen = true;
                    break;

                case RecordType.S5:
                case RecordType.S6:
                    if (options.ValidateMotorolaCountRecord && record.Address != dataRecordCount)
                        throw new FormatException($"S-Record 计数记录与数据记录数量不一致，行数：{lineNumber}。");
                    break;

                case RecordType.S9:
                case RecordType.S8:
                case RecordType.S7:
                    terminationSeen = true;
                    break;
            }
        }

        if (options.RequireEndOfFile && !terminationSeen)
            throw new FormatException("Motorola S-Record 文件缺少结束记录。");

        return ParserBlockBuilder.BuildBlocks(segments, options.DataSize);
    }

    private static void ValidateRecord(RecordItemSlim record, FlashLoadOptions options, int lineNumber)
    {
        if (record.RawData.Length != record.Count + 1)
            throw new FormatException($"Motorola S-Record 记录长度与 Count 字段不一致，行数：{lineNumber}。");

        switch (record.RecordType)
        {
            case RecordType.S0:
            case RecordType.S1:
            case RecordType.S2:
            case RecordType.S3:
                break;
            case RecordType.S5:
            case RecordType.S6:
            case RecordType.S7:
            case RecordType.S8:
            case RecordType.S9:
                if (options.ValidateRecordTypeLength && !record.Data.IsEmpty)
                    throw new FormatException($"Motorola S-Record 非数据记录不能包含数据字段，行数：{lineNumber}。");
                break;
            default:
                throw new FormatException($"未知的 Motorola S-Record 记录类型：S{(byte)record.RecordType}，行数：{lineNumber}。");
        }

        if (options.ValidateChecksums && record.CheckSum != record.CalculatedCheckSum)
            throw new FormatException($"校验和验证失败，行数：{lineNumber}。");
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
}

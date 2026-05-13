using System.Buffers;

namespace DiagKit.FlashFiles.Common.Utilities;

/// <summary>
/// 解析器数据段合并工具。
/// </summary>
internal static class ParserBlockBuilder
{
    internal static void ValidateDataSize(byte dataSize)
    {
        if (dataSize == 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "地址数据大小必须大于 0。");
    }

    internal static void AddSegment(List<ParsedDataSegment> segments, ulong startAddress, ReadOnlySpan<byte> data, byte dataSize, int lineNumber)
    {
        if (data.IsEmpty)
            return;
        if (data.Length % dataSize != 0)
            throw new FormatException($"数据记录长度必须是 DataSize 的整数倍，行数：{lineNumber}。");

        segments.Add(new ParsedDataSegment(startAddress, data.ToArray(), lineNumber));
    }

    internal static List<FlashBlock> BuildBlocks(List<ParsedDataSegment> segments, byte dataSize)
    {
        if (segments.Count == 0)
            return [];

        segments.Sort(static (a, b) => a.StartAddress.CompareTo(b.StartAddress));

        var blocks = new List<FlashBlock>();
        var buffer = new ArrayBufferWriter<byte>(4096);
        var blockStartAddress = 0ul;
        var blockEndAddress = 0ul;
        var hasBlock = false;

        try
        {
            foreach (var segment in segments)
            {
                var segmentEndAddress = GetEndAddress(segment.StartAddress, segment.Data.Length, dataSize, segment.LineNumber);

                if (!hasBlock)
                {
                    blockStartAddress = segment.StartAddress;
                    blockEndAddress = segmentEndAddress;
                    buffer.Write(segment.Data);
                    hasBlock = true;
                    continue;
                }

                if (segment.StartAddress <= blockEndAddress)
                    throw new FormatException($"存在重复或重叠地址的数据，地址：0x{segment.StartAddress:X8}，行数：{segment.LineNumber}。");

                if (blockEndAddress != ulong.MaxValue && segment.StartAddress == blockEndAddress + 1)
                {
                    buffer.Write(segment.Data);
                    blockEndAddress = segmentEndAddress;
                    continue;
                }

                blocks.Add(CreateBlock(buffer, blockStartAddress, dataSize));
                buffer.Clear();
                blockStartAddress = segment.StartAddress;
                blockEndAddress = segmentEndAddress;
                buffer.Write(segment.Data);
            }

            if (hasBlock)
                blocks.Add(CreateBlock(buffer, blockStartAddress, dataSize));
        }
        catch
        {
            foreach (var block in blocks)
                block.Dispose();
            throw;
        }

        return blocks;
    }

    private static ulong GetEndAddress(ulong startAddress, int byteCount, byte dataSize, int lineNumber)
    {
        var addressCount = (ulong)(byteCount / dataSize);
        var offset = addressCount - 1;
        if (startAddress > ulong.MaxValue - offset)
            throw new FormatException($"地址范围溢出，行数：{lineNumber}。");
        return startAddress + offset;
    }

    private static FlashBlock CreateBlock(ArrayBufferWriter<byte> buffer, ulong startAddress, byte dataSize)
    {
        var block = new FlashBlock(buffer.WrittenCount, startAddress, dataSize);
        buffer.WrittenSpan.CopyTo(block.WritableData.Span);
        return block;
    }
}

internal readonly record struct ParsedDataSegment(ulong StartAddress, byte[] Data, int LineNumber);

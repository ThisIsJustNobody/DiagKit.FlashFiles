using DiagKit.FlashFiles.Define.Enumerates;

using System.Text;

namespace DiagKit.FlashFiles;

/// <summary>
/// Flash 文档，包含一个或多个连续地址的数据块。<br/>A Flash document containing one or more blocks of contiguous address data.
/// </summary>
public sealed class FlashDocument : IDisposable
{
    private bool disposedValue;
    private readonly List<FlashBlock> blocks;
    private readonly IReadOnlyList<FlashBlock> blocksView;

    #region 构造

    private FlashDocument(List<FlashBlock> blocks, byte dataSize)
    {
        if (dataSize == 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "地址数据大小必须大于 0。");

        // 按起始地址排序，确保二分查找正确
        blocks.Sort((a, b) => a.StartAddress.CompareTo(b.StartAddress));
        this.blocks = blocks;
        blocksView = blocks.AsReadOnly();
        DataSize = dataSize;

        if (blocks.Count > 0)
        {
            StartAddress = blocks[0].StartAddress;
            EndAddress = blocks[^1].EndAddress;
            var byteCount = 0ul;
            var addressCount = 0ul;
            foreach (var b in blocks)
            {
                byteCount += b.ByteCount;
                addressCount += b.AddressCount;
            }
            ByteCount = byteCount;
            AddressCount = addressCount;
        }
    }

    /// <summary>从文件路径加载 Flash 文档，根据扩展名自动检测格式。<br/>Loads a Flash document from a file path, auto-detecting the format by extension.</summary>
    public static FlashDocument Load(string filePath, byte dataSize, bool validateChecksums = true)
        => Load(filePath, new FlashLoadOptions(dataSize) { ValidateChecksums = validateChecksums });

    /// <summary>从文件路径加载 Flash 文档，根据扩展名自动检测格式。<br/>Loads a Flash document from a file path, auto-detecting the format by extension.</summary>
    public static FlashDocument Load(string filePath, FlashLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(options);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".hex" && ext != ".s19" && ext != ".s28" && ext != ".s37")
            throw new NotSupportedException($"不支持的文件格式：{ext}");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("文件不存在。", filePath);
        var blocks = ext switch
        {
            ".s19" or ".s28" or ".s37" => MotorolaSRecord.Parser.Parse(File.ReadLines(filePath), options),
            ".hex" => IntelMcs86.Parser.Parse(File.ReadLines(filePath), options),
            _ => throw new NotSupportedException($"不支持的文件格式：{ext}"),
        };
        return new FlashDocument(blocks, options.DataSize);
    }

    /// <summary>从流加载 Flash 文档，需指定格式。<br/>Loads a Flash document from a stream with a specified format.</summary>
    public static FlashDocument Load(Stream stream, FlashFileType format, byte dataSize, bool validateChecksums = true)
        => Load(stream, format, new FlashLoadOptions(dataSize) { ValidateChecksums = validateChecksums });

    /// <summary>从流加载 Flash 文档，需指定格式。<br/>Loads a Flash document from a stream with a specified format.</summary>
    public static FlashDocument Load(Stream stream, FlashFileType format, FlashLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        var blocks = format switch
        {
            FlashFileType.Intel_MCS_86 => IntelMcs86.Parser.Parse(stream, options),
            FlashFileType.Motorola_S_Record => MotorolaSRecord.Parser.Parse(stream, options),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        return new FlashDocument(blocks, options.DataSize);
    }

    /// <summary>从字节数据加载 Flash 文档，需指定格式。<br/>Loads a Flash document from byte data with a specified format.</summary>
    public static FlashDocument Load(ReadOnlySpan<byte> data, FlashFileType format, byte dataSize, bool validateChecksums = true)
        => Load(data, format, new FlashLoadOptions(dataSize) { ValidateChecksums = validateChecksums });

    /// <summary>从字节数据加载 Flash 文档，需指定格式。<br/>Loads a Flash document from byte data with a specified format.</summary>
    public static FlashDocument Load(ReadOnlySpan<byte> data, FlashFileType format, FlashLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var lines = ReadLines(data);
        var blocks = format switch
        {
            FlashFileType.Intel_MCS_86 => IntelMcs86.Parser.Parse(lines, options),
            FlashFileType.Motorola_S_Record => MotorolaSRecord.Parser.Parse(lines, options),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        return new FlashDocument(blocks, options.DataSize);
    }

    #endregion

    #region 保存

    /// <summary>将文档保存到流，需指定格式。<br/>Saves the document to a stream with a specified format.</summary>
    public void Save(Stream stream, FlashFileType format)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        ArgumentNullException.ThrowIfNull(stream);

        switch (format)
        {
            case FlashFileType.Intel_MCS_86:
                IntelMcs86.Writer.Write(stream, blocks, DataSize);
                break;
            case FlashFileType.Motorola_S_Record:
                throw new NotSupportedException("Motorola S-Record 格式的写入暂未实现。");
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    /// <summary>将文档保存到文件，根据扩展名自动检测格式。<br/>Saves the document to a file, auto-detecting the format by extension.</summary>
    public void SaveToFile(string filePath)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        ArgumentNullException.ThrowIfNull(filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var format = ext switch
        {
            ".hex" => FlashFileType.Intel_MCS_86,
            ".s19" or ".s28" or ".s37" => throw new NotSupportedException("Motorola S-Record 格式的写入暂未实现。"),
            _ => throw new NotSupportedException($"不支持的文件格式：{ext}"),
        };

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096);
        Save(fs, format);
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!disposedValue)
        {
            foreach (var b in blocks)
                b.Dispose();
            disposedValue = true;
        }
    }

    #region 属性

    /// <summary>数据块列表（只读）。<br/>The list of data blocks (read-only).</summary>
    public IReadOnlyList<FlashBlock> Blocks => blocksView;

    /// <summary>起始地址（构造时预计算）。<br/>The start address (pre-computed at construction).</summary>
    public ulong StartAddress { get; }

    /// <summary>结束地址（构造时预计算）。<br/>The end address (pre-computed at construction).</summary>
    public ulong EndAddress { get; }

    /// <summary>总字节数量。<br/>The total number of bytes.</summary>
    public ulong ByteCount { get; }

    /// <summary>总地址数量。<br/>The total number of addresses.</summary>
    public ulong AddressCount { get; }

    /// <summary>每个地址映射的字节数。<br/>The number of bytes mapped to each address.</summary>
    public byte DataSize { get; }

    #endregion

    #region 地址查找

    /// <summary>检查指定地址是否存在于文档中。O(log n) 复杂度。<br/>Checks whether the specified address exists in the document. O(log n) complexity.</summary>
    public bool ContainsAddress(ulong address)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        return FindBlockIndex(address) >= 0;
    }

    /// <summary>读取指定地址的数据。O(log n) 复杂度。<br/>Reads data at the specified address. O(log n) complexity.</summary>
    public ReadOnlySpan<byte> ReadAt(ulong address)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        var index = FindBlockIndex(address);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(address), "地址超出范围。");
        return blocks[index].ReadAt(address);
    }

    /// <summary>读取地址范围内的数据。范围必须位于同一块内。<br/>Reads data within the specified address range. The range must be within the same block.</summary>
    public ReadOnlySpan<byte> ReadRange(ulong startAddress, ulong endAddress)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        var index = FindBlockIndex(startAddress);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(startAddress), "起始地址超出范围。");
        return blocks[index].ReadRange(startAddress, endAddress);
    }

    /// <summary>写入指定地址的数据。写入长度必须等于 DataSize。<br/>Writes data at the specified address. The write length must equal DataSize.</summary>
    public void WriteAt(ulong address, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        var index = FindBlockIndex(address);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(address), "地址超出范围。");
        blocks[index].WriteAt(address, data);
    }

    /// <summary>写入地址范围内的数据。范围必须位于同一块内。<br/>Writes data within the specified address range. The range must be within the same block.</summary>
    public void WriteRange(ulong startAddress, ulong endAddress, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        var index = FindBlockIndex(startAddress);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(startAddress), "起始地址超出范围。");
        blocks[index].WriteRange(startAddress, endAddress, data);
    }

    /// <summary>
    /// 导出拥有数据副本的块 DTO 列表。<br/>Exports block DTOs that own copied data buffers.
    /// </summary>
    public IReadOnlyList<FlashBlockDto> ToBlockDtos()
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        var dtos = new List<FlashBlockDto>(blocks.Count);
        foreach (var block in blocks)
            dtos.Add(block.ToDto());
        return dtos.AsReadOnly();
    }

    /// <summary>二分查找包含指定地址的块索引，未找到返回 -1。</summary>
    private int FindBlockIndex(ulong address)
    {
        int lo = 0, hi = blocks.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            var block = blocks[mid];
            if (address < block.StartAddress)
                hi = mid - 1;
            else if (address > block.EndAddress)
                lo = mid + 1;
            else
                return mid;
        }
        return -1;
    }

    #endregion

    #region 页填充

    /// <summary>填充预分配的页。成功返回 true，无有效数据返回 false。<br/>Fills a pre-allocated page. Returns true on success, false if no valid data.</summary>
    public bool TryFillPage(FlashPage page, ulong startAddress, byte paddingValue = 0xFF)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        ArgumentNullException.ThrowIfNull(page);
        if (page.DataSize != DataSize)
            throw new ArgumentException("DataSize 不一致。", nameof(page));

        page.FillFrom(this, startAddress, paddingValue);
        return page.HasValidData;
    }

    /// <summary>
    /// 迭代创建并填充页。调用者负责处置返回的每个 FlashPage。<br/>Iterates to create and fill pages. The caller is responsible for disposing each returned FlashPage.
    /// </summary>
    public IEnumerable<FlashPage> EnumeratePages(uint addressCount, ulong startAddress, ulong endAddress, byte paddingValue = 0xFF)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        if (addressCount == 0)
            throw new ArgumentOutOfRangeException(nameof(addressCount), "页地址数量必须大于 0。");
        if (endAddress < startAddress)
            throw new ArgumentOutOfRangeException(nameof(endAddress), "结束地址不能小于起始地址。");

        var expectedCount = (endAddress - startAddress) / addressCount + 1;
        var currentAddress = startAddress;
        for (var counted = 0ul; counted < expectedCount; counted++)
        {
            var page = new FlashPage(addressCount, DataSize);
            _ = TryFillPage(page, currentAddress, paddingValue);
            yield return page;
            if (counted + 1 < expectedCount)
                currentAddress += page.AddressCount;
        }
    }

    /// <summary>
    /// 导出面向 UDS 传输的拥有数据块 DTO。<br/>Exports owned data block DTOs for UDS transfer.
    /// </summary>
    public IReadOnlyList<FlashBlockDto> ToUdsBlockDtos(FlashUdsExportOptions options)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        ArgumentNullException.ThrowIfNull(options);
        if (blocks.Count == 0)
            return Array.Empty<FlashBlockDto>();
        if (options.MaxBlockByteCount % DataSize != 0)
            throw new ArgumentException("最大块字节数必须是 DataSize 的整数倍。", nameof(options));

        var addressCount = options.MaxBlockByteCount / DataSize;
        return options.FillGaps
            ? ToUdsBlockDtosWithGaps(options, addressCount)
            : ToUdsBlockDtosWithoutGaps(options, addressCount);
    }

    private IReadOnlyList<FlashBlockDto> ToUdsBlockDtosWithGaps(FlashUdsExportOptions options, uint addressCount)
    {
        var dtos = new List<FlashBlockDto>();
        foreach (var page in EnumeratePages(addressCount, StartAddress, EndAddress, options.PaddingValue))
        {
            using (page)
            {
                if (options.SkipBlankBlocks && !page.HasValidData)
                    continue;

                var endAddress = page.StartAddress + page.AddressCount - 1;
                EnsureUInt32Address(page.StartAddress, endAddress, options);
                dtos.Add(new FlashBlockDto(page.StartAddress, page.Data, DataSize));
            }
        }

        return dtos.AsReadOnly();
    }

    private IReadOnlyList<FlashBlockDto> ToUdsBlockDtosWithoutGaps(FlashUdsExportOptions options, uint addressCount)
    {
        var dtos = new List<FlashBlockDto>();
        foreach (var block in blocks)
        {
            var remainingAddressCount = block.AddressCount;
            var currentAddress = block.StartAddress;
            var byteOffset = 0;

            while (remainingAddressCount > 0)
            {
                var chunkAddressCount = Math.Min(addressCount, remainingAddressCount);
                var chunkByteCount = checked((int)(chunkAddressCount * DataSize));
                var endAddress = currentAddress + chunkAddressCount - 1;

                EnsureUInt32Address(currentAddress, endAddress, options);
                dtos.Add(new FlashBlockDto(currentAddress, block.Data.Span.Slice(byteOffset, chunkByteCount), DataSize));

                remainingAddressCount -= chunkAddressCount;
                byteOffset += chunkByteCount;
                if (remainingAddressCount > 0)
                    currentAddress += chunkAddressCount;
            }
        }

        return dtos.AsReadOnly();
    }

    private static void EnsureUInt32Address(ulong startAddress, ulong endAddress, FlashUdsExportOptions options)
    {
        if (!options.RequireUInt32Address)
            return;
        if (startAddress > uint.MaxValue || endAddress > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options), "导出块地址必须位于 32-bit 范围内。");
    }

    private static List<string> ReadLines(ReadOnlySpan<byte> data)
    {
        Span<byte> bom = stackalloc byte[] { 0xEF, 0xBB, 0xBF };
        if (data.StartsWith(bom))
            data = data[bom.Length..];

        var lines = new List<string>();
        while (!data.IsEmpty)
        {
            var newlineIndex = data.IndexOf((byte)'\n');
            ReadOnlySpan<byte> lineBytes;
            if (newlineIndex < 0)
            {
                lineBytes = data;
                data = [];
            }
            else
            {
                lineBytes = data[..newlineIndex];
                data = data[(newlineIndex + 1)..];
            }

            if (!lineBytes.IsEmpty && lineBytes[^1] == (byte)'\r')
                lineBytes = lineBytes[..^1];
            lines.Add(Encoding.UTF8.GetString(lineBytes));
        }

        return lines;
    }

    /// <summary>
    /// 过滤页序列，跳过空白页。无有效数据时返回空序列（不抛异常）。<br/>Filters a sequence of pages, skipping blank pages. Returns an empty sequence when no valid data exists (no exception thrown).
    /// </summary>
    public static IEnumerable<FlashPage> FilterPages(IReadOnlyList<FlashPage> pages, bool skipLeadingBlank, bool skipMiddleBlank, bool skipTrailingBlank)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0 || !pages.Any(p => p.HasValidData))
            yield break;

        var firstValidIndex = -1;
        var lastValidIndex = -1;
        for (var i = 0; i < pages.Count; i++)
        {
            if (pages[i].HasValidData)
            {
                if (firstValidIndex < 0) firstValidIndex = i;
                lastValidIndex = i;
            }
        }

        for (var i = 0; i < pages.Count; i++)
        {
            if (pages[i].HasValidData)
            {
                yield return pages[i];
                continue;
            }

            if (i < firstValidIndex)
            {
                if (!skipLeadingBlank) yield return pages[i];
            }
            else if (i <= lastValidIndex)
            {
                if (!skipMiddleBlank) yield return pages[i];
            }
            else
            {
                if (skipTrailingBlank) yield break;
                yield return pages[i];
            }
        }
    }

    #endregion
}

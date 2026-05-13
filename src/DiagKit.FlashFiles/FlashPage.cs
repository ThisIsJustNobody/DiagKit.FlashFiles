using System.Buffers;

namespace DiagKit.FlashFiles;

/// <summary>
/// Flash 页，表示一个页大小的数据块，用于刷写编程。<br/>A Flash page representing a page-sized data chunk used for flash programming.
/// </summary>
public sealed class FlashPage : IDisposable
{
    private bool disposedValue;
    private readonly IMemoryOwner<byte> memoryOwner;
    private readonly Memory<byte> memory;

    /// <summary>
    /// 初始化 FlashPage 类的新实例。<br/>Initializes a new instance of the FlashPage class.
    /// </summary>
    public FlashPage(uint addressCount, byte dataSize)
    {
        if (addressCount == 0)
            throw new ArgumentOutOfRangeException(nameof(addressCount), "页地址数量必须大于 0。");
        if (dataSize == 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "地址数据大小必须大于 0。");

        var byteCount = (ulong)addressCount * dataSize;
        if (byteCount > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(addressCount), "页数据长度不能超过 Int32.MaxValue。");

        var count = (int)byteCount;
        memoryOwner = MemoryPool<byte>.Shared.Rent(count);
        memory = memoryOwner.Memory[..count];
        memory.Span.Clear();
        AddressCount = addressCount;
        DataSize = dataSize;
    }

    /// <summary>获取页中包含的地址数量。<br/>Gets the number of addresses contained in the page.</summary>
    public uint AddressCount { get; }

    /// <summary>获取每个地址映射的字节数。<br/>Gets the number of bytes mapped to each address.</summary>
    public byte DataSize { get; }

    /// <summary>获取页数据的可写范围。<br/>Gets the writable span of page data.</summary>
    public Span<byte> Data
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            return memory.Span;
        }
    }

    /// <summary>页的起始地址，由填充操作设置。<br/>The start address of the page, set by the fill operation.</summary>
    public ulong StartAddress { get; internal set; }

    /// <summary>页是否包含有效数据。<br/>Whether the page contains valid data.</summary>
    public bool HasValidData { get; internal set; }

    /// <summary>获取指定索引的地址数据。<br/>Gets the address data at the specified index.</summary>
    public Span<byte> this[Index index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            var addressOffset = index.GetOffset((int)AddressCount);
            if (addressOffset < 0 || (uint)addressOffset >= AddressCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            var offset = addressOffset * DataSize;
            return memory.Slice(offset, DataSize).Span;
        }
    }

    /// <summary>获取指定范围的地址数据。<br/>Gets the address data within the specified range.</summary>
    public Span<byte> this[Range range]
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            var (startOffset, addressLength) = range.GetOffsetAndLength((int)AddressCount);
            var offset = startOffset * DataSize;
            var length = addressLength * DataSize;
            return memory.Slice(offset, length).Span;
        }
    }

    internal void FillFrom(FlashDocument document, ulong startAddress, byte paddingValue)
    {
        Data.Fill(paddingValue);
        StartAddress = startAddress;
        HasValidData = false;
        var counted = 0;
        var currentAddress = startAddress;
        while (counted < AddressCount)
        {
            var offset = counted * DataSize;
            if (document.ContainsAddress(currentAddress))
            {
                document.ReadAt(currentAddress).CopyTo(Data.Slice(offset, DataSize));
                HasValidData = true;
            }
            counted++;
            currentAddress++;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!disposedValue)
        {
            memoryOwner.Dispose();
            disposedValue = true;
        }
    }
}

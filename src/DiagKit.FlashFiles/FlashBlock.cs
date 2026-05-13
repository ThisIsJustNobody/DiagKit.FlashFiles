using System.Buffers;

namespace DiagKit.FlashFiles;

/// <summary>
/// Flash 块，表示一段连续的地址数据。<br/>A Flash block representing a contiguous range of address data.
/// </summary>
public sealed class FlashBlock : IDisposable
{
    private bool disposedValue;
    private readonly IMemoryOwner<byte> memoryOwner;
    private readonly ReadOnlyMemory<byte> data;

    /// <summary>
    /// 初始化 FlashBlock 类的新实例。<br/>Initializes a new instance of the FlashBlock class.
    /// </summary>
    public FlashBlock(int capacity, ulong startAddress, byte dataSize)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "块数据长度必须大于 0。");
        if (dataSize == 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "地址数据大小必须大于 0。");
        if (capacity % dataSize != 0)
            throw new ArgumentException("块数据长度必须是 DataSize 的整数倍。", nameof(capacity));

        DataSize = dataSize;
        memoryOwner = MemoryPool<byte>.Shared.Rent(capacity);
        var mem = memoryOwner.Memory[..capacity];
        mem.Span.Clear();
        data = mem;
        var addressCount = (ulong)(capacity / DataSize);
        if (startAddress > ulong.MaxValue - (addressCount - 1))
            throw new ArgumentOutOfRangeException(nameof(startAddress), "块地址范围溢出。");

        StartAddress = startAddress;
        EndAddress = StartAddress + addressCount - 1;
        NextAddress = EndAddress == ulong.MaxValue ? ulong.MaxValue : EndAddress + 1;
        ByteCount = (uint)capacity;
        AddressCount = (uint)addressCount;
    }

    /// <summary>获取块的起始地址。<br/>Gets the start address of the block.</summary>
    public ulong StartAddress { get; init; }

    /// <summary>只读数据视图，对外不可变。<br/>Read-only data view, immutable externally.</summary>
    public ReadOnlyMemory<byte> Data
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            return data;
        }
    }

    /// <summary>可写数据视图，仅供解析器构造时使用。</summary>
    internal Memory<byte> WritableData
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            return memoryOwner.Memory[..(int)ByteCount];
        }
    }

    /// <summary>获取每个地址映射的字节数。<br/>Gets the number of bytes mapped to each address.</summary>
    public byte DataSize { get; init; }

    /// <summary>获取块的结束地址。<br/>Gets the end address of the block.</summary>
    public ulong EndAddress { get; init; }

    /// <summary>获取紧接在块结束地址之后的下一个地址。<br/>Gets the next address immediately following the block's end address.</summary>
    public ulong NextAddress { get; init; }

    /// <summary>Data 中包含的字节数量<br/>The number of bytes contained in Data.</summary>
    public uint ByteCount { get; init; }

    /// <summary>映射到地址的数据数量（一个地址可能映射多个字节数据）<br/>The number of addresses mapped to data (one address may map to multiple bytes of data).</summary>
    public uint AddressCount { get; init; }

    /// <summary>检查指定地址是否位于当前块范围内。<br/>Checks whether the specified address falls within the current block's range.</summary>
    public bool ContainsAddress(ulong address)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        return address >= StartAddress && address <= EndAddress;
    }

    /// <summary>读取指定地址的数据。<br/>Reads data at the specified address.</summary>
    public ReadOnlySpan<byte> ReadAt(ulong address)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        if (!ContainsAddress(address))
            throw new ArgumentOutOfRangeException(nameof(address), "地址超出范围。");
        var offset = ((int)(address - StartAddress)) * DataSize;
        return Data.Span.Slice(offset, DataSize);
    }

    /// <summary>读取地址范围内的数据。<br/>Reads data within the specified address range.</summary>
    public ReadOnlySpan<byte> ReadRange(ulong startAddress, ulong endAddress)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        if (!ContainsAddress(startAddress))
            throw new ArgumentOutOfRangeException(nameof(startAddress), "起始地址超出范围。");
        if (!ContainsAddress(endAddress))
            throw new ArgumentOutOfRangeException(nameof(endAddress), "结束地址超出范围。");
        if (endAddress < startAddress)
            throw new ArgumentOutOfRangeException(nameof(endAddress), "结束地址不能小于起始地址。");
        var length = (int)(endAddress - startAddress + 1) * DataSize;
        var offset = (int)(startAddress - StartAddress) * DataSize;
        if (length <= 0)
            return default;
        return Data.Span.Slice(offset, length);
    }

    /// <summary>写入指定地址的数据。写入长度必须等于 DataSize。<br/>Writes data at the specified address. The write length must equal DataSize.</summary>
    public void WriteAt(ulong address, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        if (!ContainsAddress(address))
            throw new ArgumentOutOfRangeException(nameof(address), "地址超出范围。");
        if (data.Length != DataSize)
            throw new ArgumentException($"数据长度必须为 {DataSize} 字节。", nameof(data));
        var offset = ((int)(address - StartAddress)) * DataSize;
        data.CopyTo(WritableData.Span.Slice(offset, DataSize));
    }

    /// <summary>写入地址范围内的数据。<br/>Writes data within the specified address range.</summary>
    public void WriteRange(ulong startAddress, ulong endAddress, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        if (!ContainsAddress(startAddress))
            throw new ArgumentOutOfRangeException(nameof(startAddress), "起始地址超出范围。");
        if (!ContainsAddress(endAddress))
            throw new ArgumentOutOfRangeException(nameof(endAddress), "结束地址超出范围。");
        if (endAddress < startAddress)
            throw new ArgumentOutOfRangeException(nameof(endAddress), "结束地址不能小于起始地址。");
        var length = (int)(endAddress - startAddress + 1) * DataSize;
        var offset = (int)(startAddress - StartAddress) * DataSize;
        if (data.Length != length)
            throw new ArgumentException($"数据长度应为 {length} 字节（地址范围 {endAddress - startAddress + 1} × DataSize {DataSize}）。", nameof(data));
        data.CopyTo(WritableData.Span.Slice(offset, length));
    }

    /// <summary>保存数据到文件。<br/>Saves data to a file.</summary>
    public string SaveToFile(string fileName)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        ArgumentNullException.ThrowIfNull(fileName);
        var dir = Path.GetDirectoryName(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);
        var newFileName = Path.GetFullPath(Path.Combine(dir ?? string.Empty, name + $".{StartAddress:X8}.{DataSize}.bin"));
        using var fs = new FileStream(newFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096);
        fs.Write(Data.Span);
        return newFileName;
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

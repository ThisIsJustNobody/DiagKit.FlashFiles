namespace DiagKit.FlashFiles;

/// <summary>
/// 拥有数据副本的 Flash 数据块 DTO。<br/>A Flash block DTO that owns a copied data buffer.
/// </summary>
public sealed class FlashBlockDto
{
    /// <summary>
    /// 初始化 FlashBlockDto 类的新实例。<br/>Initializes a new instance of the FlashBlockDto class.
    /// </summary>
    /// <param name="startAddress">块起始地址。<br/>The start address of the block.</param>
    /// <param name="data">块数据，构造时会复制。<br/>The block data, copied during construction.</param>
    /// <param name="dataSize">每个地址映射的字节数。<br/>The number of bytes mapped to each address.</param>
    public FlashBlockDto(ulong startAddress, ReadOnlySpan<byte> data, byte dataSize)
    {
        if (dataSize == 0)
            throw new ArgumentOutOfRangeException(nameof(dataSize), "地址数据大小必须大于 0。");
        if (data.IsEmpty)
            throw new ArgumentException("块数据不能为空。", nameof(data));
        if (data.Length % dataSize != 0)
            throw new ArgumentException("块数据长度必须是 DataSize 的整数倍。", nameof(data));

        var addressCount = (ulong)(data.Length / dataSize);
        if (startAddress > ulong.MaxValue - (addressCount - 1))
            throw new ArgumentOutOfRangeException(nameof(startAddress), "块地址范围溢出。");

        Data = data.ToArray();
        DataSize = dataSize;
        StartAddress = startAddress;
        EndAddress = startAddress + addressCount - 1;
        NextAddress = EndAddress == ulong.MaxValue ? ulong.MaxValue : EndAddress + 1;
        ByteCount = (uint)Data.Length;
        AddressCount = (uint)addressCount;
    }

    /// <summary>获取块的起始地址。<br/>Gets the start address of the block.</summary>
    public ulong StartAddress { get; }

    /// <summary>获取块的结束地址。<br/>Gets the end address of the block.</summary>
    public ulong EndAddress { get; }

    /// <summary>获取紧接在块结束地址之后的下一个地址。<br/>Gets the next address immediately following the block's end address.</summary>
    public ulong NextAddress { get; }

    /// <summary>获取块数据字节数。<br/>Gets the byte count of the block data.</summary>
    public uint ByteCount { get; }

    /// <summary>获取块地址数量。<br/>Gets the address count of the block.</summary>
    public uint AddressCount { get; }

    /// <summary>获取每个地址映射的字节数。<br/>Gets the number of bytes mapped to each address.</summary>
    public byte DataSize { get; }

    /// <summary>获取拥有的数据副本。<br/>Gets the owned data copy.</summary>
    public byte[] Data { get; }
}

using DiagKit.FlashFiles.Common.Utilities;

using System;
using System.Buffers;
using System.Buffers.Binary;

namespace DiagKit.FlashFiles.IntelMcs86;

/// <summary>
/// Intel HEX 记录项，用于长期保存解析后的记录数据。<br/>Represents an Intel HEX record item for long-term storage of parsed record data.
/// </summary>
/// <remarks>
/// 实例化后不应修改内部数据，否则可能影响校验和计算与数据一致性。<br/>Do not modify internal data after instantiation, as this may affect checksum calculation and data consistency.
/// </remarks>
public class RecordItem : IDisposable
{
    #region 非实例成员

    private const int DefaultBufferSize = 0xFF + 5;

    /// <summary>
    /// 计算给定原始数据的 Intel HEX 校验和（排除最后一个字节）。<br/>Calculates the Intel HEX checksum for the given raw data (excluding the last byte).
    /// </summary>
    public static byte CalculateCheckSum(ReadOnlySpan<byte> rawData)
        => ChecksumCalculator.IntelChecksum(rawData[..^1]);


    #endregion


    #region 构造

    /// <summary>
    /// 创建空的记录项实例，内部缓冲区由内存池分配。<br/>Creates an empty record item instance with internal buffers allocated from the memory pool.
    /// </summary>
    public RecordItem()
    {
        bufferMemoryOwner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
        bufferMemoryOwner.Memory.Span.Clear();
        dataMemoryOwner = MemoryPool<byte>.Shared.Rent(0xFF);
        dataMemoryOwner.Memory.Span.Clear();
    }

    /// <summary>
    /// 使用原始字节数据和可选基地址创建记录项。<br/>Creates a record item from raw byte data and an optional base address.
    /// </summary>
    /// <param name="rawData">原始记录字节数据<br/>Raw record byte data</param>
    /// <param name="baseAddress">基地址（默认为 0）<br/>Base address (defaults to 0)</param>
    public RecordItem(ReadOnlySpan<byte> rawData, ushort baseAddress = 0) : this()
    {
        Update(rawData, baseAddress);
    }

    /// <summary>
    /// 使用 HEX 文本行和基地址创建记录项。<br/>Creates a record item from a HEX text line and a base address.
    /// </summary>
    /// <param name="line">以 ':' 开头的 Intel HEX 文本行<br/>Intel HEX text line starting with ':'</param>
    /// <param name="baseAddress">基地址<br/>Base address</param>
    public RecordItem(ReadOnlySpan<char> line, ushort baseAddress) : this()
    {
        Update(line, baseAddress);
    }

    /// <summary>
    /// 使用记录类型、地址、数据和可选基地址创建记录项。<br/>Creates a record item from record type, address, data, and an optional base address.
    /// </summary>
    /// <param name="recordType">记录类型<br/>Record type</param>
    /// <param name="address">加载地址<br/>Load address</param>
    /// <param name="data">数据载荷<br/>Data payload</param>
    /// <param name="baseAddress">基地址（默认为 0）<br/>Base address (defaults to 0)</param>
    public RecordItem(RecordType recordType, ushort address, ReadOnlySpan<byte> data, ushort baseAddress = 0) : this()
    {
        Update(recordType, address, data, baseAddress);
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                bufferMemoryOwner.Dispose();
                dataMemoryOwner.Dispose();
            }

            disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion


    private bool disposedValue;

    private readonly IMemoryOwner<byte> bufferMemoryOwner;

    private readonly IMemoryOwner<byte> dataMemoryOwner;

    /// <summary>
    /// 记录的数据长度（字节数）<br/>Length of the record's data in bytes
    /// </summary>
    public byte DataLength { get; private set; }

    /// <summary>
    /// 记录的加载地址<br/>Load address of the record
    /// </summary>
    public ushort Address { get; set; }

    /// <summary>
    /// 记录类型<br/>Record type
    /// </summary>
    public RecordType RecordType { get; set; }

    /// <summary>
    /// 记录的校验和字节<br/>Checksum byte of the record
    /// </summary>
    public byte CheckSum { get; set; }

    /// <summary>
    /// 基地址（用于计算全地址的高 16 位）<br/>Base address (used as the upper 16 bits when computing the full address)
    /// </summary>
    public ushort BaseAddress { get; set; }


    #region 不含字段的属性

    /// <summary>
    /// 获取或设置完整的原始记录数据（含长度、地址、类型、数据和校验和）。<br/>Gets or sets the complete raw record data (including length, address, type, data, and checksum).
    /// </summary>
    /// <remarks>
    /// 设置时会自动解析并更新 Address、RecordType、Data 和 CheckSum。<br/>Setting this property automatically parses and updates Address, RecordType, Data, and CheckSum.
    /// </remarks>
    public ReadOnlySpan<byte> RawData
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);

            bufferMemoryOwner.Memory.Span.Clear();
            var span = bufferMemoryOwner.Memory[..RawDataLength].Span;
            span[0] = Count;
            span[1] = (byte)(Address >> 8);
            span[2] = (byte)Address;
            span[3] = (byte)RecordType;
            Data.CopyTo(span[4..]);
            span[1 + 2 + 1 + DataLength] = CheckSum;
            return span;
        }
        set
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            if (value.Length < 5)
                throw new FormatException("Intel HEX 记录长度不能小于 5 字节。");

            var count = value[0];
            var expectedLength = 1 + 2 + 1 + count + 1;
            if (value.Length != expectedLength)
                throw new FormatException($"Intel HEX 记录长度不匹配，期望 {expectedLength} 字节，实际 {value.Length} 字节。");
            Address = (ushort)((value[1] << 8) + value[2]);
            RecordType = (RecordType)value[3];
            Data = value.Slice(4, count);
            CheckSum = value[^1];
        }
    }

    /// <summary>
    /// 原始记录数据的总长度（Count + Address + RecordType + Data + CheckSum）<br/>Total length of the raw record data (Count + Address + RecordType + Data + CheckSum)
    /// </summary>
    public int RawDataLength => 1 + 2 + 1 + DataLength + 1;

    /// <summary>
    /// 记录中数据字段的字节数（等效于 DataLength）<br/>Number of bytes in the record's data field (equivalent to DataLength)
    /// </summary>
    public byte Count => DataLength;

    /// <summary>
    /// 由 BaseAddress（高 16 位）和 Address（低 16 位）组合而成的 32 位全地址。<br/>32-bit full address composed of BaseAddress (upper 16 bits) and Address (lower 16 bits).
    /// </summary>
    public ulong FullAddress
    {
        get => (((ulong)BaseAddress << 16) | Address);
        set
        {
            BaseAddress = (ushort)(value >> 16);
            Address = (ushort)value;
        }
    }

    /// <summary>
    /// 根据当前数据重新计算的校验和<br/>Checksum recalculated from the current raw data
    /// </summary>
    public byte CalculatedCheckSum => disposedValue ? throw new ObjectDisposedException(nameof(RecordItem)) : CalculateCheckSum(RawData);

    /// <summary>
    /// 记录是否有效（存储的校验和与重新计算的校验和一致）<br/>Whether the record is valid (stored checksum matches the recalculated checksum)
    /// </summary>
    public bool IsValid => CheckSum == CalculatedCheckSum;


    /// <summary>
    /// 以大端序表示的地址字段<br/>Address field in big-endian byte order
    /// </summary>
    public ReadOnlySpan<byte> AddressBigEndian => RawData[1..3];

    /// <summary>
    /// 获取或设置记录的数据载荷。<br/>Gets or sets the record's data payload.
    /// </summary>
    /// <remarks>
    /// 设置数据时会将其复制到内部缓冲区并更新 DataLength。数据长度不能超过 255 字节。<br/>Setting data copies it to the internal buffer and updates DataLength. Data length cannot exceed 255 bytes.
    /// </remarks>
    public ReadOnlySpan<byte> Data
    {
        get => disposedValue ? throw new ObjectDisposedException(nameof(RecordItem)) : dataMemoryOwner.Memory[..DataLength].Span;
        set
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            if (value.Length > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Intel HEX 单条记录数据长度不能超过 255 字节。");

            dataMemoryOwner.Memory.Span.Clear();
            value.CopyTo(dataMemoryOwner.Memory.Span);
            DataLength = (byte)value.Length;
        }
    }

    #endregion

    /// <summary>
    /// 使用原始字节数据更新记录内容。<br/>Updates the record content using raw byte data.
    /// </summary>
    /// <param name="rawData">原始记录字节数据<br/>Raw record byte data</param>
    /// <param name="baseAddress">基地址（默认为 0）<br/>Base address (defaults to 0)</param>
    public void Update(ReadOnlySpan<byte> rawData, ushort baseAddress = 0)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        
        RawData = rawData;
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// 使用 HEX 文本行更新记录内容。<br/>Updates the record content using a HEX text line.
    /// </summary>
    /// <param name="line">以 ':' 开头的 Intel HEX 文本行<br/>Intel HEX text line starting with ':'</param>
    /// <param name="baseAddress">基地址（默认为 0）<br/>Base address (defaults to 0)</param>
    public void Update(ReadOnlySpan<char> line, ushort baseAddress = 0)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        
        using var mo = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
        mo.Memory.Span.Clear();
        var slim = new RecordItemSlim(line, mo.Memory.Span, baseAddress);
        RawData = slim.RawData;
        BaseAddress = slim.BaseAddress;
    }

    /// <summary>
    /// 使用记录类型、地址、数据和可选基地址更新记录内容。<br/>Updates the record content using record type, address, data, and an optional base address.
    /// </summary>
    /// <param name="recordType">记录类型<br/>Record type</param>
    /// <param name="address">加载地址<br/>Load address</param>
    /// <param name="data">数据载荷<br/>Data payload</param>
    /// <param name="baseAddress">基地址（默认为 0）<br/>Base address (defaults to 0)</param>
    public void Update(RecordType recordType, ushort address, ReadOnlySpan<byte> data, ushort baseAddress = 0)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        
        RecordType = recordType;
        Address = address;
        Data = data;
        CheckSum = CalculateCheckSum(RawData);
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// 尝试从数据字段中读取大端序基地址。<br/>Attempts to read a big-endian base address from the data field.
    /// </summary>
    /// <param name="baseAddress">读取到的基地址（若成功）<br/>The base address read (if successful)</param>
    /// <returns>数据长度足够时返回 <c>true</c>。<br/><c>true</c> if the data is long enough; otherwise, <c>false</c>.</returns>
    public bool TryReadBaseAddress(out ushort baseAddress)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
        
        return BinaryPrimitives.TryReadUInt16BigEndian(Data, out baseAddress);
    }
}

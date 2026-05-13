using DiagKit.FlashFiles.Common.Utilities;

using System;
using System.Buffers.Binary;

namespace DiagKit.FlashFiles.IntelMcs86;

/// <summary>
/// Intel HEX 记录项的轻量级栈分配表示，用于高性能零分配解析。<br/>A lightweight stack-only representation of an Intel HEX record item for high-performance zero-allocation parsing.
/// </summary>
public ref struct RecordItemSlim
{
    #region 非实例成员

    /// <summary>
    /// 计算给定原始数据的 Intel HEX 校验和（排除最后一个字节）。<br/>Calculates the Intel HEX checksum for the given raw data (excluding the last byte).
    /// </summary>
    public static byte CalculateCheckSum(ReadOnlySpan<byte> rawData)
        => ChecksumCalculator.IntelChecksum(rawData[..^1]);


    #endregion

    /// <summary>
    /// 存储原始记录数据所需的最小缓冲区大小（最大数据 255 + 5 字节开销）。<br/>Minimum buffer size required to store raw record data (max 255 data bytes + 5 bytes overhead).
    /// </summary>
    public const int DefaultBufferSize = 0xFF + 5;

    private const int MinLength = 11;

    /// <summary>
    /// 从 HEX 文本行解析记录，使用预分配的缓冲区存储原始字节数据。<br/>Parses a record from a HEX text line, using a pre-allocated buffer for raw byte storage.
    /// </summary>
    /// <param name="line">以 ':' 开头的 Intel HEX 文本行<br/>Intel HEX text line starting with ':'</param>
    /// <param name="buffer">用于存储解析后二进制数据的缓冲区<br/>Buffer for storing parsed binary data</param>
    /// <param name="baseAddress">基地址（默认为 0）<br/>Base address (defaults to 0)</param>
    public RecordItemSlim(ReadOnlySpan<char> line, Span<byte> buffer, ushort baseAddress = 0)
    {
        if (line.IsEmpty)
            throw new ArgumentException("Record line cannot be empty.", nameof(line));
        if (buffer.IsEmpty)
            throw new ArgumentException("Buffer cannot be empty.", nameof(buffer));
        var trimmed = line.Trim();
        if (trimmed.Length < MinLength)
            throw new FormatException($"Record line is too short (actual: {trimmed.Length}, minimum: {MinLength}）.");
        if (trimmed[0] != ':')
            throw new FormatException($"Record line must start with ':' (actual: '{trimmed[0]}').");
        var rawDataLength = (trimmed.Length - 1) / 2;
        ByteConverter.FromHexString(trimmed[1..], buffer);
        RawData = buffer[..rawDataLength];
        Count = RawData[0];
        BaseAddress = baseAddress;
        AddressBigEndian = RawData[1..3];
        Address = BinaryPrimitives.ReadUInt16BigEndian(AddressBigEndian);
        FullAddress = ((ulong)BaseAddress << 16) | Address;
        RecordType = (RecordType)RawData[3];
        Data = RawData[4..^1];
        CheckSum = RawData[^1];
    }

    /// <summary>
    /// 从原始字节数据创建记录项，直接引用原始跨度而不复制。<br/>Creates a record item from raw byte data, directly referencing the original span without copying.
    /// </summary>
    /// <param name="rawData">原始记录字节数据<br/>Raw record byte data</param>
    /// <param name="baseAddress">基地址（默认为 0）<br/>Base address (defaults to 0)</param>
    public RecordItemSlim(ReadOnlySpan<byte> rawData, ushort baseAddress = 0)
    {
        RawData = rawData;
        Count = RawData[0];
        BaseAddress = baseAddress;
        AddressBigEndian = RawData[1..3];
        Address = BinaryPrimitives.ReadUInt16BigEndian(AddressBigEndian);
        FullAddress = ((ulong)BaseAddress << 16) | Address;
        RecordType = (RecordType)RawData[3];
        Data = RawData[4..^1];
        CheckSum = RawData[^1];
    }

    /// <summary>
    /// 完整的原始记录数据<br/>Complete raw record data
    /// </summary>
    public readonly ReadOnlySpan<byte> RawData { get; }

    /// <summary>
    /// 记录中数据字段的字节数<br/>Number of bytes in the record's data field
    /// </summary>
    public readonly byte Count { get; }

    /// <summary>
    /// 基地址（用于计算全地址的高 16 位）<br/>Base address (used as the upper 16 bits when computing the full address)
    /// </summary>
    public readonly ushort BaseAddress { get; }

    /// <summary>
    /// 以大端序表示的地址字段<br/>Address field in big-endian byte order
    /// </summary>
    public readonly ReadOnlySpan<byte> AddressBigEndian { get; }

    /// <summary>
    /// 记录的加载地址<br/>Load address of the record
    /// </summary>
    public readonly ushort Address { get; }

    /// <summary>
    /// 由 BaseAddress（高 16 位）和 Address（低 16 位）组合而成的 32 位全地址。<br/>32-bit full address composed of BaseAddress (upper 16 bits) and Address (lower 16 bits).
    /// </summary>
    public readonly ulong FullAddress { get; }

    /// <summary>
    /// 记录类型<br/>Record type
    /// </summary>
    public readonly RecordType RecordType { get; }

    /// <summary>
    /// 记录的数据载荷（不包含地址、类型和校验和）<br/>Data payload of the record (excluding address, type, and checksum)
    /// </summary>
    public readonly ReadOnlySpan<byte> Data { get; }

    /// <summary>
    /// 记录的校验和字节<br/>Checksum byte of the record
    /// </summary>
    public readonly byte CheckSum { get; }

    private bool isCalculated;

    private byte calculatedCheckSum;

    /// <summary>
    /// 根据当前数据重新计算的校验和（惰性计算，仅计算一次）<br/>Checksum recalculated from the current raw data (lazy, computed only once)
    /// </summary>
    public byte CalculatedCheckSum
    {
        get
        {
            if (!isCalculated)
            {
                calculatedCheckSum = CalculateCheckSum(RawData);
                isCalculated = true;
            }
            return calculatedCheckSum;
        }
    }

    /// <summary>
    /// 记录是否有效（原始数据长度正确、数据长度匹配且校验和一致）<br/>Whether the record is valid (raw data length correct, data length matching, and checksum consistent)
    /// </summary>
    public bool IsValid => RawData.Length == Count + 5 && Count == Data.Length && CheckSum == CalculatedCheckSum;

    /// <summary>
    /// 尝试从数据字段中读取大端序基地址。<br/>Attempts to read a big-endian base address from the data field.
    /// </summary>
    /// <param name="baseAddress">读取到的基地址（若成功）<br/>The base address read (if successful)</param>
    /// <returns>数据长度足够时返回 <c>true</c>。<br/><c>true</c> if the data is long enough; otherwise, <c>false</c>.</returns>
    public readonly bool TryReadBaseAddress(out ushort baseAddress)
    {
        return BinaryPrimitives.TryReadUInt16BigEndian(Data, out baseAddress);
    }
}

using DiagKit.FlashFiles.Common.Utilities;

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace DiagKit.FlashFiles.MotorolaSRecord;

/// <summary>
/// 轻量级单行 Record，仅限栈上使用<br/>Lightweight single-line record, stack-only usage
/// </summary>
/// <remarks>
/// 极致性能，零拷贝，但不可被长期持有。<br/>Extreme performance, zero-copy, but cannot be held long-term.
/// </remarks>
[DebuggerDisplay("Type = {RecordType}, Address = 0x{Address:X8}, DataLength = {Data.Length}")]
public ref struct RecordItemSlim
{
    #region 非实例成员

    /// <summary>
    /// 默认缓冲区大小（256 字节）<br/>Default buffer size (256 bytes)
    /// </summary>
    public const int DefaultBufferSize = 0xFF + 1;

    private const int MinLength = 10;

    private static ulong GetAddress(RecordType recordType, ReadOnlySpan<byte> rawData) =>
        recordType switch
        {
            RecordType.S0 => BinaryPrimitives.ReadUInt16BigEndian(rawData[1..]),
            RecordType.S1 or RecordType.S5 or RecordType.S9 => BinaryPrimitives.ReadUInt16BigEndian(
                rawData[1..]
            ),
            RecordType.S2 or RecordType.S6 or RecordType.S8 => (uint)(rawData[1] << 16 | rawData[2] << 8 | rawData[3]),
            RecordType.S3 or RecordType.S7 => BinaryPrimitives.ReadUInt32BigEndian(
                rawData[1..]
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(recordType)),
        };

    private static (byte addressStartIndex, byte addressLength, byte dataStartIndex) GetInfo(RecordType recordType) => recordType switch
    {
        RecordType.S0 or RecordType.S1 or RecordType.S5 or RecordType.S9 => (1, 2, 3),
        RecordType.S2 or RecordType.S6 or RecordType.S8 => (1, 3, 4),
        RecordType.S3 or RecordType.S7 => (1, 4, 5),
        _ => throw new ArgumentOutOfRangeException($"Unknown record type \"{recordType}\"."),
    };

    private static byte CalculateCheckSum(ReadOnlySpan<byte> rawData)
        => ChecksumCalculator.MotorolaChecksum(rawData);

    #endregion


    /// <summary>
    /// 从文本行初始化 <see cref="RecordItemSlim"/> 的新实例<br/>Initializes a new instance of <see cref="RecordItemSlim"/> from a text line
    /// </summary>
    /// <param name="line">Motorola S-Record 文本行<br/>Motorola S-Record text line</param>
    /// <param name="buffer">用于存储解析后二进制数据的缓冲区<br/>Buffer to store the parsed binary data</param>
    public RecordItemSlim(ReadOnlySpan<char> line, Span<byte> buffer)
    {
        if (line.IsEmpty)
            throw new ArgumentException("Record line cannot be empty.", nameof(line));
        if (buffer.IsEmpty)
            throw new ArgumentException("Buffer cannot be empty.", nameof(buffer));
        var trimmed = line.Trim();
        if (trimmed.Length < MinLength)
            throw new FormatException($"Record line is too short (actual: {trimmed.Length}, minimum: {MinLength}).");
        if (trimmed[0] != 'S')
            throw new FormatException($"Record line must start with 'S' (actual: '{trimmed[0]}').");
        RecordType = trimmed[1] switch
        {
            '0' => RecordType.S0,
            '1' => RecordType.S1,
            '2' => RecordType.S2,
            '3' => RecordType.S3,
            '5' => RecordType.S5,
            '6' => RecordType.S6,
            '7' => RecordType.S7,
            '8' => RecordType.S8,
            '9' => RecordType.S9,
            _ => throw new FormatException($"Unknown record type \"S{trimmed[1]}\" (line: {trimmed})"),
        };

        var (_, addressLength, dataStartIndex) = GetInfo(RecordType);

        var rawDataLength = (trimmed.Length - 2) / 2;
        ByteConverter.FromHexString(trimmed[2..], buffer);
        RawData = buffer[..rawDataLength];

        Count = RawData[0];
        AddressBigEndian = RawData.Slice(1, addressLength);
        Address = GetAddress(RecordType, RawData);
        Data = RawData.Slice(dataStartIndex, Count - addressLength - 1);
        CheckSum = RawData[^1];
        CalculatedCheckSum = CalculateCheckSum(RawData);
        IsValid = (Count == AddressBigEndian.Length + Data.Length + 1)
            && (RawData.Length == Count + 1)
            && (CheckSum == CalculatedCheckSum);
    }

    /// <summary>
    /// 使用指定的记录类型和原始数据初始化 <see cref="RecordItemSlim"/> 的新实例<br/>Initializes a new instance of <see cref="RecordItemSlim"/> with the specified record type and raw data
    /// </summary>
    /// <param name="recordType">记录类型<br/>Record type</param>
    /// <param name="rawData">原始数据<br/>Raw data</param>
    public RecordItemSlim(RecordType recordType, ReadOnlySpan<byte> rawData)
    {
        RecordType = recordType;
        RawData = rawData;

        var (addressStartIndex, addressLength, dataStartIndex) = GetInfo(RecordType);

        Count = RawData[0];
        AddressBigEndian = RawData.Slice(addressStartIndex, addressLength);
        Address = GetAddress(RecordType, RawData);
        Data = RawData.Slice(dataStartIndex, Count - addressLength - 1);
        CheckSum = RawData[^1];
        CalculatedCheckSum = CalculateCheckSum(RawData);
        IsValid = (Count == AddressBigEndian.Length + Data.Length + 1)
            && (RawData.Length == Count + 1)
            && (CheckSum == CalculatedCheckSum);
    }

    /// <summary>
    /// 记录类型<br/>Record type
    /// </summary>
    public readonly RecordType RecordType { get; }

    /// <summary>
    /// 原始二进制数据<br/>Raw binary data
    /// </summary>
    public readonly ReadOnlySpan<byte> RawData { get; }

    /// <summary>
    /// 计数字段的值<br/>Count field value
    /// </summary>
    public readonly byte Count { get; }

    /// <summary>
    /// 大端序格式的地址字节<br/>Address bytes in big-endian format
    /// </summary>
    public readonly ReadOnlySpan<byte> AddressBigEndian { get; }

    /// <summary>
    /// 解析后的地址值<br/>Parsed address value
    /// </summary>
    public readonly ulong Address { get; }

    /// <summary>
    /// 数据内容<br/>Data content
    /// </summary>
    public readonly ReadOnlySpan<byte> Data { get; }

    /// <summary>
    /// 校验和字节<br/>Checksum byte
    /// </summary>
    public readonly byte CheckSum { get; }

    /// <summary>
    /// 计算得到的校验和<br/>Calculated checksum value
    /// </summary>
    public readonly byte CalculatedCheckSum { get; }

    /// <summary>
    /// 记录是否有效<br/>Whether the record is valid
    /// </summary>
    public readonly bool IsValid { get; }

    /// <summary>
    /// 将记录转换为 Motorola S-Record 文本行<br/>Converts the record to a Motorola S-Record text line
    /// </summary>
    /// <returns>S-Record 格式的文本行<br/>S-Record formatted text line</returns>
    public readonly string ToRecordLine()
    {
        var sb = new StringBuilder();
        sb.Append('S');
        sb.Append((byte)RecordType);
        sb.Append(Convert.ToHexString(RawData));
        return sb.ToString();
    }
}

using DiagKit.FlashFiles.Common.Utilities;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace DiagKit.FlashFiles.MotorolaSRecord;

/// <summary>
/// 单行 Record，可长期持有，有较大性能损耗。<br/>Single-line record, can be held long-term, with significant performance overhead.
/// </summary>
public class RecordItem : IDisposable
{
    #region 非实例成员

    /// <summary>
    /// 默认缓冲区大小（256 字节）<br/>Default buffer size (256 bytes)
    /// </summary>
    public const int DefaultBufferSize = 0xFF + 1;

    private const int MinLength = 10;

    private static (byte addressStartIndex, byte addressLength, byte dataStartIndex) GetInfo(RecordType recordType) => recordType switch
    {
        RecordType.S0 or RecordType.S1 or RecordType.S5 or RecordType.S9 => (1, 2, 3),
        RecordType.S2 or RecordType.S6 or RecordType.S8 => (1, 3, 4),
        RecordType.S3 or RecordType.S7 => (1, 4, 5),
        _ => throw new ArgumentOutOfRangeException($"Unknown record type \"{recordType}\"."),
    };

    /// <summary>
    /// 计算 Motorola S-Record 原始数据的校验和<br/>Calculates the checksum for Motorola S-Record raw data
    /// </summary>
    /// <param name="rawData">原始数据<br/>Raw data</param>
    /// <returns>校验和字节<br/>Checksum byte</returns>
    public static byte CalculateCheckSum(ReadOnlySpan<byte> rawData)
        => ChecksumCalculator.MotorolaChecksum(rawData);

    /// <summary>
    /// 将记录类型和原始数据转换为 Motorola S-Record 文本行<br/>Converts a record type and raw data into a Motorola S-Record text line
    /// </summary>
    /// <param name="recordType">记录类型<br/>Record type</param>
    /// <param name="rawData">原始数据<br/>Raw data</param>
    /// <returns>S-Record 格式的文本行<br/>S-Record formatted text line</returns>
    public static string ToRecordLine(RecordType recordType, ReadOnlySpan<byte> rawData)
    {
        var sb = new StringBuilder();
        sb.Append('S');
        sb.Append((byte)recordType);
        sb.Append(Convert.ToHexString(rawData));
        return sb.ToString();
    }


    #endregion


    #region 构造

    /// <summary>
    /// 初始化 <see cref="RecordItem"/> 类的新实例，分配默认大小的缓冲区<br/>Initializes a new instance of the <see cref="RecordItem"/> class with default-sized buffers
    /// </summary>
    public RecordItem()
    {
        bufferMemoryOwner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
        bufferMemoryOwner.Memory.Span.Clear();
        dataMemoryOwner = MemoryPool<byte>.Shared.Rent(0xFF);
        dataMemoryOwner.Memory.Span.Clear();
    }

    /// <summary>
    /// 从文本行初始化 <see cref="RecordItem"/> 类的新实例<br/>Initializes a new instance of the <see cref="RecordItem"/> class from a text line
    /// </summary>
    /// <param name="line">Motorola S-Record 文本行<br/>Motorola S-Record text line</param>
    public RecordItem(ReadOnlySpan<char> line) : this()
    {
        Update(line);
    }

    /// <summary>
    /// 使用指定的记录类型、地址和数据初始化 <see cref="RecordItem"/> 类的新实例<br/>Initializes a new instance of the <see cref="RecordItem"/> class with the specified record type, address, and data
    /// </summary>
    /// <param name="recordType">记录类型<br/>Record type</param>
    /// <param name="address">地址值<br/>Address value</param>
    /// <param name="data">数据内容<br/>Data content</param>
    public RecordItem(RecordType recordType, ulong address, ReadOnlySpan<byte> data) : this()
    {
        Update(recordType, address, data);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue) return;

        if (disposing)
        {
            bufferMemoryOwner.Dispose();
            dataMemoryOwner.Dispose();
        }

        disposedValue = true;
    }

    #endregion


    private bool disposedValue = false;
    private ulong address;

    /// <summary>
    /// 缓存临时数据
    /// </summary>
    private readonly IMemoryOwner<byte> bufferMemoryOwner;

    /// <summary>
    /// 保存数据
    /// </summary>
    private readonly IMemoryOwner<byte> dataMemoryOwner;

    /// <summary>
    /// 获取或设置记录类型<br/>Gets or sets the record type
    /// </summary>
    public RecordType RecordType { get; set; }

    /// <summary>
    /// 获取或设置地址值<br/>Gets or sets the address value
    /// </summary>
    /// <remarks>
    /// 获取时根据 <see cref="RecordType"/> 的不同返回不同位宽的地址值：S0/S1/S5/S9 返回 16 位，S2/S6/S8 返回 24 位，S3/S7 返回 32 位。<br/>When getting, returns a different bit-width address value based on <see cref="RecordType"/>: S0/S1/S5/S9 returns 16-bit, S2/S6/S8 returns 24-bit, S3/S7 returns 32-bit.
    /// </remarks>
    public ulong Address
    {
        get
        {
            return RecordType switch
            {
                RecordType.S0 or RecordType.S1 or RecordType.S5 or RecordType.S9 => address & 0xFFFF,
                RecordType.S2 or RecordType.S6 or RecordType.S8 => address & 0xFFFFFF,
                RecordType.S3 or RecordType.S7 => address,
                _ => throw new ArgumentOutOfRangeException(nameof(Address))
            };
        }
        set => address = value;
    }

    /// <summary>
    /// 获取或设置校验和字节<br/>Gets or sets the checksum byte
    /// </summary>
    public byte CheckSum { get; set; }

    #region 不含字段的属性

    /// <summary>
    /// 获取或设置原始二进制数据<br/>Gets or sets the raw binary data
    /// </summary>
    /// <remarks>
    /// 设置时从二进制数据中解析出地址、数据和校验和字段。<br/>When setting, parses the address, data, and checksum fields from the binary data.
    /// </remarks>
    public ReadOnlySpan<byte> RawData
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);

            bufferMemoryOwner.Memory.Span.Clear();
            var span = bufferMemoryOwner.Memory[..RawDataLength].Span;
            span[0] = Count;
            var (addressStartIndex, addressLength, dataStartIndex) = GetInfo(RecordType);
            for (var i = 0; i < addressLength; i++)
            {
                span[addressStartIndex + i] = (byte)(Address >> ((addressLength - i - 1) * 8));
            }
            Data.CopyTo(span.Slice(dataStartIndex, DataLength));
            span[^1] = CheckSum;
            return span;
        }
        set
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            if (value.Length < 4)
                throw new FormatException("Motorola S-Record 记录长度过短。");

            bufferMemoryOwner.Memory.Span.Clear();
            var (addressStartIndex, addressLength, dataStartIndex) = GetInfo(RecordType);
            var dataCount = (value[0] - addressLength - 1);
            var expectedLength = value[0] + 1;
            if (value.Length != expectedLength)
                throw new FormatException($"Motorola S-Record 记录长度不匹配，期望 {expectedLength} 字节，实际 {value.Length} 字节。");
            if (dataCount < 0)
                throw new FormatException("Motorola S-Record 记录长度小于地址和校验和字段长度。");
            var address = 0ul;
            for (var i = 0; i < addressLength; i++)
            {
                var temp = (ulong)value[addressStartIndex + i];  // 必须先转为 ulong 再进行位运算
                var shift = (addressLength - i - 1) * 8;
                address |= temp << shift;
            }
            Address = address;
            Data = value.Slice(dataStartIndex, dataCount);
            CheckSum = value[^1];
        }
    }

    /// <summary>
    /// 获取记录中地址字段的起始索引<br/>Gets the starting index of the address field in the record
    /// </summary>
    public byte AddressStartIndex => GetInfo(RecordType).addressStartIndex;

    /// <summary>
    /// 获取地址字段的字节长度<br/>Gets the byte length of the address field
    /// </summary>
    public byte AddressLength => GetInfo(RecordType).addressLength;

    /// <summary>
    /// 获取记录中数据字段的起始索引<br/>Gets the starting index of the data field in the record
    /// </summary>
    public byte DataStartIndex => GetInfo(RecordType).dataStartIndex;

    /// <summary>
    /// 获取计数字段的值（地址长度 + 数据长度 + 1）<br/>Gets the count field value (address length + data length + 1)
    /// </summary>
    public byte Count => (byte)(AddressLength + DataLength + 1);

    /// <summary>
    /// 获取完整的未掩码地址值<br/>Gets the full unmasked address value
    /// </summary>
    public ulong FullAddress => Address;

    /// <summary>
    /// 获取原始数据的总长度（Count + 1）<br/>Gets the total length of the raw data (Count + 1)
    /// </summary>
    public int RawDataLength => Count + 1;

    /// <summary>
    /// 获取计算得到的校验和<br/>Gets the calculated checksum value
    /// </summary>
    public byte CalculatedCheckSum => disposedValue ? throw new ObjectDisposedException(nameof(RecordItem)) : CalculateCheckSum(RawData);

    /// <summary>
    /// 获取一个值，指示记录是否有效（计数字段匹配且校验和正确）<br/>Gets a value indicating whether the record is valid (count field matches and checksum is correct)
    /// </summary>
    public bool IsValid => (Count == AddressLength + Data.Length + 1) && (CheckSum == CalculateCheckSum(RawData));

    /// <summary>
    /// 获取大端序格式的地址字节<br/>Gets the address bytes in big-endian format
    /// </summary>
    public ReadOnlySpan<byte> AddressBigEndian => disposedValue ? throw new ObjectDisposedException(nameof(RecordItem)) : RawData.Slice(AddressStartIndex, AddressLength);

    /// <summary>
    /// 获取或设置记录的数据内容<br/>Gets or sets the data content of the record
    /// </summary>
    /// <remarks>
    /// 设置时数据长度加上地址长度和校验和不能超过 255 字节。<br/>When setting, the data length plus address length and checksum cannot exceed 255 bytes.
    /// </remarks>
    public ReadOnlySpan<byte> Data
    {
        get => disposedValue ? throw new ObjectDisposedException(nameof(RecordItem)) : dataMemoryOwner.Memory[..DataLength].Span;
        set
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            if (AddressLength + value.Length + 1 > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Motorola S-Record 单条记录长度不能超过 255 字节。");

            dataMemoryOwner.Memory.Span.Clear();
            value.CopyTo(dataMemoryOwner.Memory.Span);
            DataLength = (byte)value.Length;
        }
    }

    /// <summary>
    /// 获取数据字段的字节长度<br/>Gets the byte length of the data field
    /// </summary>
    public byte DataLength { get; private set; }

    /// <summary>
    /// 获取记录的文本行表示<br/>Gets the text line representation of the record
    /// </summary>
    public string Text => ToRecordLine(RecordType, RawData);

    /// <summary>
    /// 获取数据的 UTF-8 字符串表示<br/>Gets the UTF-8 string representation of the data
    /// </summary>
    public string Title => Encoding.UTF8.GetString(Data);

    #endregion

    /// <summary>
    /// 从文本行更新记录内容<br/>Updates the record content from a text line
    /// </summary>
    /// <param name="line">Motorola S-Record 文本行<br/>Motorola S-Record text line</param>
    public void Update(ReadOnlySpan<char> line)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);

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

        using var mo = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
        mo.Memory.Span.Clear();
        ByteConverter.FromHexString(trimmed[2..], mo.Memory.Span);

        var rawDataLength = (trimmed.Length - 2) / 2;
        // S* 后面的所有字节
        var rawData = mo.Memory[..rawDataLength].Span;
        RawData = rawData;
    }

    /// <summary>
    /// 使用指定的记录类型和原始数据更新记录<br/>Updates the record with the specified record type and raw data
    /// </summary>
    /// <param name="recordType">记录类型<br/>Record type</param>
    /// <param name="rawData">原始数据<br/>Raw data</param>
    public void Update(RecordType recordType, ReadOnlySpan<byte> rawData)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);

        RecordType = recordType;
        RawData = rawData;
    }

    /// <summary>
    /// 使用指定的记录类型、地址和数据更新记录<br/>Updates the record with the specified record type, address, and data
    /// </summary>
    /// <param name="recordType">记录类型<br/>Record type</param>
    /// <param name="address">地址值<br/>Address value</param>
    /// <param name="data">数据内容<br/>Data content</param>
    public void Update(RecordType recordType, ulong address, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);

        RecordType = recordType;
        Address = address;
        Data = data;
        CheckSum = CalculatedCheckSum;
    }
}

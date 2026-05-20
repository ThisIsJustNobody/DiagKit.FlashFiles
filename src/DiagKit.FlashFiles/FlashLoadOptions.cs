namespace DiagKit.FlashFiles;

/// <summary>
/// Flash 文件加载选项。<br/>Options used when loading a Flash file.
/// </summary>
public sealed class FlashLoadOptions
{
    private byte dataSize;

    /// <summary>
    /// 使用指定的地址数据大小创建加载选项。<br/>Creates load options with the specified address data size.
    /// </summary>
    /// <param name="dataSize">每个地址映射的字节数。<br/>The number of bytes mapped to each address.</param>
    public FlashLoadOptions(byte dataSize)
    {
        DataSize = dataSize;
    }

    /// <summary>
    /// 每个地址映射的字节数。<br/>The number of bytes mapped to each address.
    /// </summary>
    public byte DataSize
    {
        get => dataSize;
        set
        {
            if (value == 0)
                throw new ArgumentOutOfRangeException(nameof(value), "地址数据大小必须大于 0。");
            dataSize = value;
        }
    }

    /// <summary>
    /// 是否验证记录校验和。<br/>Whether record checksums are validated.
    /// </summary>
    public bool ValidateChecksums { get; set; } = true;

    /// <summary>
    /// 是否要求文件包含终止记录。<br/>Whether an end-of-file or termination record is required.
    /// </summary>
    public bool RequireEndOfFile { get; set; } = true;

    /// <summary>
    /// 终止记录之后有效记录的处理方式。<br/>How valid records after an end-of-file or termination record are handled.
    /// </summary>
    public RecordsAfterEndOfFileBehavior RecordsAfterEndOfFileBehavior { get; set; } = RecordsAfterEndOfFileBehavior.Reject;

    /// <summary>
    /// 是否验证 Intel HEX 非数据记录的地址字段必须为 0。<br/>Whether Intel HEX non-data record address fields must be zero.
    /// </summary>
    public bool ValidateNonDataRecordAddress { get; set; } = true;

    /// <summary>
    /// 是否验证记录类型要求的固定数据长度。<br/>Whether fixed data lengths required by record types are validated.
    /// </summary>
    public bool ValidateRecordTypeLength { get; set; } = true;

    /// <summary>
    /// 是否验证数据记录长度必须是 <see cref="DataSize"/> 的整数倍。<br/>Whether data record length must be a multiple of <see cref="DataSize"/>.
    /// </summary>
    public bool ValidateDataRecordLength { get; set; } = true;

    /// <summary>
    /// 数据记录长度不对齐且允许补齐时使用的填充值。<br/>Padding value used when unaligned data records are allowed and padded.
    /// </summary>
    public byte DataRecordPaddingValue { get; set; } = 0xFF;

    /// <summary>
    /// 是否验证 Motorola S-Record 的 S0 记录只能位于首行。<br/>Whether the Motorola S-Record S0 header must appear before data records.
    /// </summary>
    public bool ValidateMotorolaHeaderPosition { get; set; } = true;

    /// <summary>
    /// 是否验证 Motorola S-Record 的 S5/S6 计数记录。<br/>Whether Motorola S-Record S5/S6 count records are validated.
    /// </summary>
    public bool ValidateMotorolaCountRecord { get; set; } = true;
}

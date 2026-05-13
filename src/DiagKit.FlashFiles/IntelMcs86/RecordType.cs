namespace DiagKit.FlashFiles.IntelMcs86;

/// <summary>
/// Intel MCS-86 HEX 记录类型<br/>Intel MCS-86 HEX record types
/// </summary>
public enum RecordType : byte
{
    /// <summary>
    /// 数据记录<br/>Data record
    /// </summary>
    DataRecord = 0,

    /// <summary>
    /// 文件结束记录<br/>End of file record
    /// </summary>
    EndOfFileRecord = 1,

    /// <summary>
    /// 扩展段地址记录<br/>Extended segment address record
    /// </summary>
    ExtendedSegmentAddressRecord = 2,

    /// <summary>
    /// 起始段地址记录<br/>Start segment address record
    /// </summary>
    StartSegmentAddressRecord = 3,

    /// <summary>
    /// 扩展线性地址记录<br/>Extended linear address record
    /// </summary>
    ExtendedLinearAddressRecord = 4,

    /// <summary>
    /// 起始线性地址记录<br/>Start linear address record
    /// </summary>
    StartLinearAddressRecord = 5,
}

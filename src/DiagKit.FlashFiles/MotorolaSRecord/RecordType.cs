namespace DiagKit.FlashFiles.MotorolaSRecord;

/// <summary>
/// Motorola S-Record 记录类型<br/>Motorola S-Record record types
/// </summary>
public enum RecordType : byte
{
    /// <summary>
    /// 头部记录（文件标识信息）<br/>Header record (file identification information)
    /// </summary>
    S0 = 0,

    /// <summary>
    /// 16 位地址数据记录<br/>16-bit address data record
    /// </summary>
    S1 = 1,

    /// <summary>
    /// 24 位地址数据记录<br/>24-bit address data record
    /// </summary>
    S2 = 2,

    /// <summary>
    /// 32 位地址数据记录<br/>32-bit address data record
    /// </summary>
    S3 = 3,

    /// <summary>
    /// 16 位地址计数记录<br/>16-bit address count record
    /// </summary>
    S5 = 5,

    /// <summary>
    /// 24 位地址计数记录<br/>24-bit address count record
    /// </summary>
    S6 = 6,

    /// <summary>
    /// 32 位地址结束记录<br/>32-bit address termination record
    /// </summary>
    S7 = 7,

    /// <summary>
    /// 24 位地址结束记录<br/>24-bit address termination record
    /// </summary>
    S8 = 8,

    /// <summary>
    /// 16 位地址结束记录<br/>16-bit address termination record
    /// </summary>
    S9 = 9,
}

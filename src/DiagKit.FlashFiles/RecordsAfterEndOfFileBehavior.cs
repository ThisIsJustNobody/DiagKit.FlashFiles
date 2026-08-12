namespace DiagKit.FlashFiles;

/// <summary>
/// 终止记录之后有效记录的处理方式。<br/>Specifies how valid records after an end-of-file or termination record are handled.
/// </summary>
public enum RecordsAfterEndOfFileBehavior
{
    /// <summary>
    /// 拒绝终止记录之后的有效记录。<br/>Reject valid records after the end-of-file or termination record.
    /// </summary>
    Reject = 0,

    /// <summary>
    /// 忽略终止记录之后的所有非空行。<br/>Ignore all non-empty lines after the end-of-file or termination record.
    /// </summary>
    Ignore = 1,

    /// <summary>
    /// 继续解析终止记录之后的有效记录。<br/>Continue parsing valid records after the end-of-file or termination record.
    /// </summary>
    Parse = 2,
}

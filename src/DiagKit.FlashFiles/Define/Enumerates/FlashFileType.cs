namespace DiagKit.FlashFiles.Define.Enumerates;

/// <summary>
/// Flash 文件格式枚举<br/>Flash file format enumeration
/// </summary>
public enum FlashFileType : byte
{
    /// <summary>
    /// Motorola S-Record 格式（.s19 / .s28 / .s37）<br/>Motorola S-Record format (.s19 / .s28 / .s37)
    /// </summary>
    Motorola_S_Record,

    /// <summary>
    /// Intel MCS-86 HEX 格式（.hex）<br/>Intel MCS-86 HEX format (.hex)
    /// </summary>
    Intel_MCS_86,
}

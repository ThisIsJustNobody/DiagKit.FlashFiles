namespace DiagKit.FlashFiles;

/// <summary>
/// UDS 数据块导出选项。<br/>Options used when exporting UDS data blocks.
/// </summary>
public sealed class FlashUdsExportOptions
{
    /// <summary>
    /// 初始化 FlashUdsExportOptions 类的新实例。<br/>Initializes a new instance of the FlashUdsExportOptions class.
    /// </summary>
    /// <param name="maxBlockByteCount">每个导出块的最大字节数。<br/>The maximum byte count for each exported block.</param>
    public FlashUdsExportOptions(uint maxBlockByteCount)
    {
        if (maxBlockByteCount == 0)
            throw new ArgumentOutOfRangeException(nameof(maxBlockByteCount), "最大块字节数必须大于 0。");
        if (maxBlockByteCount > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maxBlockByteCount), "最大块字节数不能超过 Int32.MaxValue。");

        MaxBlockByteCount = maxBlockByteCount;
    }

    /// <summary>获取每个导出块的最大字节数。<br/>Gets the maximum byte count for each exported block.</summary>
    public uint MaxBlockByteCount { get; }

    /// <summary>获取或设置空洞填充值。<br/>Gets or sets the padding value used for address gaps.</summary>
    public byte PaddingValue { get; set; } = 0xFF;

    /// <summary>获取或设置是否填充块间地址空洞。<br/>Gets or sets whether address gaps between blocks are filled.</summary>
    public bool FillGaps { get; set; } = true;

    /// <summary>获取或设置是否跳过无源文件有效数据的块。<br/>Gets or sets whether blocks without source-file data are skipped.</summary>
    public bool SkipBlankBlocks { get; set; } = true;

    /// <summary>获取或设置是否要求导出块地址位于 32-bit 范围内。<br/>Gets or sets whether exported block addresses must fit in the 32-bit range.</summary>
    public bool RequireUInt32Address { get; set; } = true;
}

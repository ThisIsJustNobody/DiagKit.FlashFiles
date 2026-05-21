using DiagKit.FlashFiles.Define.Enumerates;
using DiagKit.FlashFiles.Tests;

using System.Reflection;

namespace DiagKit.FlashFiles.Tests.Common;

/// <summary>
/// UDS 拥有数据块导出测试。
/// </summary>
[TestClass]
public class FlashUdsExportTest
{
    [TestMethod]
    public void ToUdsBlockDtos_按最大块字节数分页()
    {
        using var doc = LoadIntel(
            dataSize: 2,
            TestRecordFactory.IntelData(0x0000, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 4);

        var blocks = doc.ToUdsBlockDtos(options);

        Assert.HasCount(2, blocks);
        Assert.AreEqual(0ul, blocks[0].StartAddress);
        Assert.AreEqual(1ul, blocks[0].EndAddress);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, blocks[0].Data);
        Assert.AreEqual(2ul, blocks[1].StartAddress);
        Assert.AreEqual(3ul, blocks[1].EndAddress);
        CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, blocks[1].Data);
    }

    [TestMethod]
    public void ToUdsBlockDtos_FillGaps为True_填充块间空洞()
    {
        using var doc = LoadIntel(
            dataSize: 1,
            TestRecordFactory.IntelData(0x0000, new byte[] { 1 }),
            TestRecordFactory.IntelData(0x0002, new byte[] { 3 }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 3)
        {
            PaddingValue = 0xEE,
        };

        var blocks = doc.ToUdsBlockDtos(options);

        Assert.HasCount(1, blocks);
        Assert.AreEqual(0ul, blocks[0].StartAddress);
        Assert.AreEqual(2ul, blocks[0].EndAddress);
        CollectionAssert.AreEqual(new byte[] { 1, 0xEE, 3 }, blocks[0].Data);
    }

    [TestMethod]
    public void ToUdsBlockDtos_SkipBlankBlocks为True_跳过无源数据页()
    {
        using var doc = LoadIntel(
            dataSize: 1,
            TestRecordFactory.IntelData(0x0000, new byte[] { 1 }),
            TestRecordFactory.IntelData(0x0002, new byte[] { 3 }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 1)
        {
            PaddingValue = 0xEE,
        };

        var blocks = doc.ToUdsBlockDtos(options);

        Assert.HasCount(2, blocks);
        Assert.AreEqual(0ul, blocks[0].StartAddress);
        Assert.AreEqual(2ul, blocks[1].StartAddress);
    }

    [TestMethod]
    public void ToUdsBlockDtos_SkipBlankBlocks为False_保留无源数据页()
    {
        using var doc = LoadIntel(
            dataSize: 1,
            TestRecordFactory.IntelData(0x0000, new byte[] { 1 }),
            TestRecordFactory.IntelData(0x0002, new byte[] { 3 }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 1)
        {
            PaddingValue = 0xEE,
            SkipBlankBlocks = false,
        };

        var blocks = doc.ToUdsBlockDtos(options);

        Assert.HasCount(3, blocks);
        Assert.AreEqual(1ul, blocks[1].StartAddress);
        CollectionAssert.AreEqual(new byte[] { 0xEE }, blocks[1].Data);
    }

    [TestMethod]
    public void ToUdsBlockDtos_真实FF数据_不会被误判为空白页()
    {
        using var doc = LoadIntel(
            dataSize: 1,
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xFF }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 1)
        {
            PaddingValue = 0xFF,
            SkipBlankBlocks = true,
        };

        var blocks = doc.ToUdsBlockDtos(options);

        Assert.HasCount(1, blocks);
        CollectionAssert.AreEqual(new byte[] { 0xFF }, blocks[0].Data);
    }

    [TestMethod]
    public void ToUdsBlockDtos_FillGaps为False_只导出源数据块()
    {
        using var doc = LoadIntel(
            dataSize: 1,
            TestRecordFactory.IntelData(0x0000, new byte[] { 1 }),
            TestRecordFactory.IntelData(0x0002, new byte[] { 3 }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 1)
        {
            FillGaps = false,
            SkipBlankBlocks = false,
        };

        var blocks = doc.ToUdsBlockDtos(options);

        Assert.HasCount(2, blocks);
        Assert.AreEqual(0ul, blocks[0].StartAddress);
        Assert.AreEqual(2ul, blocks[1].StartAddress);
    }

    [TestMethod]
    public void ToUdsBlockDtos_RequireUInt32Address为True_超出32位地址时抛出异常()
    {
        using var doc = CreateDocumentWithBlock((ulong)uint.MaxValue + 1);
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => doc.ToUdsBlockDtos(options));
    }

    [TestMethod]
    public void ToUdsBlockDtos_RequireUInt32Address为False_允许超出32位地址()
    {
        using var doc = CreateDocumentWithBlock((ulong)uint.MaxValue + 1);
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 1)
        {
            RequireUInt32Address = false,
        };

        var blocks = doc.ToUdsBlockDtos(options);

        Assert.HasCount(1, blocks);
        Assert.AreEqual((ulong)uint.MaxValue + 1, blocks[0].StartAddress);
    }

    [TestMethod]
    public void ToUdsBlockDtos_最大块字节数未按DataSize对齐_抛出异常()
    {
        using var doc = LoadIntel(
            dataSize: 2,
            TestRecordFactory.IntelData(0x0000, new byte[] { 1, 2 }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 3);

        Assert.ThrowsExactly<ArgumentException>(() => doc.ToUdsBlockDtos(options));
    }

    [TestMethod]
    public void FlashUdsExportOptions_无效最大块字节数_抛出异常()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FlashFiles.FlashUdsExportOptions(maxBlockByteCount: (uint)int.MaxValue + 1));
    }

    private static FlashFiles.FlashDocument LoadIntel(byte dataSize, params string[] records)
        => FlashFiles.FlashDocument.Load(
            TestRecordFactory.ToStream(records),
            FlashFileType.Intel_MCS_86,
            dataSize);

    private static FlashFiles.FlashDocument CreateDocumentWithBlock(ulong startAddress)
    {
        var block = new FlashFiles.FlashBlock(capacity: 1, startAddress, dataSize: 1);
        block.WriteAt(startAddress, new byte[] { 0xAA });
        var blocks = new List<FlashFiles.FlashBlock> { block };

        var constructor = typeof(FlashFiles.FlashDocument).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(List<FlashFiles.FlashBlock>), typeof(byte)],
            modifiers: null);

        Assert.IsNotNull(constructor);
        return (FlashFiles.FlashDocument)constructor.Invoke([blocks, (byte)1]);
    }
}

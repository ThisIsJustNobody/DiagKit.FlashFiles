using DiagKit.FlashFiles.Define.Enumerates;
using DiagKit.FlashFiles.Tests;

namespace DiagKit.FlashFiles.Tests.MotorolaSRecord;

/// <summary>
/// Motorola S-Record 解析器测试。
/// </summary>
[TestClass]
public class ParserTest
{
    [TestMethod]
    public void Parse_嵌入资源_返回正确的块数量()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetMotorolaSRecordStream(), FlashFileType.Motorola_S_Record, dataSize: 1);
        Assert.HasCount(8, doc.Blocks, "示例文件包含多个非连续地址区间。");
    }

    [TestMethod]
    public void Parse_嵌入资源_包含有效地址范围()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetMotorolaSRecordStream(), FlashFileType.Motorola_S_Record, dataSize: 1);
        Assert.AreEqual(0x20000200UL, doc.StartAddress);
        Assert.AreEqual(0x202F8FEFUL, doc.EndAddress);
        Assert.AreEqual(0x1F6248UL, doc.ByteCount);
        Assert.AreEqual(0x1F6248UL, doc.AddressCount);
    }

    [TestMethod]
    public void ContainsAddress_有效地址_返回True()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetMotorolaSRecordStream(), FlashFileType.Motorola_S_Record, dataSize: 1);
        Assert.IsTrue(doc.ContainsAddress(doc.StartAddress));
        Assert.IsTrue(doc.ContainsAddress(doc.EndAddress));
        Assert.IsFalse(doc.ContainsAddress(0x202F8848UL), "主体块和尾部入口块之间的空洞不能被视为有效数据。");
    }

    [TestMethod]
    public void ReadAt_起始地址_可读取数据()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetMotorolaSRecordStream(), FlashFileType.Motorola_S_Record, dataSize: 1);
        var data = doc.ReadAt(doc.StartAddress);
        Assert.AreEqual(1, data.Length, "dataSize=1 时每次读取 1 字节。");
        Assert.AreEqual(0x5A, data[0], "示例文件首字节应来自第一条 S3 数据记录。");
    }

    [TestMethod]
    public void RecordItem_解析已知行_校验通过()
    {
        // S0 记录
        var line = "S01100000000486578766965772056312E3009";
        var record = new FlashFiles.MotorolaSRecord.RecordItem(line);
        Assert.IsTrue(record.IsValid, "S0 记录校验应通过。");
        Assert.AreEqual(FlashFiles.MotorolaSRecord.RecordType.S0, record.RecordType);
    }

    [TestMethod]
    public void RecordItemSlim_解析已知行_校验通过()
    {
        var line = "S01100000000486578766965772056312E3009";
        using var memoryOwner = System.Buffers.MemoryPool<byte>.Shared.Rent(
            FlashFiles.MotorolaSRecord.RecordItemSlim.DefaultBufferSize);
        var buffer = memoryOwner.Memory.Span;
        var record = new FlashFiles.MotorolaSRecord.RecordItemSlim(line, buffer);
        Assert.IsTrue(record.IsValid, "S0 RecordItemSlim 校验应通过。");
    }

    [TestMethod]
    public void Parse_S1地址模式_返回16位地址()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1234, new byte[] { 0xAA, 0xBB }),
            TestRecordFactory.SRecord('9', 0, []));

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1);

        Assert.AreEqual(0x1234UL, doc.StartAddress);
        Assert.AreEqual(0x1235UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, doc.ReadRange(0x1234, 0x1235).ToArray());
    }

    [TestMethod]
    public void Parse_S2地址模式_返回24位地址()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('2', 0x123456, new byte[] { 0xCC, 0xDD }),
            TestRecordFactory.SRecord('8', 0, []));

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1);

        Assert.AreEqual(0x123456UL, doc.StartAddress);
        Assert.AreEqual(0x123457UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xCC, 0xDD }, doc.ReadRange(0x123456, 0x123457).ToArray());
    }

    [TestMethod]
    public void Parse_S3地址模式_返回32位地址()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('3', 0x12345678, new byte[] { 0xEE, 0xFF }),
            TestRecordFactory.SRecord('7', 0, []));

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1);

        Assert.AreEqual(0x12345678UL, doc.StartAddress);
        Assert.AreEqual(0x12345679UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xEE, 0xFF }, doc.ReadRange(0x12345678, 0x12345679).ToArray());
    }

    [TestMethod]
    public void Parse_S5计数记录_数量匹配时可解析()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA }),
            TestRecordFactory.SRecord('1', 0x1001, new byte[] { 0xBB }),
            TestRecordFactory.SRecord('5', 2, []),
            TestRecordFactory.SRecord('9', 0, []));

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1);

        Assert.HasCount(1, doc.Blocks);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, doc.ReadRange(0x1000, 0x1001).ToArray());
    }

    [TestMethod]
    public void Parse_S6计数记录_数量不匹配时抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('2', 0x100000, new byte[] { 0xAA }),
            TestRecordFactory.SRecord('6', 2, []),
            TestRecordFactory.SRecord('8', 0, []));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1));
    }

    [TestMethod]
    public void Parse_乱序非重叠记录_按地址排序并合并()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1002, new byte[] { 0xCC, 0xDD }),
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA, 0xBB }),
            TestRecordFactory.SRecord('9', 0, []));

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1);

        Assert.HasCount(1, doc.Blocks);
        Assert.AreEqual(0x1000UL, doc.StartAddress);
        Assert.AreEqual(0x1003UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, doc.ReadRange(0x1000, 0x1003).ToArray());
    }

    [TestMethod]
    public void Parse_重叠记录_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA, 0xBB }),
            TestRecordFactory.SRecord('1', 0x1001, new byte[] { 0xCC }),
            TestRecordFactory.SRecord('9', 0, []));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1));
    }

    [TestMethod]
    public void Parse_缺少结束记录_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA }));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1));
    }
}

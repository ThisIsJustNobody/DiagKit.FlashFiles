using DiagKit.FlashFiles.Define.Enumerates;
using DiagKit.FlashFiles.Tests;

namespace DiagKit.FlashFiles.Tests.IntelMcs86;

/// <summary>
/// Intel MCS-86 HEX 解析器测试。
/// </summary>
[TestClass]
public class ParserTest
{
    [TestMethod]
    public void Parse_嵌入资源_返回正确的块数量()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        Assert.HasCount(8, doc.Blocks, "示例文件包含多个非连续地址区间。");
    }

    [TestMethod]
    public void Parse_嵌入资源_地址范围正确()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        Assert.AreEqual(0x003E8000UL, doc.StartAddress, "起始地址不匹配。");
        Assert.AreEqual(0x003F5FFFUL, doc.EndAddress, "结束地址不匹配。");
    }

    [TestMethod]
    public void ReadAt_已知地址_返回正确数据()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        var data = doc.ReadAt(0x003E8500);
        Assert.AreEqual(0x64, data[0], "字节 0 不匹配。");
        Assert.AreEqual(0x7A, data[1], "字节 1 不匹配。");
    }

    [TestMethod]
    public void ReadAt_最后一个块的地址_返回正确数据()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        var data = doc.ReadAt(0x003F5FFF);
        Assert.AreEqual(0xB7, data[0]);
        Assert.AreEqual(0x79, data[1]);
    }

    [TestMethod]
    public void ReadRange_跨地址范围_返回正确数据()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        var data = doc.ReadRange(0x003F5FFE, 0x003F5FFF);
        CollectionAssert.AreEqual(new byte[] { 0x25, 0x38, 0xB7, 0x79 }, data.ToArray());
    }

    [TestMethod]
    public void ReadAt_不存在的地址_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(
            () => doc.ReadAt(0x00000000));
    }

    [TestMethod]
    public void ContainsAddress_已知地址_返回True()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        Assert.IsTrue(doc.ContainsAddress(0x003E8500));
        Assert.IsFalse(doc.ContainsAddress(0x00000000));
    }

    [TestMethod]
    public void Load_从文件路径_与流结果一致()
    {
        // 使用流加载
        using var docFromStream = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.hex");
        try
        {
            File.WriteAllBytes(filePath, InitializeTest.Intel86HexSampleBytes);

            using var docFromFile = FlashFiles.FlashDocument.Load(filePath, dataSize: 2);

            Assert.HasCount(docFromStream.Blocks.Count, docFromFile.Blocks);
            Assert.AreEqual(docFromStream.StartAddress, docFromFile.StartAddress);
            Assert.AreEqual(docFromStream.EndAddress, docFromFile.EndAddress);
            Assert.AreEqual(docFromStream.ByteCount, docFromFile.ByteCount);
            Assert.AreEqual(docFromStream.AddressCount, docFromFile.AddressCount);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [TestMethod]
    public void RecordItemSlim_各行校验_均通过()
    {
        var lines = InitializeTest.Intel86HexSampleResource.Split(
            Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        using var memoryOwner = System.Buffers.MemoryPool<byte>.Shared.Rent(
            FlashFiles.IntelMcs86.RecordItemSlim.DefaultBufferSize);
        var buffer = memoryOwner.Memory.Span;

        var checkedCount = 0;
        ushort baseAddress = 0;
        foreach (var line in lines.Take(100))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var record = new FlashFiles.IntelMcs86.RecordItemSlim(line, buffer, baseAddress);
            Assert.IsTrue(record.IsValid, $"记录校验失败：{line}");
            if (record.RecordType == FlashFiles.IntelMcs86.RecordType.ExtendedLinearAddressRecord)
            {
                record.TryReadBaseAddress(out baseAddress);
            }
            checkedCount++;
        }
        Assert.AreEqual(100, checkedCount, "应完整校验前 100 条记录。");
    }

    [TestMethod]
    public void Parse_扩展段地址_按IntelHex86规则左移4位()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelExtendedSegmentAddress(0x1234),
            TestRecordFactory.IntelData(0x0020, new byte[] { 0xAA, 0xBB }),
            TestRecordFactory.IntelEndOfFile());

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1);

        Assert.AreEqual(0x12360UL, doc.StartAddress);
        Assert.AreEqual(0x12361UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, doc.ReadRange(0x12360, 0x12361).ToArray());
    }

    [TestMethod]
    public void Parse_乱序非重叠记录_按地址排序并合并()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0002, new byte[] { 0xCC, 0xDD }),
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA, 0xBB }),
            TestRecordFactory.IntelEndOfFile());

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1);

        Assert.HasCount(1, doc.Blocks);
        Assert.AreEqual(0x0000UL, doc.StartAddress);
        Assert.AreEqual(0x0003UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, doc.ReadRange(0x0000, 0x0003).ToArray());
    }

    [TestMethod]
    public void Parse_重叠记录_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA, 0xBB }),
            TestRecordFactory.IntelData(0x0001, new byte[] { 0xCC }),
            TestRecordFactory.IntelEndOfFile());

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1));
    }

    [TestMethod]
    public void Parse_缺少EOF记录_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1));
    }

    [TestMethod]
    public void Parse_EOF之后还有记录_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelEndOfFile(),
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1));
    }

    [TestMethod]
    public void Parse_非数据记录长度不符合规范_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelRecord(1, 0, new byte[] { 0x00 }));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1));
    }

    [TestMethod]
    public void Parse_数据长度不是DataSize整数倍_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA, 0xBB, 0xCC }),
            TestRecordFactory.IntelEndOfFile());

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 2));
    }

    [TestMethod]
    public void Parse_单条记录255字节_可完整解析()
    {
        var data = Enumerable.Range(0, 255).Select(i => (byte)i).ToArray();
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0100, data),
            TestRecordFactory.IntelEndOfFile());

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1);

        Assert.AreEqual(0x0100UL, doc.StartAddress);
        Assert.AreEqual(0x01FEUL, doc.EndAddress);
        Assert.AreEqual(255UL, doc.ByteCount);
        CollectionAssert.AreEqual(data, doc.ReadRange(0x0100, 0x01FE).ToArray());
    }
}

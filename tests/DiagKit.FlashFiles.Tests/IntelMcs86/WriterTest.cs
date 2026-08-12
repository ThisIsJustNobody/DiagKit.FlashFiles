using DiagKit.FlashFiles.Define.Enumerates;

namespace DiagKit.FlashFiles.Tests.IntelMcs86;

/// <summary>
/// Intel MCS-86 HEX 写入测试。
/// </summary>
[TestClass]
public class WriterTest
{
    [TestMethod]
    public void Save_写入流_可重新解析且数据一致()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        using var stream = new MemoryStream();

        doc.Save(stream, FlashFileType.Intel_MCS_86);

        Assert.AreEqual((byte)':', stream.ToArray()[0], "Intel HEX 输出必须直接以 ':' 开始，不能写入 UTF-8 BOM。");

        stream.Position = 0;
        using var roundTrip = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 2);
        Assert.AreEqual(doc.StartAddress, roundTrip.StartAddress);
        Assert.AreEqual(doc.EndAddress, roundTrip.EndAddress);
        Assert.AreEqual(doc.ByteCount, roundTrip.ByteCount);
        CollectionAssert.AreEqual(doc.ReadAt(0x003E8500).ToArray(), roundTrip.ReadAt(0x003E8500).ToArray());
    }

    [TestMethod]
    public void SaveToFile_Hex扩展名_可重新解析()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.hex");

        try
        {
            doc.SaveToFile(filePath);
            using var saved = FlashFiles.FlashDocument.Load(filePath, dataSize: 2);

            Assert.AreEqual(doc.StartAddress, saved.StartAddress);
            Assert.AreEqual(doc.EndAddress, saved.EndAddress);
            Assert.AreEqual(doc.ByteCount, saved.ByteCount);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [TestMethod]
    public void Save_跨64K边界_写入扩展线性地址记录()
    {
        var first = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var second = Enumerable.Range(16, 16).Select(i => (byte)i).ToArray();
        using var input = TestRecordFactory.ToStream(
            TestRecordFactory.IntelExtendedLinearAddress(0),
            TestRecordFactory.IntelData(0xFFF8, first),
            TestRecordFactory.IntelExtendedLinearAddress(1),
            TestRecordFactory.IntelData(0x0000, second),
            ":00000001FF");
        using var doc = FlashFiles.FlashDocument.Load(input, FlashFileType.Intel_MCS_86, dataSize: 2);
        using var output = new MemoryStream();

        doc.Save(output, FlashFileType.Intel_MCS_86);

        output.Position = 0;
        using var roundTrip = FlashFiles.FlashDocument.Load(output, FlashFileType.Intel_MCS_86, dataSize: 2);
        Assert.HasCount(1, roundTrip.Blocks);
        Assert.AreEqual(0x0000FFF8UL, roundTrip.StartAddress);
        Assert.AreEqual(0x00010007UL, roundTrip.EndAddress);
        CollectionAssert.AreEqual(first.Concat(second).ToArray(), roundTrip.ReadRange(0x0000FFF8, 0x00010007).ToArray());
    }
}

using DiagKit.FlashFiles.Define.Enumerates;
using DiagKit.FlashFiles.Tests;

namespace DiagKit.FlashFiles.Tests.Common;

/// <summary>
/// FlashDocument 核心功能测试。
/// </summary>
[TestClass]
public class FlashDocumentTest
{
    [TestMethod]
    public void Load_Intel嵌入资源_属性正确()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.HasCount(8, doc.Blocks);
        Assert.AreEqual(2, doc.DataSize);
        Assert.AreEqual(0x003E8000UL, doc.StartAddress);
        Assert.AreEqual(0x003F5FFFUL, doc.EndAddress);
        Assert.AreEqual(0x00013C4CUL, doc.ByteCount);
        Assert.AreEqual(0x00009E26UL, doc.AddressCount);
    }

    [TestMethod]
    public void Load_Motorola嵌入资源_属性正确()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetMotorolaSRecordStream(), FlashFileType.Motorola_S_Record, dataSize: 1);

        Assert.HasCount(8, doc.Blocks);
        Assert.AreEqual(1, doc.DataSize);
        Assert.AreEqual(0x20000200UL, doc.StartAddress);
        Assert.AreEqual(0x202F8FEFUL, doc.EndAddress);
    }

    [TestMethod]
    public void Blocks_返回只读列表()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.IsInstanceOfType<IReadOnlyList<FlashBlock>>(doc.Blocks);
        Assert.IsFalse(doc.Blocks is List<FlashBlock>, "Blocks 不应暴露内部可变 List。");
    }

    [TestMethod]
    public void TryFillPage_有效范围_返回True()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        using var page = new FlashPage(0x100, 2);
        var result = doc.TryFillPage(page, doc.StartAddress);

        Assert.IsTrue(result, "填充页应成功。");
        Assert.IsTrue(page.HasValidData, "页应包含有效数据。");
        Assert.AreEqual(doc.StartAddress, page.StartAddress, "页的起始地址应匹配。");
    }

    [TestMethod]
    public void TryFillPage_DataSize不匹配_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        using var page = new FlashPage(0x100, 1); // dataSize=1 不匹配
        Assert.ThrowsExactly<ArgumentException>(
            () => doc.TryFillPage(page, doc.StartAddress));
    }

    [TestMethod]
    public void EnumeratePages_有效范围_返回正确页数()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        var pages = doc.EnumeratePages(0x100, 0x003E8000, 0x003E81FF).ToList();
        Assert.HasCount(2, pages, "0x200 / 0x100 = 2 页。");

        foreach (var page in pages)
            page.Dispose();
    }

    [TestMethod]
    public void FilterPages_无有效数据_返回空()
    {
        var result = FlashFiles.FlashDocument.FilterPages(
            [], true, true, true);
        Assert.AreEqual(0, result.Count(), "空列表过滤应返回空。");
    }

    [TestMethod]
    public void FilterPages_跳过空白页_过滤正确()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        var pages = doc.EnumeratePages(0x100, 0x003E8000, 0x003E81FF).ToList();
        var filtered = FlashFiles.FlashDocument.FilterPages(pages, true, true, true).ToList();

        // 所有页都应该有数据（该范围是连续数据）
        Assert.HasCount(pages.Count, filtered, "有数据的页不应被过滤。");

        foreach (var page in pages)
            page.Dispose();
    }

    [TestMethod]
    public void Load_文件路径不存在_抛出异常()
    {
        Assert.ThrowsExactly<FileNotFoundException>(
            () => FlashFiles.FlashDocument.Load("不存在的文件.hex", 2));
    }

    [TestMethod]
    public void Load_不支持的格式_抛出异常()
    {
        Assert.ThrowsExactly<NotSupportedException>(
            () => FlashFiles.FlashDocument.Load("test.txt", 2));
    }

    [TestMethod]
    public void ReadAt_地址不存在_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(
            () => doc.ReadAt(0x00000000));
    }

    [TestMethod]
    public void ReadRange_块内范围_返回正确数据()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        var data = doc.ReadRange(0x003F5F80, 0x003F5F81);
        CollectionAssert.AreEqual(new byte[] { 0x69, 0x60, 0xAA, 0xEB }, data.ToArray());
    }

    [TestMethod]
    public void ReadRange_结束地址小于起始地址_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => doc.ReadRange(0x003E8501, 0x003E8500));
    }

    [TestMethod]
    public void Load_DataSize为零_抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0, new byte[] { 0xAA }),
            TestRecordFactory.IntelEndOfFile());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 0));
    }

    [TestMethod]
    public void ContainsAddress_释放后_抛出异常()
    {
        var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
        doc.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(
            () => doc.ContainsAddress(0x003E8500));
    }

    [TestMethod]
    public void StartAddress_EndAddress_预计算且一致()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        // 验证预计算值与块的极值一致
        var minStart = doc.Blocks.Min(b => b.StartAddress);
        var maxEnd = doc.Blocks.Max(b => b.EndAddress);
        Assert.AreEqual(minStart, doc.StartAddress);
        Assert.AreEqual(maxEnd, doc.EndAddress);
    }

    [TestMethod]
    public void WriteAt_修改后_ReadAt返回新数据()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        ulong address = 0x003E8500;
        var original = doc.ReadAt(address).ToArray();

        // 写入新数据
        var newData = new byte[] { 0xAA, 0xBB };
        doc.WriteAt(address, newData);

        // 读取验证
        var modified = doc.ReadAt(address).ToArray();
        CollectionAssert.AreEqual(newData, modified, "写入后应读取到新数据。");

        // 恢复原始数据
        doc.WriteAt(address, original);
    }

    [TestMethod]
    public void WriteRange_修改范围_读取验证()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        ulong start = 0x003F5FFE;
        ulong end = 0x003F5FFF;
        var original = doc.ReadRange(start, end).ToArray();

        // 写入新数据
        var newData = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        doc.WriteRange(start, end, newData);

        var modified = doc.ReadRange(start, end).ToArray();
        CollectionAssert.AreEqual(newData, modified, "写入范围后应读取到新数据。");

        // 恢复
        doc.WriteRange(start, end, original);
    }

    [TestMethod]
    public void WriteAt_地址不存在_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(
            () => doc.WriteAt(0x00000000UL, new byte[] { 0x00, 0x00 }));
    }

    [TestMethod]
    public void WriteAt_数据长度不匹配_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.ThrowsExactly<ArgumentException>(
            () => doc.WriteAt(0x003E8500UL, new byte[] { 0x01 }));
    }
}

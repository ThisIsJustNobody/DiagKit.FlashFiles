using DiagKit.FlashFiles.Define.Enumerates;

namespace DiagKit.FlashFiles.Tests.Common;

/// <summary>
/// FlashDocument 内存数据创建测试。
/// </summary>
[TestClass]
public class FlashDocumentCreateTest
{
    [TestMethod]
    public void Create_RawData_复制数据且可保存为IntelHex后重新解析()
    {
        var source = new byte[] { 1, 2, 3, 4 };

        using var doc = FlashFiles.FlashDocument.Create(0x00010010, source, dataSize: 1);
        source[0] = 0xFF;

        Assert.AreEqual(0x00010010UL, doc.StartAddress);
        Assert.AreEqual(0x00010013UL, doc.EndAddress);
        Assert.AreEqual(4UL, doc.ByteCount);
        Assert.AreEqual(4UL, doc.AddressCount);
        Assert.AreEqual(1, doc.DataSize);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, doc.ReadRange(0x00010010, 0x00010013).ToArray());

        using var stream = new MemoryStream();
        doc.Save(stream, FlashFileType.Intel_MCS_86);
        stream.Position = 0;

        using var roundTrip = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1);
        Assert.AreEqual(doc.StartAddress, roundTrip.StartAddress);
        Assert.AreEqual(doc.EndAddress, roundTrip.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, roundTrip.ReadRange(0x00010010, 0x00010013).ToArray());
    }

    [TestMethod]
    public void Create_BlockDtos_排序合并相邻块且保留地址空洞()
    {
        var dtos = new[]
        {
            new FlashFiles.FlashBlockDto(0x2004, new byte[] { 5, 6 }, dataSize: 1),
            new FlashFiles.FlashBlockDto(0x1002, new byte[] { 3, 4 }, dataSize: 1),
            new FlashFiles.FlashBlockDto(0x1000, new byte[] { 1, 2 }, dataSize: 1),
        };

        using var doc = FlashFiles.FlashDocument.Create(dtos);

        Assert.HasCount(2, doc.Blocks);
        Assert.AreEqual(0x1000UL, doc.Blocks[0].StartAddress);
        Assert.AreEqual(0x1003UL, doc.Blocks[0].EndAddress);
        Assert.AreEqual(0x2004UL, doc.Blocks[1].StartAddress);
        Assert.AreEqual(0x2005UL, doc.Blocks[1].EndAddress);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, doc.ReadRange(0x1000, 0x1003).ToArray());
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, doc.ReadRange(0x2004, 0x2005).ToArray());
    }

    [TestMethod]
    public void Create_BlockDtos_复制Dto数据且支持文档读写和Uds导出()
    {
        var dto = new FlashFiles.FlashBlockDto(0x3000, new byte[] { 1, 2, 3, 4 }, dataSize: 2);

        using var doc = FlashFiles.FlashDocument.Create(new[] { dto });
        dto.Data[0] = 0xFF;

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, doc.ReadAt(0x3000).ToArray());
        doc.WriteAt(0x3001, new byte[] { 0xAA, 0xBB });
        CollectionAssert.AreEqual(new byte[] { 1, 2, 0xAA, 0xBB }, doc.ReadRange(0x3000, 0x3001).ToArray());

        var owned = doc.ToBlockDtos();
        Assert.HasCount(1, owned);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 0xAA, 0xBB }, owned[0].Data);

        var uds = doc.ToUdsBlockDtos(new FlashUdsExportOptions(maxBlockByteCount: 4));
        Assert.HasCount(1, uds);
        Assert.AreEqual(0x3000UL, uds[0].StartAddress);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 0xAA, 0xBB }, uds[0].Data);
    }

    [TestMethod]
    public void Create_无效RawData_抛出异常()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => FlashFiles.FlashDocument.Create(0, ReadOnlySpan<byte>.Empty, dataSize: 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => FlashFiles.FlashDocument.Create(0, new byte[] { 1 }, dataSize: 0));
        Assert.ThrowsExactly<ArgumentException>(
            () => FlashFiles.FlashDocument.Create(0, new byte[] { 1, 2, 3 }, dataSize: 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => FlashFiles.FlashDocument.Create(ulong.MaxValue - 1, new byte[] { 1, 2, 3 }, dataSize: 1));
    }

    [TestMethod]
    public void Create_无效BlockDtos_抛出异常()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => FlashFiles.FlashDocument.Create(Array.Empty<FlashFiles.FlashBlockDto>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => FlashFiles.FlashDocument.Create(new FlashFiles.FlashBlockDto?[] { null! }!));
        Assert.ThrowsExactly<ArgumentException>(
            () => FlashFiles.FlashDocument.Create(new[]
            {
                new FlashFiles.FlashBlockDto(0x1000, new byte[] { 1, 2 }, dataSize: 1),
                new FlashFiles.FlashBlockDto(0x2000, new byte[] { 3, 4 }, dataSize: 2),
            }));
        Assert.ThrowsExactly<ArgumentException>(
            () => FlashFiles.FlashDocument.Create(new[]
            {
                new FlashFiles.FlashBlockDto(0x1000, new byte[] { 1, 2 }, dataSize: 1),
                new FlashFiles.FlashBlockDto(0x1001, new byte[] { 3 }, dataSize: 1),
            }));
    }

    [TestMethod]
    public void Save_IntelHex_地址超过32位_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Create(0x1_0000_0000UL, new byte[] { 1 }, dataSize: 1);
        using var stream = new MemoryStream();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => doc.Save(stream, FlashFileType.Intel_MCS_86));
    }
}

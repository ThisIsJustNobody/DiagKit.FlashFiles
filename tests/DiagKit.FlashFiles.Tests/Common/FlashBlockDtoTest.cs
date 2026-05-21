using DiagKit.FlashFiles.Define.Enumerates;
using DiagKit.FlashFiles.Tests;

namespace DiagKit.FlashFiles.Tests.Common;

/// <summary>
/// FlashBlockDto 拥有数据模型测试。
/// </summary>
[TestClass]
public class FlashBlockDtoTest
{
    [TestMethod]
    public void Constructor_复制输入数据()
    {
        var source = new byte[] { 1, 2, 3, 4 };

        var dto = new FlashFiles.FlashBlockDto(startAddress: 0x1000, data: source, dataSize: 2);
        source[0] = 0xFF;

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, dto.Data);
        Assert.AreEqual(0x1000UL, dto.StartAddress);
        Assert.AreEqual(0x1001UL, dto.EndAddress);
        Assert.AreEqual(0x1002UL, dto.NextAddress);
        Assert.AreEqual(4u, dto.ByteCount);
        Assert.AreEqual(2u, dto.AddressCount);
        Assert.AreEqual(2, dto.DataSize);
    }

    [TestMethod]
    public void Constructor_无效输入_抛出异常()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new FlashFiles.FlashBlockDto(startAddress: 0, data: [], dataSize: 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FlashFiles.FlashBlockDto(startAddress: 0, data: [1, 2], dataSize: 0));
        Assert.ThrowsExactly<ArgumentException>(
            () => new FlashFiles.FlashBlockDto(startAddress: 0, data: [1, 2, 3], dataSize: 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new FlashFiles.FlashBlockDto(startAddress: ulong.MaxValue - 2, data: [1, 2, 3, 4], dataSize: 1));
    }

    [TestMethod]
    public void FlashBlock_ToDto_复制数据且不影响原块()
    {
        using var block = new FlashFiles.FlashBlock(capacity: 4, startAddress: 0x2000, dataSize: 2);
        block.WriteRange(0x2000, 0x2001, new byte[] { 1, 2, 3, 4 });

        var dto = block.ToDto();
        dto.Data[0] = 0xFF;

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, block.Data.ToArray());
        Assert.AreEqual(0x2000UL, dto.StartAddress);
        Assert.AreEqual(0x2001UL, dto.EndAddress);
    }

    [TestMethod]
    public void FlashDocument_ToBlockDtos_释放文档后仍可读取Dto数据()
    {
        var doc = FlashFiles.FlashDocument.Load(
            TestRecordFactory.ToStream(
                TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA, 0xBB }),
                TestRecordFactory.IntelEndOfFile()),
            FlashFileType.Intel_MCS_86,
            dataSize: 1);

        var dtos = doc.ToBlockDtos();
        doc.Dispose();

        Assert.HasCount(1, dtos);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, dtos[0].Data);
        Assert.AreEqual(0ul, dtos[0].StartAddress);
        Assert.AreEqual(1ul, dtos[0].EndAddress);
    }
}

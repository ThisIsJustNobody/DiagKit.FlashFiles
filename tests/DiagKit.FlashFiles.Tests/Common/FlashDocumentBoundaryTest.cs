using DiagKit.FlashFiles.Define.Enumerates;

using System.Reflection;
using System.Runtime.Serialization;

namespace DiagKit.FlashFiles.Tests.Common;

/// <summary>
/// FlashDocument 边界条件测试。
/// </summary>
[TestClass]
public class FlashDocumentBoundaryTest
{
    [TestMethod]
    public void Load_ReadOnlySpan_可解析数据()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.Intel86HexSampleBytes.AsSpan(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.AreEqual(0x003E8000UL, doc.StartAddress);
        Assert.AreEqual(0x003F5FFFUL, doc.EndAddress);
    }

    [TestMethod]
    public void EnumeratePages_结束地址小于起始地址_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => doc.EnumeratePages(0x100, doc.EndAddress, doc.StartAddress).ToList());
    }

    [TestMethod]
    public void EnumeratePages_页地址数量为零_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => doc.EnumeratePages(0, doc.StartAddress, doc.EndAddress).ToList());
    }

    [TestMethod]
    public void EnumeratePages_非整页范围_包含尾页()
    {
        using var doc = FlashFiles.FlashDocument.Load(
            InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);

        var pages = doc.EnumeratePages(0x100, doc.StartAddress, doc.StartAddress + 0x100).ToList();

        Assert.HasCount(2, pages);
        foreach (var page in pages)
            page.Dispose();
    }

    [TestMethod]
    public void ByteCount_AddressCount_超过UInt32范围_不截断()
    {
        var blocks = new List<FlashBlock>
        {
            CreateFakeBlock(0, uint.MaxValue, uint.MaxValue),
            CreateFakeBlock(uint.MaxValue, 2, 2),
        };

        var constructor = typeof(FlashFiles.FlashDocument).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(List<FlashBlock>), typeof(byte)],
            modifiers: null);

        Assert.IsNotNull(constructor);
        var doc = (FlashFiles.FlashDocument)constructor.Invoke([blocks, (byte)1]);

        Assert.AreEqual((ulong)uint.MaxValue + 2, doc.ByteCount);
        Assert.AreEqual((ulong)uint.MaxValue + 2, doc.AddressCount);
    }

    private static FlashBlock CreateFakeBlock(ulong startAddress, uint byteCount, uint addressCount)
    {
#pragma warning disable SYSLIB0050
        var block = (FlashBlock)FormatterServices.GetUninitializedObject(typeof(FlashBlock));
#pragma warning restore SYSLIB0050
        SetAutoProperty(block, nameof(FlashBlock.StartAddress), startAddress);
        SetAutoProperty(block, nameof(FlashBlock.EndAddress), startAddress + addressCount - 1);
        SetAutoProperty(block, nameof(FlashBlock.NextAddress), startAddress + addressCount);
        SetAutoProperty(block, nameof(FlashBlock.ByteCount), byteCount);
        SetAutoProperty(block, nameof(FlashBlock.AddressCount), addressCount);
        SetAutoProperty(block, nameof(FlashBlock.DataSize), (byte)1);
        return block;
    }

    private static void SetAutoProperty<T>(FlashBlock block, string propertyName, T value)
    {
        var field = typeof(FlashBlock).GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(block, value);
    }
}

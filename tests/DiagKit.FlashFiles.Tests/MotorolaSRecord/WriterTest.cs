using DiagKit.FlashFiles.Define.Enumerates;

using System.Text;

namespace DiagKit.FlashFiles.Tests.MotorolaSRecord;

/// <summary>
/// Motorola S-Record 写入测试。
/// </summary>
[TestClass]
public class WriterTest
{
    [TestMethod]
    public void Save_写入流_低地址可重新解析且数据一致()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0x1234,
            new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
            dataSize: 1);
        using var stream = new MemoryStream();

        doc.Save(stream, FlashFileType.Motorola_S_Record);

        stream.Position = 0;
        using var roundTrip = FlashFiles.FlashDocument.Load(
            stream,
            FlashFileType.Motorola_S_Record,
            dataSize: 1);
        Assert.AreEqual(doc.StartAddress, roundTrip.StartAddress);
        Assert.AreEqual(doc.EndAddress, roundTrip.EndAddress);
        Assert.AreEqual(doc.ByteCount, roundTrip.ByteCount);
        CollectionAssert.AreEqual(
            doc.ReadRange(doc.StartAddress, doc.EndAddress).ToArray(),
            roundTrip.ReadRange(roundTrip.StartAddress, roundTrip.EndAddress).ToArray());
    }

    [TestMethod]
    public void Save_写入流_不写入Utf8Bom()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0x1234,
            new byte[] { 0xAA, 0xBB },
            dataSize: 1);
        using var stream = new MemoryStream();

        doc.Save(stream, FlashFileType.Motorola_S_Record);

        var bytes = stream.ToArray();
        Assert.IsNotEmpty(bytes);
        Assert.AreEqual((byte)'S', bytes[0], "S-Record 输出必须直接以 'S' 开始，不能写入 UTF-8 BOM。");
    }

    [TestMethod]
    public void Save_写入流_最高地址FFFF自动使用S1与S9()
    {
        AssertAutoSelectedRecordTypes(0xFFFF, "S1", "S9");
    }

    [TestMethod]
    public void Save_写入流_最高地址10000自动使用S2与S8()
    {
        AssertAutoSelectedRecordTypes(0x10000, "S2", "S8");
    }

    [TestMethod]
    public void Save_写入流_最高地址01000000自动使用S3与S7()
    {
        AssertAutoSelectedRecordTypes(0x01000000, "S3", "S7");
    }

    [TestMethod]
    public void Save_写入流_DataSize超过单条记录数据上限时写入前失败()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0,
            new byte[32],
            dataSize: 32);
        using var stream = new MemoryStream();

        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => doc.Save(stream, FlashFileType.Motorola_S_Record));

        Assert.AreEqual("dataSize", ex.ParamName);
        Assert.AreEqual(0, stream.Length, "DataSize 超过单条记录数据上限时必须在写入前失败。");
    }

    [TestMethod]
    public void Save_写入流_数据记录数量超过S5范围时写入前失败()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0,
            new byte[(ushort.MaxValue + 1) * 16],
            dataSize: 1);
        using var stream = new MemoryStream();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => doc.Save(stream, FlashFileType.Motorola_S_Record));

        Assert.AreEqual(0, stream.Length, "数据记录数量超过 S5 范围时必须在写入前失败。");
    }

    private static void AssertAutoSelectedRecordTypes(ulong highestAddress, string expectedDataPrefix, string expectedTerminationPrefix)
    {
        using var doc = FlashFiles.FlashDocument.Create(
            highestAddress,
            new byte[] { 0xAA },
            dataSize: 1);
        using var stream = new MemoryStream();

        doc.Save(stream, FlashFileType.Motorola_S_Record);

        var lines = ReadLines(stream);
        var dataLine = lines.First(line => line.StartsWith('S') && line[1] is '1' or '2' or '3');
        var terminationLine = lines[^1];

        Assert.IsTrue(dataLine.StartsWith(expectedDataPrefix, StringComparison.Ordinal), $"数据记录应以 {expectedDataPrefix} 开始，实际为 {dataLine}。");
        Assert.IsTrue(terminationLine.StartsWith(expectedTerminationPrefix, StringComparison.Ordinal), $"结束记录应以 {expectedTerminationPrefix} 开始，实际为 {terminationLine}。");
    }

    private static string[] ReadLines(MemoryStream stream)
    {
        var text = Encoding.UTF8.GetString(stream.ToArray());
        return text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    }
}

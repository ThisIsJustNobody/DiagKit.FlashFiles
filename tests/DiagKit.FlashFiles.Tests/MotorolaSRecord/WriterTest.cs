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

    [TestMethod]
    public void SaveToFile_S19扩展名_写出S1和S9()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0x1234,
            new byte[] { 0xAA, 0xBB },
            dataSize: 1);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.s19");

        try
        {
            doc.SaveToFile(filePath);

            var lines = File.ReadAllLines(filePath);
            Assert.IsTrue(lines.Any(line => line.StartsWith("S1", StringComparison.Ordinal)));
            Assert.IsTrue(lines[^1].StartsWith("S9", StringComparison.Ordinal));

            using var saved = FlashFiles.FlashDocument.Load(filePath, dataSize: 1);
            Assert.AreEqual(doc.StartAddress, saved.StartAddress);
            Assert.AreEqual(doc.EndAddress, saved.EndAddress);
            CollectionAssert.AreEqual(
                doc.ReadRange(doc.StartAddress, doc.EndAddress).ToArray(),
                saved.ReadRange(saved.StartAddress, saved.EndAddress).ToArray());
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [TestMethod]
    public void SaveToFile_S28扩展名_写出S2和S8()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0x1234,
            new byte[] { 0xCC, 0xDD },
            dataSize: 1);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.s28");

        try
        {
            doc.SaveToFile(filePath);

            var lines = File.ReadAllLines(filePath);
            Assert.IsTrue(lines.Any(line => line.StartsWith("S2", StringComparison.Ordinal)));
            Assert.IsTrue(lines[^1].StartsWith("S8", StringComparison.Ordinal));

            using var saved = FlashFiles.FlashDocument.Load(filePath, dataSize: 1);
            Assert.AreEqual(doc.StartAddress, saved.StartAddress);
            Assert.AreEqual(doc.EndAddress, saved.EndAddress);
            CollectionAssert.AreEqual(
                doc.ReadRange(doc.StartAddress, doc.EndAddress).ToArray(),
                saved.ReadRange(saved.StartAddress, saved.EndAddress).ToArray());
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [TestMethod]
    public void SaveToFile_S37扩展名_写出S3和S7()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0x1234,
            new byte[] { 0xEE, 0xFF },
            dataSize: 1);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.s37");

        try
        {
            doc.SaveToFile(filePath);

            var lines = File.ReadAllLines(filePath);
            Assert.IsTrue(lines.Any(line => line.StartsWith("S3", StringComparison.Ordinal)));
            Assert.IsTrue(lines[^1].StartsWith("S7", StringComparison.Ordinal));

            using var saved = FlashFiles.FlashDocument.Load(filePath, dataSize: 1);
            Assert.AreEqual(doc.StartAddress, saved.StartAddress);
            Assert.AreEqual(doc.EndAddress, saved.EndAddress);
            CollectionAssert.AreEqual(
                doc.ReadRange(doc.StartAddress, doc.EndAddress).ToArray(),
                saved.ReadRange(saved.StartAddress, saved.EndAddress).ToArray());
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [TestMethod]
    public void SaveToFile_S19地址超过16位_保留既有文件内容()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0x010000,
            new byte[] { 0xAA },
            dataSize: 1);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.s19");
        const string sentinel = "既有文件内容不应被截断";
        File.WriteAllText(filePath, sentinel, Encoding.UTF8);

        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => doc.SaveToFile(filePath));
            Assert.AreEqual(sentinel, File.ReadAllText(filePath, Encoding.UTF8));
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [TestMethod]
    public void SaveToFile_S19地址超过16位_抛出异常()
    {
        using var doc = FlashFiles.FlashDocument.Create(
            0x010000,
            new byte[] { 0xAA },
            dataSize: 1);
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.s19");

        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => doc.SaveToFile(filePath));
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
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

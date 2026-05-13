using DiagKit.FlashFiles.Define.Enumerates;

namespace DiagKit.FlashFiles.Tests;

/// <summary>
/// 解析器校验失败路径测试。
/// </summary>
[TestClass]
public class ParserValidationTest
{
    [TestMethod]
    public void Intel_Checksum错误_启用校验时抛出异常()
    {
        var validData = TestRecordFactory.IntelData(0, new byte[] { 1, 2 });
        var invalidData = validData[..^2] + "00";
        using var stream = TestRecordFactory.ToStream(invalidData, ":00000001FF");

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1));
    }

    [TestMethod]
    public void Intel_Checksum错误_关闭校验时可解析()
    {
        var validData = TestRecordFactory.IntelData(0, new byte[] { 1, 2 });
        var invalidData = validData[..^2] + "00";
        using var stream = TestRecordFactory.ToStream(invalidData, ":00000001FF");

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1, validateChecksums: false);

        Assert.AreEqual(0ul, doc.StartAddress);
        Assert.AreEqual(1ul, doc.EndAddress);
    }

    [TestMethod]
    public void Intel_结构错误_关闭校验时仍抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(":0200000001FD", TestRecordFactory.IntelEndOfFile());

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 1, validateChecksums: false));
    }

    [TestMethod]
    public void Motorola_Checksum错误_启用校验时抛出异常()
    {
        var validData = TestRecordFactory.SRecord('1', 0, new byte[] { 1, 2 });
        var invalidData = validData[..^2] + "00";
        using var stream = TestRecordFactory.ToStream(invalidData, TestRecordFactory.SRecord('9', 0, []));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1));
    }

    [TestMethod]
    public void Motorola_Checksum错误_关闭校验时可解析()
    {
        var validData = TestRecordFactory.SRecord('1', 0, new byte[] { 1, 2 });
        var invalidData = validData[..^2] + "00";
        using var stream = TestRecordFactory.ToStream(invalidData, TestRecordFactory.SRecord('9', 0, []));

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1, validateChecksums: false);

        Assert.AreEqual(0ul, doc.StartAddress);
        Assert.AreEqual(1ul, doc.EndAddress);
    }

    [TestMethod]
    public void Motorola_结构错误_关闭校验时仍抛出异常()
    {
        var validData = TestRecordFactory.SRecord('1', 0, new byte[] { 1, 2 });
        var invalidData = validData[..2] + "06" + validData[4..];
        using var stream = TestRecordFactory.ToStream(invalidData, TestRecordFactory.SRecord('9', 0, []));

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, dataSize: 1, validateChecksums: false));
    }
}

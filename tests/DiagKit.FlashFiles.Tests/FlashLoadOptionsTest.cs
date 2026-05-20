using DiagKit.FlashFiles.Define.Enumerates;

namespace DiagKit.FlashFiles.Tests;

/// <summary>
/// Flash 加载选项测试。
/// </summary>
[TestClass]
public class FlashLoadOptionsTest
{
    [TestMethod]
    public void Intel_默认选项_缺少EOF记录时抛出异常()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1);

        Assert.ThrowsExactly<FormatException>(
            () => FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options));
    }

    [TestMethod]
    public void Intel_关闭RequireEndOfFile_缺少EOF记录时可解析已有数据()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            RequireEndOfFile = false,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options);

        Assert.AreEqual(0ul, doc.StartAddress);
        Assert.AreEqual(0ul, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0).ToArray());
    }

    [TestMethod]
    public void Intel_EOF之后记录_Ignore时忽略后续记录()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }),
            TestRecordFactory.IntelEndOfFile(),
            TestRecordFactory.IntelData(0x0001, new byte[] { 0xBB }));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            RecordsAfterEndOfFileBehavior = FlashFiles.RecordsAfterEndOfFileBehavior.Ignore,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options);

        Assert.AreEqual(0ul, doc.StartAddress);
        Assert.AreEqual(0ul, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0).ToArray());
    }

    [TestMethod]
    public void Intel_EOF之后记录_Parse时继续解析后续记录()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }),
            TestRecordFactory.IntelEndOfFile(),
            TestRecordFactory.IntelData(0x0001, new byte[] { 0xBB }));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            RecordsAfterEndOfFileBehavior = FlashFiles.RecordsAfterEndOfFileBehavior.Parse,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options);

        Assert.AreEqual(0ul, doc.StartAddress);
        Assert.AreEqual(1ul, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, doc.ReadRange(0, 1).ToArray());
    }

    [TestMethod]
    public void Intel_关闭ValidateNonDataRecordAddress_EOF地址非零时可解析()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }),
            TestRecordFactory.IntelRecord(1, 0x1234, []));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            ValidateNonDataRecordAddress = false,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options);

        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0).ToArray());
    }

    [TestMethod]
    public void Intel_关闭ValidateRecordTypeLength_扩展地址记录长度不规范时可解析()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelRecord(4, 0, new byte[] { 0x00, 0x01, 0xFF }),
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            ValidateRecordTypeLength = false,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options);

        Assert.AreEqual(0x00010000UL, doc.StartAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0x00010000).ToArray());
    }

    [TestMethod]
    public void Intel_新选项关闭ValidateChecksums_校验错误时可解析()
    {
        var validData = TestRecordFactory.IntelData(0, new byte[] { 0xAA });
        using var stream = TestRecordFactory.ToStream(WithInvalidChecksum(validData), TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            ValidateChecksums = false,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options);

        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0).ToArray());
    }

    [TestMethod]
    public void Intel_关闭ValidateDataRecordLength_数据长度不对齐时补齐()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.IntelData(0x0000, new byte[] { 0xAA, 0xBB, 0xCC }),
            TestRecordFactory.IntelEndOfFile());
        var options = new FlashFiles.FlashLoadOptions(dataSize: 2)
        {
            ValidateDataRecordLength = false,
            DataRecordPaddingValue = 0xFF,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, options);

        Assert.AreEqual(4ul, doc.ByteCount);
        Assert.AreEqual(2ul, doc.AddressCount);
        Assert.AreEqual(1ul, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xFF }, doc.ReadRange(0, 1).ToArray());
    }

    [TestMethod]
    public void Motorola_关闭RequireEndOfFile_缺少结束记录时可解析已有数据()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA }));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            RequireEndOfFile = false,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, options);

        Assert.AreEqual(0x1000UL, doc.StartAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0x1000).ToArray());
    }

    [TestMethod]
    public void Motorola_结束记录之后记录_Ignore时忽略后续记录()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA }),
            TestRecordFactory.SRecord('9', 0, []),
            TestRecordFactory.SRecord('1', 0x1001, new byte[] { 0xBB }));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            RecordsAfterEndOfFileBehavior = FlashFiles.RecordsAfterEndOfFileBehavior.Ignore,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, options);

        Assert.AreEqual(0x1000UL, doc.StartAddress);
        Assert.AreEqual(0x1000UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0x1000).ToArray());
    }

    [TestMethod]
    public void Motorola_结束记录之后记录_Parse时继续解析后续记录()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA }),
            TestRecordFactory.SRecord('9', 0, []),
            TestRecordFactory.SRecord('1', 0x1001, new byte[] { 0xBB }));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            RecordsAfterEndOfFileBehavior = FlashFiles.RecordsAfterEndOfFileBehavior.Parse,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, options);

        Assert.AreEqual(0x1000UL, doc.StartAddress);
        Assert.AreEqual(0x1001UL, doc.EndAddress);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, doc.ReadRange(0x1000, 0x1001).ToArray());
    }

    [TestMethod]
    public void Motorola_关闭ValidateMotorolaHeaderPosition_S0在数据后时可解析()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('1', 0x1000, new byte[] { 0xAA }),
            TestRecordFactory.SRecord('0', 0, new byte[] { 0x01, 0x02 }),
            TestRecordFactory.SRecord('9', 0, []));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            ValidateMotorolaHeaderPosition = false,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, options);

        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0x1000).ToArray());
    }

    [TestMethod]
    public void Motorola_关闭ValidateMotorolaCountRecord_S6计数不匹配时可解析()
    {
        using var stream = TestRecordFactory.ToStream(
            TestRecordFactory.SRecord('2', 0x100000, new byte[] { 0xAA }),
            TestRecordFactory.SRecord('6', 2, []),
            TestRecordFactory.SRecord('8', 0, []));
        var options = new FlashFiles.FlashLoadOptions(dataSize: 1)
        {
            ValidateMotorolaCountRecord = false,
        };

        using var doc = FlashFiles.FlashDocument.Load(stream, FlashFileType.Motorola_S_Record, options);

        CollectionAssert.AreEqual(new byte[] { 0xAA }, doc.ReadAt(0x100000).ToArray());
    }

    private static string WithInvalidChecksum(string record)
        => record[..^2] + (record.EndsWith("00", StringComparison.Ordinal) ? "01" : "00");
}

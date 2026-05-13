using DiagKit.FlashFiles.Common.Utilities;

namespace DiagKit.FlashFiles.Tests.Common.Utilities;

/// <summary>
/// 十六进制转换工具测试。
/// </summary>
[TestClass]
public class HexConverterTest
{
    [TestMethod]
    public void FromHexString_已知十六进制字符串_转换正确()
    {
        var hex = "25800400001D03B29A91B00148D94400001D0002008208404F8209820A820BC81CC82D483C0A";
        var expected = new byte[]
        {
            0x25, 0x80, 0x04, 0x00, 0x00, 0x1D, 0x03, 0xB2,
            0x9A, 0x91, 0xB0, 0x01, 0x48, 0xD9, 0x44, 0x00,
            0x00, 0x1D, 0x00, 0x02, 0x00, 0x82, 0x08, 0x40,
            0x4F, 0x82, 0x09, 0x82, 0x0A, 0x82, 0x0B, 0xC8,
            0x1C, 0xC8, 0x2D, 0x48, 0x3C, 0x0A,
        };
        var data = new byte[hex.Length / 2];
        ByteConverter.FromHexString(hex, data);
        CollectionAssert.AreEqual(expected, data, "转换结果应与预期字节数组一致。");
    }

    [TestMethod]
    public void FromHexCharsToByte_有效字符_转换正确()
    {
        Assert.AreEqual(0x00, ByteConverter.FromHexCharsToByte("00".AsSpan()));
        Assert.AreEqual(0xFF, ByteConverter.FromHexCharsToByte("FF".AsSpan()));
        Assert.AreEqual(0xAB, ByteConverter.FromHexCharsToByte("ab".AsSpan()));
        Assert.AreEqual(0x12, ByteConverter.FromHexCharsToByte("12".AsSpan()));
    }

    [TestMethod]
    public void FromHexCharToByte_无效字符_抛出异常()
    {
        Assert.ThrowsExactly<FormatException>(
            () => ByteConverter.FromHexCharToByte('G'));
        Assert.ThrowsExactly<FormatException>(
            () => ByteConverter.FromHexCharToByte(' '));
    }

    [TestMethod]
    public void ReadUInt24BigEndian_有效数据_返回正确值()
    {
        Span<byte> data = [0x01, 0x02, 0x03];
        var result = ByteConverter.ReadUInt24BigEndian(data);
        Assert.AreEqual(0x010203u, result);
    }

    [TestMethod]
    public void FromHexString_奇数长度_抛出异常()
    {
        var data = new byte[10];
        Assert.ThrowsExactly<ArgumentException>(
            () => ByteConverter.FromHexString("ABC", data));
    }
}

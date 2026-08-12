using DiagKit.FlashFiles.Common.Utilities;

using System.Text;

namespace DiagKit.FlashFiles.Tests.Common.Utilities;

/// <summary>
/// CRC-32 校验计算器测试。
/// </summary>
[TestClass]
public class UdsCrc32Test
{
    [TestMethod]
    public void Compute_标准测试向量_返回公开CheckValue()
    {
        var data = Encoding.ASCII.GetBytes("123456789");

        Assert.AreEqual(0x0376E6E7u, UdsCrc32.Mpeg2.Compute(data), "CRC-32/MPEG-2 check value 不匹配。");
        Assert.AreEqual(0xCBF43926u, UdsCrc32.Standard.Compute(data), "CRC-32/ISO-HDLC check value 不匹配。");
        Assert.AreEqual(0x765E7680u, UdsCrc32.Posix.Compute(data), "CRC-32/POSIX check value 不匹配。");
        Assert.AreEqual(0x1697D06Au, UdsCrc32.AutoSar.Compute(data), "CRC-32/AUTOSAR check value 不匹配。");
    }

    [TestMethod]
    public void Compute_空数据_按Init和XorOut返回()
    {
        Assert.AreEqual(0xFFFFFFFFu, UdsCrc32.Mpeg2.Compute([]));
        Assert.AreEqual(0x00000000u, UdsCrc32.Standard.Compute([]));
        Assert.AreEqual(0xFFFFFFFFu, UdsCrc32.Posix.Compute([]));
        Assert.AreEqual(0x00000000u, UdsCrc32.AutoSar.Compute([]));
    }

    [TestMethod]
    public void 标准变体_单例实例一致()
    {
        Assert.AreSame(UdsCrc32.Mpeg2, UdsCrc32.Mpeg2, "Mpeg2 单例应一致。");
        Assert.AreSame(UdsCrc32.Standard, UdsCrc32.Standard, "Standard 单例应一致。");
        Assert.AreSame(UdsCrc32.Posix, UdsCrc32.Posix, "Posix 单例应一致。");
        Assert.AreSame(UdsCrc32.AutoSar, UdsCrc32.AutoSar, "AutoSar 单例应一致。");
    }
}

namespace DiagKit.FlashFiles.Tests.Common;

/// <summary>
/// FlashPage 索引器测试。
/// </summary>
[TestClass]
public class FlashPageTest
{
    [TestMethod]
    public void Index_有效索引_返回对应地址数据()
    {
        using var page = new FlashPage(4, 2);
        new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }.CopyTo(page.Data);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, page[0].ToArray());
        CollectionAssert.AreEqual(new byte[] { 7, 8 }, page[^1].ToArray());
    }

    [TestMethod]
    public void Index_越界索引_抛出异常()
    {
        using var page = new FlashPage(4, 2);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ReadIndex(page, 4));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ReadIndex(page, ^5));
    }

    [TestMethod]
    public void Range_有效范围_返回对应地址数据()
    {
        using var page = new FlashPage(4, 2);
        new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }.CopyTo(page.Data);

        CollectionAssert.AreEqual(new byte[] { 3, 4, 5, 6 }, page[1..3].ToArray());
        CollectionAssert.AreEqual(new byte[] { 3, 4, 5, 6 }, page[^3..^1].ToArray());
        Assert.AreEqual(0, page[^0..^0].Length);
    }

    [TestMethod]
    public void Range_无效范围_抛出异常()
    {
        using var page = new FlashPage(4, 2);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ReadRange(page, 3..1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ReadRange(page, 0..5));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ReadRange(page, ^5..^1));
    }

    [TestMethod]
    public void Data_释放后访问_抛出异常()
    {
        var page = new FlashPage(4, 2);
        page.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = page.Data.Length);
    }

    private static void ReadIndex(FlashPage page, Index index) => _ = page[index][0];

    private static void ReadRange(FlashPage page, Range range) => _ = page[range].Length;
}

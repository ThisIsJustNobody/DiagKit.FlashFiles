using DiagKit.FlashFiles.Define.Enumerates;

namespace DiagKit.FlashFiles.Tests;

/// <summary>
/// 并发解析线程安全测试。
/// </summary>
[TestClass]
public class ThreadSafetyTest
{
    [TestMethod]
    public async Task Parse_并发调用_不产生数据竞争()
    {
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            using var doc = FlashFiles.FlashDocument.Load(
                InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
            Assert.HasCount(8, doc.Blocks);
            Assert.IsTrue(doc.ContainsAddress(doc.StartAddress));
        }, TestContext.CancellationToken));
        await Task.WhenAll(tasks);
    }

    [TestMethod]
    public async Task Parse_Intel和Motorola并发_不产生数据竞争()
    {
        var task1 = Task.Run(() =>
        {
            using var doc = FlashFiles.FlashDocument.Load(
                InitializeTest.GetIntelHexStream(), FlashFileType.Intel_MCS_86, dataSize: 2);
            Assert.HasCount(8, doc.Blocks);
        }, TestContext.CancellationToken);
        var task2 = Task.Run(() =>
        {
            using var doc = FlashFiles.FlashDocument.Load(
                InitializeTest.GetMotorolaSRecordStream(), FlashFileType.Motorola_S_Record, dataSize: 1);
            Assert.HasCount(8, doc.Blocks);
        }, TestContext.CancellationToken);
        await Task.WhenAll(task1, task2);
    }

    public TestContext TestContext { get; set; }
}

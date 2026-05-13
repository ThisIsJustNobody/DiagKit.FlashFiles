using System.Text;

namespace DiagKit.FlashFiles.Tests;

/// <summary>
/// 测试程序集初始化，加载嵌入资源。
/// </summary>
[TestClass]
public static class InitializeTest
{
    internal static string Intel86HexSampleResource { get; private set; } = string.Empty;
    internal static string MotorolaSRecordResource { get; private set; } = string.Empty;

    internal static byte[] Intel86HexSampleBytes { get; private set; } = [];
    internal static byte[] MotorolaSRecordBytes { get; private set; } = [];

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        Intel86HexSampleBytes = Properties.Resources.F28035_Sample;
        MotorolaSRecordBytes = Properties.Resources.SRecordSample;
        Intel86HexSampleResource = Encoding.UTF8.GetString(Intel86HexSampleBytes);
        MotorolaSRecordResource = Encoding.UTF8.GetString(MotorolaSRecordBytes);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
    }

    /// <summary>获取 Intel HEX 嵌入资源的流。</summary>
    internal static Stream GetIntelHexStream()
        => new MemoryStream(Intel86HexSampleBytes);

    /// <summary>获取 Motorola S-Record 嵌入资源的流。</summary>
    internal static Stream GetMotorolaSRecordStream()
        => new MemoryStream(MotorolaSRecordBytes);
}

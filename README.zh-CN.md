# DiagKit.FlashFiles

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DiagKit.FlashFiles?label=NuGet&color=orange)](https://www.nuget.org/packages/DiagKit.FlashFiles)
[![.NET](https://img.shields.io/badge/.NET-8.0_10.0-purple.svg)](https://dotnet.microsoft.com/)

汽车诊断刷写文件解析库，支持 Intel MCS-86 HEX (`.hex`) 和 Motorola S-Record (`.s19`/`.s28`/`.s37`) 格式，适用于 ECU reflashing、UDS 下载流程和刷写数据校验场景。

## 安装

```powershell
dotnet add package DiagKit.FlashFiles
```

## 快速开始

```csharp
using DiagKit.FlashFiles;
using DiagKit.FlashFiles.Define.Enumerates;

// 从文件路径加载，按扩展名自动识别 .hex/.s19/.s28/.s37
using var doc = FlashDocument.Load("firmware.hex", dataSize: 2);

// 从 Stream 加载，需指定格式
using var fromStream = FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 2);

// 从内存字节加载，需指定格式
using var fromBytes = FlashDocument.Load(data, FlashFileType.Motorola_S_Record, dataSize: 1);
```

## 严格与兼容解析

默认解析行为保持严格。除非显式放宽，否则会验证校验和、Intel EOF 记录、Motorola 结束记录、记录类型长度、非数据记录地址字段、S-Record 头/计数记录以及 `DataSize` 对齐。

```csharp
// 与默认构造函数和既有 Load 重载行为一致。
var strict = FlashLoadOptions.Strict(dataSize: 2);

// 现场诊断：容忍常见格式噪声，但仍验证校验和与 DataSize 对齐。
var lenient = FlashLoadOptions.Lenient(dataSize: 2);

// 供应商文件兼容：EOF/结束记录后继续解析，并补齐不对齐的数据记录。
// 默认仍验证校验和。
var supplierCompatible = FlashLoadOptions.SupplierCompatible(dataSize: 2);

using var supplier = FlashDocument.Load("supplier.hex", supplierCompatible);
```

高级调用方仍可调整单个解析细项：

```csharp
var options = FlashLoadOptions.SupplierCompatible(dataSize: 2);
options.ValidateChecksums = false; // 仅建议用于现场排查或可信供应商文件恢复。
options.DataRecordPaddingValue = 0xFF;

using var lenientDocument = FlashDocument.Load("supplier.hex", options);
```

`RecordsAfterEndOfFileBehavior.Reject` 保持默认标准行为；`Ignore` 在首次 EOF/结束记录后停止解析；`Parse` 会继续解析其后的有效记录。

## 数据所有权

`FlashDocument`、`FlashBlock` 和 `FlashPage` 使用池化内存并实现 `IDisposable`。`Span` 与 `ReadOnlyMemory<byte>` 视图只在拥有对象存活期间有效。数据需要跨服务/UI 边界传递，或需要在文档释放后继续使用时，建议导出 DTO：

```csharp
using var doc = FlashDocument.Load("firmware.hex", dataSize: 2);

IReadOnlyList<FlashBlockDto> ownedBlocks = doc.ToBlockDtos();
byte[] firstBlockData = ownedBlocks[0].Data;
```

每个 `FlashBlockDto` 都拥有复制后的 `byte[]`，不需要调用 `Dispose`。

## 从内存创建文档

当刷写载荷已经是带地址的原始字节数据时，例如从 UDS `TransferData` 记录还原出的数据，可以使用 `FlashDocument.Create(...)` 创建文档。该 API 会复制输入数据、按地址排序、合并相邻块，并拒绝重叠地址块或混用 `DataSize` 的输入。

```csharp
var appBlock = new FlashBlockDto(
    startAddress: 0xA0100000,
    data: appPayload,
    dataSize: 1);

using var doc = FlashDocument.Create(new[] { appBlock });
using var output = File.Create("app.hex");
doc.Save(output, FlashFileType.Intel_MCS_86);
```

## 核心 API

```csharp
doc.StartAddress   // 起始地址
doc.EndAddress     // 结束地址
doc.ByteCount      // 总字节数，ulong
doc.AddressCount   // 总地址数，ulong
doc.DataSize       // 每地址字节数
doc.Blocks         // IReadOnlyList<FlashBlock>

// O(log n) 地址查找
var exists = doc.ContainsAddress(0x003E8500);
var word = doc.ReadAt(0x003E8500);
var range = doc.ReadRange(0x003E8500, 0x003E850F);

// 修改数据
doc.WriteAt(0x003E8500, new byte[] { 0xAA, 0xBB });
doc.WriteRange(0x003E8500, 0x003E8501, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

// UDS DTO 导出。返回块拥有复制后的数据，不需要 Dispose。
var udsBlocks = doc.ToUdsBlockDtos(new FlashUdsExportOptions(maxBlockByteCount: 0x400)
{
    PaddingValue = 0xFF,
    FillGaps = true,
    SkipBlankBlocks = true,
    RequireUInt32Address = true,
});

// 底层 UDS 页填充
using var page = new FlashPage(addressCount: 0x100, dataSize: 2);
var hasData = doc.TryFillPage(page, doc.StartAddress);

// 迭代页，调用方负责释放每个 FlashPage
foreach (var item in doc.EnumeratePages(0x100, doc.StartAddress, doc.EndAddress))
{
    using (item)
    {
        // 处理 item.Data
    }
}

// 过滤空白页
var pages = doc.EnumeratePages(0x100, doc.StartAddress, doc.EndAddress).ToList();
var filtered = FlashDocument.FilterPages(pages, skipLeadingBlank: true, skipMiddleBlank: true, skipTrailingBlank: true);
```

## 保存 Intel HEX

当前支持写出 Intel MCS-86 HEX。Motorola S-Record 写入暂未实现。

```csharp
using var output = File.Create("firmware.hex");
doc.Save(output, FlashFileType.Intel_MCS_86);

doc.SaveToFile("firmware.hex");
```

## CRC-32

```csharp
using DiagKit.FlashFiles.Common.Utilities;

uint mpeg2 = UdsCrc32.Mpeg2.Compute(data);
uint standard = UdsCrc32.Standard.Compute(data);
uint posix = UdsCrc32.Posix.Compute(data);
uint autosar = UdsCrc32.AutoSar.Compute(data);
```

## 构建与测试

```powershell
dotnet build .\src\DiagKit.FlashFiles\DiagKit.FlashFiles.csproj
dotnet test .\tests\DiagKit.FlashFiles.Tests\DiagKit.FlashFiles.Tests.csproj
dotnet pack .\src\DiagKit.FlashFiles\DiagKit.FlashFiles.csproj -c Release
```

## 项目信息

- 目标框架: `net8.0`, `net10.0`
- 许可证: MIT
- 包标识: `DiagKit.FlashFiles`
- 依赖: 无

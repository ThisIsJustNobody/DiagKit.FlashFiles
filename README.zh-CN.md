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

// UDS 页填充
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

# DiagKit.FlashFiles

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DiagKit.FlashFiles?label=NuGet&color=orange)](https://www.nuget.org/packages/DiagKit.FlashFiles)
[![.NET](https://img.shields.io/badge/.NET-8.0_10.0-purple.svg)](https://dotnet.microsoft.com/)

An automotive diagnostic flash file parsing library, supporting Intel MCS-86 HEX (`.hex`) and Motorola S-Record (`.s19`/`.s28`/`.s37`) formats for ECU reflashing, UDS download flows, and flash data verification.

## Installation

```powershell
dotnet add package DiagKit.FlashFiles
```

## Quick Start

```csharp
using DiagKit.FlashFiles;
using DiagKit.FlashFiles.Define.Enumerates;

// Load from file path — auto-detects .hex/.s19/.s28/.s37 by extension
using var doc = FlashDocument.Load("firmware.hex", dataSize: 2);

// Load from Stream — format must be specified
using var fromStream = FlashDocument.Load(stream, FlashFileType.Intel_MCS_86, dataSize: 2);

// Load from in-memory bytes — format must be specified
using var fromBytes = FlashDocument.Load(data, FlashFileType.Motorola_S_Record, dataSize: 1);
```

## Core API

```csharp
doc.StartAddress   // Start address
doc.EndAddress     // End address
doc.ByteCount      // Total bytes (ulong)
doc.AddressCount   // Total addresses (ulong)
doc.DataSize       // Bytes per address
doc.Blocks         // IReadOnlyList<FlashBlock>

// O(log n) address lookup
var exists = doc.ContainsAddress(0x003E8500);
var word = doc.ReadAt(0x003E8500);
var range = doc.ReadRange(0x003E8500, 0x003E850F);

// Modify data
doc.WriteAt(0x003E8500, new byte[] { 0xAA, 0xBB });
doc.WriteRange(0x003E8500, 0x003E8501, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

// UDS page filling
using var page = new FlashPage(addressCount: 0x100, dataSize: 2);
var hasData = doc.TryFillPage(page, doc.StartAddress);

// Enumerate pages — caller is responsible for disposing each FlashPage
foreach (var item in doc.EnumeratePages(0x100, doc.StartAddress, doc.EndAddress))
{
    using (item)
    {
        // Process item.Data
    }
}

// Filter blank pages
var pages = doc.EnumeratePages(0x100, doc.StartAddress, doc.EndAddress).ToList();
var filtered = FlashDocument.FilterPages(pages, skipLeadingBlank: true, skipMiddleBlank: true, skipTrailingBlank: true);
```

## Save as Intel HEX

Intel MCS-86 HEX output is currently supported. Motorola S-Record writing is not yet implemented.

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

## Build & Test

```powershell
dotnet build .\src\DiagKit.FlashFiles\DiagKit.FlashFiles.csproj
dotnet test .\tests\DiagKit.FlashFiles.Tests\DiagKit.FlashFiles.Tests.csproj
dotnet pack .\src\DiagKit.FlashFiles\DiagKit.FlashFiles.csproj -c Release
```

## Project Info

- Target frameworks: `net8.0`, `net10.0`
- License: MIT
- Package ID: `DiagKit.FlashFiles`
- Dependencies: none

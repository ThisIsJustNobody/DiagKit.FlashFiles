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

## Strict and Lenient Parsing

Parsing is strict by default. Checksums, Intel EOF records, Motorola termination records, record type lengths, non-data record address rules, S-Record header/count rules, and `DataSize` alignment are validated unless explicitly relaxed.

```csharp
// Same behavior as the default constructor and existing Load overloads.
var strict = FlashLoadOptions.Strict(dataSize: 2);

// Field diagnostics: tolerate common format noise, but still validate checksums and DataSize alignment.
var lenient = FlashLoadOptions.Lenient(dataSize: 2);

// Supplier compatibility: continue after EOF/termination and pad unaligned data records.
// Checksum validation remains enabled by default.
var supplierCompatible = FlashLoadOptions.SupplierCompatible(dataSize: 2);

using var supplier = FlashDocument.Load("supplier.hex", supplierCompatible);
```

Advanced callers can still tune individual parser behaviors:

```csharp
var options = FlashLoadOptions.SupplierCompatible(dataSize: 2);
options.ValidateChecksums = false; // Use only for field diagnostics or trusted supplier recovery.
options.DataRecordPaddingValue = 0xFF;

using var lenientDocument = FlashDocument.Load("supplier.hex", options);
```

`RecordsAfterEndOfFileBehavior.Reject` preserves the default standard behavior, `Ignore` stops at the first EOF/termination record, and `Parse` continues reading valid records after it.

## Data Ownership

`FlashDocument`, `FlashBlock`, and `FlashPage` use pooled memory and implement `IDisposable`. Spans and `ReadOnlyMemory<byte>` views are only valid while the owning object is alive. Use DTO export when data needs to cross service/UI boundaries or outlive the document:

```csharp
using var doc = FlashDocument.Load("firmware.hex", dataSize: 2);

IReadOnlyList<FlashBlockDto> ownedBlocks = doc.ToBlockDtos();
byte[] firstBlockData = ownedBlocks[0].Data;
```

Each `FlashBlockDto` owns a copied `byte[]` and does not need to be disposed.

## Create Documents from Memory

Use `FlashDocument.Create(...)` when flash payloads already exist as raw address-mapped bytes, such as data reconstructed from UDS `TransferData` records. This API copies input data, sorts blocks by address, merges adjacent blocks, and rejects overlapping blocks or mixed `DataSize` values.

```csharp
var appBlock = new FlashBlockDto(
    startAddress: 0xA0100000,
    data: appPayload,
    dataSize: 1);

using var doc = FlashDocument.Create(new[] { appBlock });
using var output = File.Create("app.hex");
doc.Save(output, FlashFileType.Intel_MCS_86);
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

// UDS DTO export. Blocks own copied data and do not require Dispose.
var udsBlocks = doc.ToUdsBlockDtos(new FlashUdsExportOptions(maxBlockByteCount: 0x400)
{
    PaddingValue = 0xFF,
    FillGaps = true,
    SkipBlankBlocks = true,
    RequireUInt32Address = true,
});

// Low-level UDS page filling
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

## Save as Intel HEX or Motorola S-Record

Intel MCS-86 HEX and Motorola S-Record output are supported. `SaveToFile` maps `.s19` to S1/S9 records, `.s28` to S2/S8 records, and `.s37` to S3/S7 records. Stream saves with `FlashFileType.Motorola_S_Record` automatically choose the smallest S-Record address width that can contain the document's highest address.

```csharp
using var hexOutput = File.Create("firmware.hex");
doc.Save(hexOutput, FlashFileType.Intel_MCS_86);

using var sRecordOutput = new MemoryStream();
doc.Save(sRecordOutput, FlashFileType.Motorola_S_Record); // Auto-selects S1/S2/S3; no file extension is inspected.

doc.SaveToFile("firmware.hex");
doc.SaveToFile("firmware.s19");
doc.SaveToFile("firmware.s28");
doc.SaveToFile("firmware.s37");
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

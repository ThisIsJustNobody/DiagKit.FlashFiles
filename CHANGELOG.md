# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

- Added `FlashDocument.Create(...)` factory methods for raw address-mapped memory data, including block sorting, adjacent-block merging, overlap rejection, and copied data ownership.
- Added Intel HEX save validation for addresses beyond the 32-bit linear address range.
- Added `FlashLoadOptions.Strict`, `Lenient`, and `SupplierCompatible` profile factory methods.
- Added `FlashBlockDto` plus `FlashBlock.ToDto()` and `FlashDocument.ToBlockDtos()` for owned data export.
- Added `FlashUdsExportOptions` and `FlashDocument.ToUdsBlockDtos(...)` for UDS-sized owned block export with gap filling, blank-page skipping, and 32-bit address checks.
- Added Motorola S-Record saving for streams and `.s19`/`.s28`/`.s37` files, including S1/S2/S3 address-width selection, S5/S6 count records, and strict round-trip validation.
- Documented pooled-memory lifetime rules and DTO copy semantics.

## 1.0.1-preview.1 - 2026-05-20

- Added `FlashLoadOptions` and `RecordsAfterEndOfFileBehavior` for configurable strict and lenient parsing.
- Added option-based `FlashDocument.Load` overloads for file paths, streams, and in-memory bytes.
- Kept existing Load API and default parsing behavior strict and backward compatible.
- Added explicit compatibility controls for EOF/termination requirements, records after EOF, checksums, Intel non-data record addresses, record type lengths, DataSize alignment, and Motorola S-Record header/count validation.
- Documented strict defaults and lenient field-diagnostics usage.

## 1.0.0 - 2026-05-13

- Initial stable release under the `DiagKit.FlashFiles` package identity.
- Supports Intel MCS-86 HEX and Motorola S-Record parsing.
- Supports Intel HEX saving.
- Provides address lookup, range reading/writing, page enumeration, page filtering, and CRC-32 helpers.
- Targets `net8.0` and `net10.0`.

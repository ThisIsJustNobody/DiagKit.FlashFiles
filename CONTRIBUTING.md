# Contributing

Thanks for helping improve `DiagKit.FlashFiles`.

## Development Setup

Install a current .NET SDK that can build `net8.0` and `net10.0`, then run:

```powershell
dotnet restore
dotnet test .\tests\DiagKit.FlashFiles.Tests\DiagKit.FlashFiles.Tests.csproj
```

## Pull Request Expectations

- Keep public API changes intentional and documented in `README.md`.
- Add or update tests for parser behavior, address calculations, page enumeration, and writer output.
- Keep comments in Chinese when editing source code.
- Do not introduce shared mutable parser state.
- Do not commit generated NuGet packages, build output, or local IDE files.

## Release Checklist

```powershell
dotnet test .\tests\DiagKit.FlashFiles.Tests\DiagKit.FlashFiles.Tests.csproj
dotnet pack .\src\DiagKit.FlashFiles\DiagKit.FlashFiles.csproj -c Release
```

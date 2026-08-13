# Getting Started

[English](getting-started.md) | [Portugues (Brasil)](../pt-BR/getting-started.md)

This page explains how to build, test, pack, and consume the isolated analyzer workspace through Phase 4.

## Prerequisites

- .NET SDK `10.0.100`, or a compatible SDK selected by `analyzer/global.json`.
- Git.
- A shell capable of running `dotnet` commands.

The analyzer project targets `netstandard2.0` because Roslyn analyzers are loaded by compiler and IDE hosts, not by the runtime target of the application being analyzed. The test project targets `net10.0`.

## Clone and Build

From the repository root:

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

The solution is isolated under `analyzer/`. Files outside that directory belong to the inherited implementation and are reference-only for this analyzer workspace.

## Pack

Create a local analyzer package:

```bash
cd analyzer
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.4.0-phase4-local
```

The package is a Roslyn analyzer package. The analyzer assembly is packed under:

```text
analyzers/dotnet/cs/
```

The project sets `PackageReadmeFile` to `README.md`, and the packed README is the English `analyzer/README.md`.

## Local Consumption

The package is currently consumed from a local package source. Do not treat this documentation as evidence of a NuGet.org publication.

One local workflow is:

```bash
cd analyzer
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.4.0-phase4-local --output artifacts/local-packages
dotnet new console -o artifacts/tmp/AnalyzerConsumer
cd artifacts/tmp/AnalyzerConsumer
dotnet nuget add source ../../local-packages --name complexity-analysis-local
dotnet add package ComplexityAnalysis.Analyzers --version 0.4.0-phase4-local --source complexity-analysis-local
```

If needed, point the local NuGet source at the directory containing the generated `.nupkg`.

## PackageReference

Analyzer packages are normally referenced with `PrivateAssets="all"` so they affect compilation but do not become transitive dependencies of consuming projects:

```xml
<PackageReference
    Include="ComplexityAnalysis.Analyzers"
    Version="<local-or-published-version>"
    PrivateAssets="all" />
```

The analyzer is not a runtime library. Application code does not call its types.

## Smoke Test Diagnostics

`BIG1001`, `BIG1002`, and `BIG1003` are enabled by default as `Info` diagnostics. Build output visibility depends on the consuming project and SDK settings. You can promote one locally:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

`BIG0001` is disabled by default. Enable it when you want method complexity estimates:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

`BIG9000` is disabled by default. To prove the analyzer loaded, enable it temporarily:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Disable the probe after the smoke test:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

See [Configuration](configuration.md) for details.

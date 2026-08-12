# Getting Started

[English](getting-started.md) | [Português (Brasil)](../pt-BR/getting-started.md)

This page explains how to build, test, pack, and consume the isolated analyzer workspace through Phase 3.

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
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.3.0-docs-local
```

The package is a Roslyn analyzer package. The analyzer assembly is packed under:

```text
analyzers/dotnet/cs/
```

The project sets `PackageReadmeFile` to `README.md`, and the packed README is the English `analyzer/README.md`.

## Local Consumption

The package is currently consumed from a local build/package source. Do not treat this documentation as evidence of a NuGet.org publication.

One local workflow is:

```bash
cd analyzer
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.3.0-docs-local --output artifacts/local-packages
dotnet new console -o artifacts/tmp/AnalyzerConsumer
cd artifacts/tmp/AnalyzerConsumer
dotnet nuget add source ../../local-packages --name complexity-analysis-local
dotnet add package ComplexityAnalysis.Analyzers --version 0.3.0-docs-local --source complexity-analysis-local
```

The exact package output directory can vary by SDK and repository configuration. If needed, point the local NuGet source at the directory containing the generated `.nupkg`.

## PackageReference

Analyzer packages are normally referenced with `PrivateAssets="all"` so they affect compilation but do not become transitive dependencies of consuming projects:

```xml
<PackageReference
    Include="ComplexityAnalysis.Analyzers"
    Version="<local-or-published-version>"
    PrivateAssets="all" />
```

The analyzer is not a runtime library. Application code does not call its types.

```text
application
    |
    | compiled by
    v
Roslyn compiler
    |
    | loads
    v
ComplexityAnalysis.Analyzers
```

## Smoke Test With BIG9000

`BIG9000` is disabled by default. To prove the analyzer was loaded by a consumer project, create or edit `.editorconfig` in the consumer:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Then build the consumer project. If `BIG9000` appears, it does not mean your code has a problem. It means the execution probe was explicitly enabled and the analyzer successfully ran.

After the smoke test, disable it again:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

See [Configuration](configuration.md) for details.

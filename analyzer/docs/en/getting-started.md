# Getting Started

[English](getting-started.md) | [Portugues (Brasil)](../pt-BR/getting-started.md)

This page explains how to build, test, pack, and consume the isolated analyzer workspace through Phase 7.

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
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

The package is a Roslyn analyzer package. The analyzer assembly is packed under:

```text
analyzers/dotnet/cs/
```

The project sets `PackageReadmeFile` to `README.md`, and the packed README is the English `analyzer/README.md`.
The package uses the repository URL as project metadata, declares the repository type as `git`, and uses the repository MIT license declaration as a NuGet license expression.

The package intentionally has no runtime `lib/` asset and no transitive Roslyn dependency. Package contract tests inspect the generated `.nupkg` as a ZIP archive.

`.snupkg` generation is not enabled for the current analyzer layout because the package keeps the analyzer DLL out of conventional build output and under `analyzers/dotnet/cs/`. Source Link build tooling is provided by the current .NET SDK, so no Source Link package reference is required.

## Local Consumption

The package is currently consumed from a local package source. Do not treat this documentation as evidence of a NuGet.org publication.

From the repository root, one PowerShell local workflow is:

```powershell
cd analyzer
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
$packageSource = (Resolve-Path "artifacts/local-packages").Path
$consumer = Join-Path ([System.IO.Path]::GetTempPath()) ("AnalyzerConsumer-" + [System.Guid]::NewGuid().ToString("N"))
dotnet new console -o $consumer
cd $consumer
dotnet new nugetconfig
dotnet nuget add source $packageSource --name complexity-analysis-local --configfile NuGet.config
dotnet add package ComplexityAnalysis.Analyzers --version 0.0.0-local --source $packageSource
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

`BIG1001`, `BIG1002`, `BIG1003`, `BIG1004`, `BIG1005`, and `BIG1006` are enabled by default as `Info` diagnostics. `BIG1006` still needs a configured `maximum_complexity` threshold before it can report. Build output visibility depends on the consuming project and SDK settings. You can promote one locally:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

Promote the Phase 5 source-call loop diagnostic:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

Promote the exponential-recursion diagnostic:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

Configure and promote the Phase 7 complexity-threshold diagnostic:

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

`BIG0001` is disabled by default. Enable it when you want method complexity estimates, including supported source-method costs and solved direct recursion:

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

## Compatibility Matrix

CI validates analyzer package consumption on supported SDK hosts:

| SDK host | Consumer target framework |
| --- | --- |
| .NET 8 LTS | `net8.0` |
| .NET 9 STS | `net9.0` |
| .NET 10 LTS | `net10.0` |

The analyzer assembly targets `netstandard2.0`, but the compatibility check is about compiler hosts loading the analyzer package, not only about the consumer target framework.

## Performance Validation

From `analyzer/`, run the structural performance harness:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
```

After restore, get the compiler analyzer execution report:

```bash
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Timing varies by hardware and runner. The useful gate is that the synthetic workload completes, structural invariants hold, and the compiler report includes `ComplexityAnalysis.Analyzers.ComplexityAnalyzer`.

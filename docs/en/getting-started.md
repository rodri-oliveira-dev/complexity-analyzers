# Getting Started

[English](getting-started.md) | [Português (Brasil)](../pt-BR/getting-started.md)

This guide explains how to build, test, pack, and validate `ComplexityAnalysis.Analyzers` from the current repository layout.

## Prerequisites

- .NET SDK `10.0.400`, or a compatible SDK selected by the root `global.json`.
- Git.
- A shell capable of running `dotnet` commands.

The analyzer project targets `netstandard2.0` because Roslyn analyzers are loaded by compiler and IDE hosts rather than by the runtime target of the application being analyzed. The repository tests and tooling use the SDK selected by `global.json`.

The repository build SDK is not the minimum consumer host version. Build and test tooling currently use SDK `10.0.400`; package compatibility is validated separately by installing the generated `.nupkg` into consumer projects built by supported SDK hosts.

## Clone and build

From the repository root:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

The analyzer is the product represented by the repository root. Production code lives under `src/`, tests under `tests/`, performance validation under `performance/`, and documentation under `docs/`.

## Create a local package

Build first, then create a local NuGet package:

```bash
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj \
  --configuration Release \
  --no-build \
  -p:PackageVersion=0.0.0-local \
  --output artifacts/local-packages
```

The generated package is a Roslyn analyzer package. `ComplexityAnalysis.Analyzers.dll` is packed under:

```text
analyzers/dotnet/cs/
```

It is intentionally not packed as a normal runtime library under `lib/`. Roslyn authoring dependencies are private assets and are not intended to become transitive dependencies of consuming projects.

The project uses `README.md` as the package readme and declares the repository URL, repository type, and MIT package license metadata.

The package contract also expects no `.deps.json`, no duplicate analyzer DLL, no runtime `lib/` asset for the analyzer assembly, and no transitive Roslyn dependency group in the generated `.nuspec`.

## Consume the local package

The repository documents local package creation and consumption. Do not treat this guide as evidence that a version has been published to NuGet.org.

One PowerShell workflow is:

```powershell
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj `
  --configuration Release `
  --no-build `
  -p:PackageVersion=0.0.0-local `
  --output artifacts/local-packages

$packageSource = (Resolve-Path "artifacts/local-packages").Path
$consumer = Join-Path ([System.IO.Path]::GetTempPath()) ("AnalyzerConsumer-" + [System.Guid]::NewGuid().ToString("N"))

dotnet new console -o $consumer
Set-Location $consumer
dotnet new nugetconfig
dotnet nuget add source $packageSource --name complexity-analysis-local --configfile NuGet.config
dotnet add package ComplexityAnalysis.Analyzers --version 0.0.0-local --source $packageSource
```

If needed, point the local NuGet source directly at the directory containing the generated `.nupkg`.

## PackageReference

Analyzer packages are normally referenced with `PrivateAssets="all"` so they participate in compilation without becoming a transitive runtime dependency:

```xml
<PackageReference
    Include="ComplexityAnalysis.Analyzers"
    Version="<local-or-published-version>"
    PrivateAssets="all" />
```

Consumer application code does not call analyzer types at runtime.

## Verify the analyzer is running

`BIG9000` is an opt-in infrastructure probe. Enable it temporarily when you need to prove that the package loaded and the analyzer executed:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Disable it again after the smoke test:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## Try the diagnostics

`BIG1001` through `BIG1005` are enabled by default with `Info` severity. `BIG1006` and `BIG2001` through `BIG2006` are also enabled as descriptors, but they only report after concrete thresholds are configured. Build visibility for informational diagnostics depends on the consuming project and SDK settings.

Promote an actionable diagnostic locally:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

Promote the source-call-in-loop diagnostic:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

Promote exponential recursion:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

Configure a maximum-complexity threshold:

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

Promote the Cyclomatic Complexity threshold diagnostic:

```ini
[*.cs]

complexity_analyzers.maximum_cyclomatic_complexity = 10
complexity_analyzers.cyclomatic_complexity_mode = standard
dotnet_diagnostic.BIG2001.severity = warning
```

Promote the Maximum Control-Flow Nesting Depth threshold diagnostic:

```ini
[*.cs]

complexity_analyzers.maximum_nesting_depth = 3
dotnet_diagnostic.BIG2002.severity = warning
```

Promote method-size threshold diagnostics:

```ini
[*.cs]

complexity_analyzers.maximum_method_nloc = 40
complexity_analyzers.maximum_statement_count = 25
complexity_analyzers.maximum_token_count = 300
complexity_analyzers.maximum_parameters = 5
dotnet_diagnostic.BIG2003.severity = warning
dotnet_diagnostic.BIG2004.severity = warning
dotnet_diagnostic.BIG2005.severity = warning
dotnet_diagnostic.BIG2006.severity = warning
```

`BIG0001` is disabled by default. Enable it when you want method-level complexity estimates:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

See [Configuration](configuration.md) and the [Analyzer Catalog](analyzers.md) for the complete behavior of each option and rule.

## Compatibility matrix

CI validates local package consumption on the supported SDK hosts:

| SDK host | Consumer target framework |
| --- | --- |
| .NET 8 LTS | `net8.0` |
| .NET 9 STS | `net9.0` |
| .NET 10 LTS | `net10.0` |

The analyzer assembly itself targets `netstandard2.0`. The compatibility matrix verifies that compiler hosts can load and execute the analyzer package, not merely that the consumer project can target a given framework.

The analyzer is compiled against `Microsoft.CodeAnalysis.CSharp` `4.8.0`, which resolves `Microsoft.CodeAnalysis.Common` `4.8.0`. This is a conservative Roslyn host baseline: dependency upgrades require package inspection and successful consumer execution across the supported host matrix before merge. `Microsoft.CodeAnalysis.Workspaces` is intentionally absent because the package does not provide code fixes or workspace-based IDE features.

## Performance validation

Run the structural performance harness from the repository root:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx \
  --configuration Release \
  --no-build \
  --filter PerformanceSyntheticCorpusTests
```

After restore, request the compiler analyzer execution report:

```bash
dotnet build performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj \
  --configuration Release \
  --no-restore \
  -t:Rebuild \
  -p:ReportAnalyzer=true \
  -p:UseSharedCompilation=false \
  -v:detailed
```

Timing varies by hardware and CI runner. The repeatable validation is that the synthetic workload completes, structural invariants hold, and the compiler report contains `ComplexityAnalysis.Analyzers.ComplexityAnalyzer`.

## Next steps

- Read the [Analyzer Catalog](analyzers.md) to understand each diagnostic.
- Read [Configuration](configuration.md) to tune analysis budgets and severities.
- Read [Architecture](architecture.md) for the analyzer pipeline and design boundaries.

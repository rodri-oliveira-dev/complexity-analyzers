# Issue 33 Roslyn Host and Package Compatibility

This SDD artifact formalizes the compatibility and packaging contract for
`ComplexityAnalysis.Analyzers`.

Issue #31 is complete. PR #37 was merged into `main` at merge commit
`9ee805733ea74bf78a82543986676b6077319a2b` and established the analyzer
correctness characterization baseline.

Issue #32 is complete. PR #38 was merged into `main` at merge commit
`06efd98b56839cd264aeafa458857ff09df20d76` and established the performance
budget and regression baseline.

This issue treats those baselines as invariants. There is no intentional analyzer
behavior, diagnostic, severity, enablement, configuration, `Unknown`, or
performance-budget change.

## Specification

The central contract is that the SDK used to build the repository is not the same
thing as the compiler or IDE host that loads the analyzer package.

### Build-Time Contract

| Area | Contract |
| --- | --- |
| Repository SDK | `global.json` selects .NET SDK `10.0.400` with `rollForward: latestFeature` and `allowPrerelease: false`. |
| CI build SDK | Normal repository jobs use `actions/setup-dotnet` with `global-json-file: global.json`. |
| Repository language version | C# `12.0` from `Directory.Build.props`. |
| Analyzer target framework | `netstandard2.0`. This is a host compatibility boundary, not a reflection of the repository SDK. |
| Test target framework | `tests/ComplexityAnalysis.Analyzers.Tests` targets `net10.0`. |
| Performance target framework | `performance/ComplexityAnalysis.Analyzers.Performance` targets `net10.0`. |
| Development/build dependencies | Roslyn authoring packages, xUnit, test SDK, runner, and coverage collector are development-time only for repository validation. |

The repository build SDK may advance independently after normal review. Advancing
it must not be documented as raising the minimum Roslyn/compiler host able to load
the analyzer.

### Roslyn Host Compatibility Contract

| Area | Contract |
| --- | --- |
| Roslyn compiler API baseline | `Microsoft.CodeAnalysis.CSharp` `4.8.0`, which resolves `Microsoft.CodeAnalysis.Common` `4.8.0`. |
| Analyzer authoring rules baseline | `Microsoft.CodeAnalysis.Analyzers` `3.11.0`. |
| Rationale | `4.8.0` is the current conservative Roslyn 4.x baseline from the isolated analyzer foundation. Compiling against unnecessarily newer Roslyn APIs risks producing an analyzer that builds locally but fails to load in older supported compiler/IDE hosts. |
| APIs used | `DiagnosticAnalyzer`, `DiagnosticDescriptor`, `Diagnostic`, `AnalysisContext`, `CompilationStartAnalysisContext`, `SyntaxNodeAnalysisContext`, `CompilationAnalysisContext`, `LanguageNames`, `GeneratedCodeAnalysisFlags`, `CSharpSyntaxTree`, `CSharpCompilation`, C# syntax node types, `SemanticModel`, `Compilation`, `ISymbol`/`IMethodSymbol`/`ITypeSymbol`, `SymbolEqualityComparer`, `AnalyzerConfigOptionsProvider`, `AnalyzerOptions`, `SourceText`, and `Location`. |
| Host risk | A future Roslyn package upgrade can compile code that uses APIs unavailable in older hosts. Such an upgrade requires compatibility evaluation, supported-host execution, package inspection, and documentation updates before merge. |
| Workspaces | `Microsoft.CodeAnalysis.Workspaces`, `Microsoft.CodeAnalysis.CSharp.Workspaces`, `MSBuildWorkspace`, Visual Studio workspace packages, and IDE feature packages are not part of this analyzer package. |

### Analyzer Package Contract

| Area | Contract |
| --- | --- |
| Package ID | `ComplexityAnalysis.Analyzers`. |
| Versioning | CI/local validation injects `PackageVersion`; this issue does not change the public package version. |
| Analyzer assembly path | `analyzers/dotnet/cs/ComplexityAnalysis.Analyzers.dll`. |
| Runtime assets | The package must not contain `lib/` runtime assets for the analyzer assembly. |
| Dependencies | The `.nuspec` must not expose Roslyn authoring dependencies or inherited implementation dependencies to consumers. |
| Build output | `IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`, `DevelopmentDependency=true`, and explicit packing of the analyzer DLL preserve the analyzer-only package shape. |
| Debug symbols | `DebugType=embedded`; a separate `.snupkg` is not produced by the current package contract. |
| Metadata | README, license expression, repository URL/type/commit, project URL, description, authors, and tags are package contract metadata. |

### Consumer Contract

| Area | Contract |
| --- | --- |
| Installation | Consumers install the real `.nupkg` through `PackageReference`. |
| Loading | The compiler host loads the analyzer from package analyzer assets. |
| Execution proof | Compatibility validation enables `BIG9000` as an explicit infrastructure probe and `BIG1006` with a threshold to prove diagnostic execution on real source. |
| Runtime dependency behavior | The package must not add analyzer compile/runtime assets or Roslyn runtime dependencies to the consumer application. |
| Recommended consumer reference | Analyzer references should normally use `PrivateAssets="all"` in reusable libraries so analyzers do not flow unintentionally to downstream projects. |

## Discovery

| Area | Finding |
| --- | --- |
| Issues | #31 and #32 are closed and merged through PRs #37 and #38. Roadmap #36 keeps #33 after correctness and performance baselines and before diagnostic UX/release governance work. |
| SDKs | Local `PATH` has SDKs `8.0.424`, `10.0.111`, `10.0.204`, and `10.0.303`; user-profile `dotnet` has SDK `10.0.400`, which is the SDK selected by `global.json`. |
| Analyzer project | `TargetFramework=netstandard2.0`, `IsAnalyzerProject=true`, `GeneratePackageOnBuild=false`, `IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`, `DevelopmentDependency=true`, `DebugType=embedded`. |
| Roslyn packages | `Microsoft.CodeAnalysis.CSharp=4.8.0`, `Microsoft.CodeAnalysis.Common=4.8.0`, `Microsoft.CodeAnalysis.Analyzers=3.11.0`. |
| Private assets | Analyzer Roslyn package references use `PrivateAssets=all`; test infrastructure packages that should remain private also use `PrivateAssets=all`. |
| Workspaces | No `Microsoft.CodeAnalysis.Workspaces` or `Microsoft.CodeAnalysis.CSharp.Workspaces` package reference exists. |
| Existing package tests | Existing `AnalyzerPackageContractTests` inspect analyzer path, absence of `lib/`, metadata, absence of Roslyn dependencies, and disabled symbol package. |
| Existing compatibility | CI already packs once and builds temporary consumers using SDK hosts `8.0.x`, `9.0.x`, and `10.0.x`; the logic was inline in the workflow. |
| Existing docs | Architecture/getting-started docs mention package boundary and compatibility matrix but need a fuller Roslyn baseline and contract explanation. README prerequisites still referenced an older `10.0.100` SDK value. |

No production analyzer source change is required. The current Roslyn APIs are
covered by compilation against the pinned Roslyn `4.8.0` package and by real
package consumption in compiler hosts.

## Design

The implementation keeps the production analyzer and package project shape
unchanged unless tests reveal a contract defect.

Validation is split by concern:

| Test/validation area | Mechanism |
| --- | --- |
| Package structure tests | Open the real `.nupkg`, assert analyzer DLL placement, absence of duplicate/runtime DLLs, absence of `.deps.json`, metadata, and dependencies. |
| Consumer compatibility tests | Pack the real analyzer, create a temporary consumer, install via `PackageReference`, restore/build, assert `BIG9000` and `BIG1006`, and inspect `project.assets.json` for compile/runtime leakage. |
| Host matrix tests | CI compatibility matrix runs the same consumer smoke script under SDK hosts `8.0.x`, `9.0.x`, and `10.0.x`. |
| Dependency contract tests | Parse project/package config to assert Roslyn baseline, `PrivateAssets=all`, no Workspaces, Dependabot conservative policy, and package metadata dependency absence. |

## Development

Development changes:

- add `AnalyzerHostCompatibilityContractTests` for build SDK, target frameworks,
  Roslyn pins, `PrivateAssets`, Workspaces absence, package-project settings, CI
  host matrix, and Dependabot policy;
- add `AnalyzerPackageConsumerContractTests` to consume the real `.nupkg` in a
  temporary consumer and inspect consumer assets;
- strengthen `AnalyzerPackageContractTests` for single-DLL package layout and
  absence of runtime/debug/dependency assemblies;
- add `RepositoryTestSupport` so test subprocesses use the SDK selected for the
  test run even when a machine PATH does not expose the user-profile SDK first;
- add `eng/Validate-AnalyzerPackageConsumer.ps1` and call it from the CI host
  matrix;
- update English and Brazilian Portuguese docs with build SDK, analyzer target,
  supported hosts, Roslyn baseline, package layout, consumer contract, and
  Dependabot policy.

## Validation Plan

Run from the repository root with the `10.0.400` SDK selected by `global.json`:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageConsumerContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Host matrix validation:

| Consumer SDK host | Consumer target framework | Expected |
| --- | --- | --- |
| .NET 8 | `net8.0` | restore, build, analyzer load, `BIG9000`, `BIG1006`, package contract pass |
| .NET 9 | `net9.0` | restore, build, analyzer load, `BIG9000`, `BIG1006`, package contract pass |
| .NET 10 | `net10.0` | restore, build, analyzer load, `BIG9000`, `BIG1006`, package contract pass |

Local validation records any unavailable SDK host explicitly. CI is the required
source of truth for the full .NET 8/9/10 matrix.

## Validation Results

Local validation on Windows with SDK `10.0.400` from the user-profile dotnet
installation:

| Check | Result |
| --- | --- |
| `dotnet restore ComplexityAnalysis.Analyzers.slnx` | Passed. |
| `dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore` | Passed, 15 existing `IDE0046` style warnings, 0 errors. |
| `dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build` | Passed, 624/624. |
| `AnalyzerCharacterizationBaselineTests` | Passed, 51/51. |
| `AnalyzerPerformanceBudgetContractTests` | Passed, 10/10. |
| `PerformanceSyntheticCorpusTests` | Passed, 9/9. |
| `AnalyzerPackageContractTests` | Passed, 5/5. |
| `AnalyzerPackageConsumerContractTests` | Passed, 1/1. |
| `AnalyzerHostCompatibilityContractTests` | Passed, 5/5. |
| `dotnet pack ... -p:PackageVersion=0.0.0-local --output artifacts/local-packages` | Passed. |
| Package inspection | `ComplexityAnalysis.Analyzers.0.0.0-local.nupkg` contains `analyzers/dotnet/cs/ComplexityAnalysis.Analyzers.dll`, `README.md`, and `.nuspec` metadata; no `lib/`, `.deps.json`, `.pdb`, duplicate DLL, or dependency group. |
| Performance `ReportAnalyzer=true` | Passed; report includes `ComplexityAnalysis.Analyzers.ComplexityAnalyzer` with `<0,001 s`, `<1%` in this local run. |
| Local consumer SDK host `.NET 8` | Passed with SDK `8.0.424`, target `net8.0`, `BIG9000`, and `BIG1006`. |
| Local consumer SDK host `.NET 10` | Passed with SDK `10.0.400`, target `net10.0`, `BIG9000`, and `BIG1006`. |
| Local consumer SDK host `.NET 9` | Not run locally because SDK 9 is not installed; CI matrix validates `9.0.x`. |

## Delivery

Before commit:

- review `git diff --check` and full `git diff`;
- confirm analyzer behavior, `Unknown`, diagnostics, performance budgets,
  `netstandard2.0`, Roslyn `4.8.0`, Workspaces absence, package layout,
  dependency leakage, release safety, and generated artifacts;
- commit with Conventional Commits;
- push the dedicated branch;
- open a PR to `main` with `Closes #33`;
- do not merge, publish packages, create tags, or create releases.

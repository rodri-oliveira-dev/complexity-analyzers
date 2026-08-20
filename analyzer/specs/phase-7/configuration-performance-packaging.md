# Phase 7 — Configuration, Performance & NuGet Readiness

## Status

In Progress.

This document is the Phase 7 specification. It does not implement production code, publish a package, create a GitHub Release, or push repository changes.

Phase 7 may proceed because Phase 6 is factual in the current repository: direct-recursion recurrence infrastructure exists under `Analysis/Recursion/`, `BIG1005` is part of `SupportedDiagnostics`, Phase 6 recurrence and analyzer tests exist, Phase 6 documentation is present in English and pt-BR, and the Release test suite passed with 472 tests.

## Objective

Make `ComplexityAnalysis.Analyzers` ready for configurable analyzer behavior, repeatable performance validation, and NuGet package consumption while preserving the current analyzer behavior by default.

Phase 7 introduces the product contract for:

- Roslyn/AnalyzerConfig-based custom options;
- safe public configuration for existing interprocedural and recursion behavior;
- a configurable complexity threshold diagnostic;
- performance structure, workloads, and regression policy;
- NuGet metadata, symbols/source debugging, package contract tests, consumer smoke tests, and CI readiness.

## Context

The repository currently contains Phase 1 through Phase 6. The analyzer is an isolated `netstandard2.0` Roslyn analyzer package under `analyzer/`, separate from inherited `complexity-hints` projects.

Current factual inventory:

- Existing diagnostics: `BIG0001`, `BIG1001`, `BIG1002`, `BIG1003`, `BIG1004`, `BIG1005`, and `BIG9000`.
- Existing custom analyzer options: none. Documentation states that Phase 6 uses only standard `dotnet_diagnostic.<RULE_ID>.severity` configuration.
- Existing internal budgets: `AnalysisBudget.DefaultMaximumCallDepth = 5` and `AnalysisBudget.DefaultMaximumMethodsPerRootAnalysis = 32`.
- Existing package metadata: `PackageId`, `Title`, `Description`, `PackageTags`, `RepositoryUrl`, `PackageProjectUrl`, and `PackageReadmeFile` are present. `Authors`, `Copyright`, `PackageLicenseExpression` or `PackageLicenseFile`, and `RepositoryType` are absent.
- Existing package version: no repository-defined `Version`, `VersionPrefix`, or `PackageVersion` property was found. CI and docs pass temporary versions to `dotnet pack`.
- Existing package contract: `IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`, `DevelopmentDependency=true`, Roslyn package references use `PrivateAssets=all`, and the analyzer DLL is packed under `analyzers/dotnet/cs/`.
- Existing symbols/source debugging settings: no `IncludeSymbols`, `SymbolPackageFormat`, `PublishRepositoryUrl`, Source Link package, or `DebugType` setting was found.
- Existing package validation: no `EnablePackageValidation` setting was found. CI validates package contents with a custom archive inspection.
- Existing CI: `.github/workflows/analyzer-ci.yml` performs restore, Release build, Release test with coverage, package packing, package content validation, dependency review for pull requests, and SonarCloud on eligible events. It does not publish NuGet packages.
- Existing package tests: CI validates `.nupkg` contents for analyzer placement and absence of `lib/*/ComplexityAnalysis.Analyzers.dll`; no dedicated consumer smoke test project or multi-SDK package compatibility matrix exists yet.
- Existing docs: `analyzer/README.md`, `analyzer/README.pt-BR.md`, and docs under `analyzer/docs/en/` and `analyzer/docs/pt-BR/`.
- Existing performance infrastructure: structural safeguards exist through cancellation, generated-code exclusion, concurrent execution, per-compilation cache, bounded interprocedural traversal, bounded numerical recurrence solving, and no documented hot-path I/O/network/process execution. No synthetic performance corpus, benchmark project, analyzer execution-time reporting validation, or CI performance baseline was found.

Official references considered for this specification:

- Microsoft.CodeAnalysis `AnalyzerConfigOptionsProvider`: `https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.diagnostics.analyzerconfigoptionsprovider`
- .NET code-analysis configuration and `dotnet_diagnostic` severity: `https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-options`
- NuGet analyzer package conventions: `https://learn.microsoft.com/nuget/guides/analyzers-conventions`
- NuGet/MSBuild pack metadata and symbol package properties: `https://learn.microsoft.com/nuget/reference/msbuild-targets`
- NuGet `.snupkg` symbol packages: `https://learn.microsoft.com/nuget/create-packages/symbol-packages-snupkg`
- .NET package validation: `https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview`
- Csc `ReportAnalyzer`: `https://learn.microsoft.com/visualstudio/msbuild/csc-task`
- Current .NET support policy: `https://learn.microsoft.com/dotnet/core/releases-and-support`

## Current Product Surface

The analyzer pipeline currently includes:

- intraprocedural method extraction;
- input-size resolution;
- basic operation analysis;
- loop-bound analysis;
- semantic BCL/LINQ known-operation mapping;
- deferred LINQ consumption handling;
- demand-driven source-method interprocedural analysis;
- direct-recursion extraction and recurrence solving for supported patterns;
- diagnostic emission for known estimates and selected actionable patterns.

The current diagnostic policy is:

| ID | Title | Category | Severity | Enabled by default |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | `false` |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | `true` |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | `true` |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | `true` |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | `true` |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | `true` |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | `false` |

`BIG1006` is available for Phase 7 because no existing descriptor, release entry, documentation entry, or test constant uses that ID.

## Configuration Architecture

Custom configuration must use Roslyn analyzer configuration mechanisms. Do not create a manual `.editorconfig` file parser.

Conceptual architecture:

```text
.editorconfig
     |
     v
AnalyzerConfigOptionsProvider
     |
     v
ComplexityAnalyzerOptions
     |
     +-- feature flags
     +-- analysis budgets
     +-- complexity threshold
     |
     v
analyzer pipeline
```

Implementation requirements:

- Read analyzer options through `AnalyzerOptions.AnalyzerConfigOptionsProvider` or an equivalent Roslyn-supported path available to `DiagnosticAnalyzer`.
- Use tree-specific options through `AnalyzerConfigOptionsProvider.GetOptions(SyntaxTree)` when a setting can vary by file.
- Use global options only for settings that are intentionally compilation-wide.
- Parse values into an immutable `ComplexityAnalyzerOptions` value.
- Pass parsed options explicitly into analyzer pipeline components that need them.
- Avoid mutable global option state.
- Preserve existing defaults when no custom option is configured.

## Configuration Keys

All custom options use the `complexity_analyzers.*` prefix. These names become configuration API once implemented.

| Key | Type | Default | Scope | Purpose |
| --- | --- | --- | --- | --- |
| `complexity_analyzers.interprocedural_analysis` | bool | `true` | tree-specific | Enables supported source-callee expansion. |
| `complexity_analyzers.recursion_analysis` | bool | `true` | tree-specific | Enables supported direct recurrence extraction and solving. |
| `complexity_analyzers.max_call_depth` | int | `5` | tree-specific | Public cap for source-callee expansion depth. |
| `complexity_analyzers.max_methods_per_root` | int | `32` | tree-specific | Public cap for methods expanded from one root method. |
| `complexity_analyzers.maximum_complexity` | enum/string | `none` | tree-specific | Optional method complexity threshold. |

Boolean grammar:

```text
true
false
```

Boolean parsing is case-insensitive and trims surrounding whitespace. Other values are invalid.

Integer grammar:

```text
0
1
2
...
```

Only base-10 non-negative integers are valid. Signed values, decimals, separators, whitespace inside digits, and non-finite values are invalid.

Threshold grammar:

```text
none
constant
log_n
n
n_log_n
n2
n3
exponential
factorial
```

The grammar intentionally exposes only forms the current model can compare reliably for same-variable known expressions. It does not expose arbitrary composite expressions such as `n_m`, fractional powers such as `n_1_585`, or parameterized forms such as `n^k` in Phase 7.

## Defaults

Defaults preserve current behavior:

- Interprocedural analysis is on by default because Phase 5/6 behavior currently expands supported source callees.
- Recursion analysis is on by default because Phase 6 direct-recursion solving is implemented and active.
- `max_call_depth` defaults to the real current internal value: `5`.
- `max_methods_per_root` defaults to the real current internal value: `32`.
- `maximum_complexity` defaults to `none`, so no threshold diagnostic is emitted by default.

## Validation and Fallback Policy

Invalid configuration must be safe:

- must not throw;
- must not break the build;
- must not cause analyzer exceptions;
- must not report an analyzer failure diagnostic;
- must fall back to the documented default for that option.

Phase 7 does not introduce a diagnostic for invalid configuration. This avoids adding user-facing noise before the product has evidence that config mistakes need reporting. Tests must cover invalid values and prove fallback behavior.

If a key appears in both global and tree-specific options, the tree-specific value wins for analysis of that syntax tree.

## Analysis Budgets

Public budget options remain guarded by internal hard limits.

`complexity_analyzers.max_call_depth`:

- default: `5`;
- minimum accepted value: `0`;
- maximum public value: `16`;
- maximum hard limit: `16`;
- invalid or above-limit values fall back to `5`.

`complexity_analyzers.max_methods_per_root`:

- default: `32`;
- minimum accepted value: `0`;
- maximum public value: `128`;
- maximum hard limit: `128`;
- invalid or above-limit values fall back to `32`.

The implementation must not allow configuration to remove protections against stack overflow, analysis explosion, cache deadlock, or unbounded numerical solving. Budget enforcement continues to return conservative `Unknown` at the boundary.

## Feature Controls

`complexity_analyzers.interprocedural_analysis=false` must prevent expansion into source callees while preserving:

- intraprocedural analysis;
- supported BCL/LINQ operation analysis;
- existing diagnostics whose evidence remains intraprocedural or BCL/LINQ-based.

`complexity_analyzers.recursion_analysis=false` must prevent recurrence extraction and solving while preserving:

- non-recursive intraprocedural analysis;
- supported BCL/LINQ analysis;
- supported non-recursive source-callee expansion if `interprocedural_analysis=true`;
- Phase 5 cycle safety, where recursive cycles remain `Unknown`.

Feature flags must not change standard diagnostic severity configuration.

## Complexity Threshold

Phase 7 specifies a new diagnostic:

```text
BIG1006 — Method complexity exceeds configured threshold
```

Descriptor policy:

- Category: `Complexity`
- Severity: `Info`
- Enabled by default: `true`
- Message: `Method '{0}' has estimated complexity {1}, which exceeds the configured maximum {2}.`
- Location: method identifier

Although the descriptor is enabled by default, `maximum_complexity=none` means there is no functional opt-in and no `BIG1006` report. Setting a concrete threshold is the opt-in.

`BIG1006` must not recommend a specific data structure, algorithm, memoization strategy, or rewrite.

## Diagnostic Policy

Custom `complexity_analyzers.*` options control analyzer behavior, not diagnostic severity.

Severity must continue to use standard Roslyn configuration:

```ini
dotnet_diagnostic.BIG1006.severity = warning
```

Supported environments determine exact handling for:

```text
none
silent
suggestion
warning
error
default
```

Existing diagnostics must not regress. `BIG0001` remains disabled by default and reports only known method estimates when explicitly enabled. Threshold evaluation may reuse the same known method complexity result but must not cause `BIG0001` to be emitted.

Threshold reporting policy:

- report only when actual complexity is known;
- do not report for `Unknown`;
- do not report for `GrowthComparison.Equivalent`;
- do not report for `GrowthComparison.Less`;
- do not report for `GrowthComparison.Incomparable`;
- report only when `ComplexityGrowthComparer.Compare(actual, threshold) == GrowthComparison.Greater` or the factual equivalent in the implementation.

Examples:

- actual `O(n²)`, threshold `n_log_n`: report `BIG1006`;
- actual `O(n log n)`, threshold `n_log_n`: no report;
- actual `O(n)`, threshold `n_log_n`: no report;
- actual `Unknown`, threshold `n`: no report;
- actual `O(n · m)`, threshold `n2`: no report when the model considers the expressions incomparable.

## Performance Model

Analyzer performance is functional behavior.

Structural guarantees:

- no network access;
- no filesystem I/O in analyzer hot paths;
- no process launch;
- no telemetry;
- no user data leaves the `Compilation`;
- no mandatory whole-solution scan;
- no eager whole-compilation call graph;
- per-`Compilation` cache only;
- bounded interprocedural traversal;
- bounded recurrence solving and bounded numerical loops;
- cancellation respected before expensive syntax, semantic, cache, traversal, substitution, and solver work;
- generated code handling remains explicit;
- no mutable global analysis state;
- deterministic behavior under concurrent analyzer execution.

## Performance Measurement

Phase 7 must add a small synthetic corpus under `analyzer/` for repeatable performance validation. It must not use third-party real code.

Required workload categories:

- many trivial methods;
- loop-heavy methods;
- LINQ-heavy methods;
- call-chain methods;
- recursive methods;
- repeated shared callees.

The workload should be compiled and analyzed in a repeatable test or tool path that exercises the package/analyzer as consumers would. It may record elapsed time for coarse regression detection, but initial pass/fail gates must avoid rigid hardware-dependent millisecond thresholds.

When supported by the toolchain, CI must validate compiler analyzer execution-time reporting through the Csc `ReportAnalyzer` mechanism or an equivalent official compiler path. This is a reporting validation, not telemetry.

## Performance Regression Policy

Initial Phase 7 regression policy:

- enforce structural invariants through tests and code review;
- require the synthetic workload to complete successfully in CI;
- compare coarse local measurements only against broad thresholds suitable for GitHub-hosted runner variability;
- investigate regressions that materially increase analyzer runtime, allocations, or timeout risk;
- do not fail CI on narrow millisecond deltas until enough baseline history exists.

No runtime telemetry, external service, or network upload is allowed.

## Package Contract

The package remains a Roslyn analyzer package:

- analyzer assembly must be under `analyzers/dotnet/cs/`;
- analyzer assembly must not be exposed as a conventional runtime library under `lib/`;
- Roslyn authoring dependencies must not flow transitively to consumers;
- inherited assemblies from upstream projects must not be included;
- package content must be validated from the generated `.nupkg`;
- package consumers reference it as an analyzer package, normally with `PrivateAssets="all"`.

Existing `IncludeBuildOutput=false`, `SuppressDependenciesWhenPacking=true`, `DevelopmentDependency=true`, analyzer DLL packing, and `PrivateAssets=all` Roslyn references should be preserved unless a package contract test proves a safer replacement.

## NuGet Metadata

Required metadata for Phase 7:

- `PackageId`: keep `ComplexityAnalysis.Analyzers`.
- `Title`: keep `ComplexityAnalysis.Analyzers`.
- `Description`: keep factual analyzer description.
- `PackageTags`: keep and refine factual tags if needed.
- `PackageReadmeFile`: keep `README.md`.
- `RepositoryUrl`: keep `https://github.com/rodri-oliveira-dev/complexity-analyzers`.
- `RepositoryType`: add `git`.
- `PackageProjectUrl`: keep repository URL unless a factual project website exists.
- `Authors`: add factual repository owner/author value; do not invent an organization.
- `Copyright`: add only if a factual owner/year can be derived from repository files.
- `PackageLicenseExpression` or `PackageLicenseFile`: add only after confirming the repository license file and using the matching NuGet-supported expression or packed license file.

Do not invent a company, foundation, or publication status.

## Symbols and Source Debugging

Phase 7 should generate a `.snupkg` if package inspection confirms it contains useful symbols for the analyzer assembly:

```xml
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
```

Source Link should be enabled only through official SDK-supported properties/packages compatible with the current analyzer package. It must not introduce a runtime dependency and must not cause Roslyn dependencies to become transitive consumer dependencies.

Recommended evaluation:

- `PublishRepositoryUrl=true`;
- `RepositoryType=git`;
- provider-specific Source Link package with `PrivateAssets=all` if needed by the SDK/toolchain;
- deterministic CI build compatibility.

If Source Link or `.snupkg` adds no useful debugging value for the analyzer package, document the decision and keep package contract tests as the release readiness gate.

## Package Validation

Evaluate `EnablePackageValidation=true` during implementation, but do not force it by checklist.

The official .NET package validation feature is designed to validate package compatibility after `Pack`, especially API compatibility across package versions and target frameworks. Because this package's primary product asset is an analyzer under `analyzers/dotnet/cs/` rather than a runtime library under `lib/`, Phase 7 must prove that built-in package validation adds useful signal before enabling it permanently.

If `EnablePackageValidation` does not validate the analyzer-specific contract meaningfully, keep it off and rely on explicit package contract tests that inspect the `.nupkg` for:

- analyzer DLL under `analyzers/dotnet/cs/`;
- no analyzer DLL under `lib/`;
- no inherited assemblies;
- no unintended dependency groups;
- README, license, repository metadata, and symbols/source debugging assets according to this spec.

## Compatibility Matrix

Phase 7 must test analyzer package consumption on currently supported .NET SDK lines according to the official .NET support policy at implementation time.

As of this specification date, official .NET support includes .NET 8 LTS, .NET 9 STS, and .NET 10 LTS. The matrix should be rechecked during CI implementation.

Compatibility tests must verify:

- package restore;
- analyzer load from `analyzers/dotnet/cs/`;
- build success;
- expected diagnostics on a small consumer project;
- no Roslyn host incompatibility such as `CS9057` or an equivalent analyzer-load error.

The matrix must not claim support for unsupported SDKs unless an explicit compatibility reason and test exists.

## CI Contract

CI must perform:

- restore;
- Release build;
- Release tests;
- pack;
- package contract tests;
- consumer smoke tests;
- compatibility matrix;
- proportional performance workload validation;
- analyzer execution-time reporting validation when supported;
- NuGet audit or equivalent built-in dependency audit.

CI must not:

- publish to NuGet.org;
- push tags;
- create GitHub Releases;
- require publish tokens for normal validation;
- upload user code or analyzer telemetry to external performance services.

The existing SonarCloud job may remain separate and token-gated. Normal validation must not require a NuGet publish secret.

## Documentation Requirements

Update English and pt-BR documentation:

- `README.md`;
- `README.pt-BR.md`;
- `docs/en/configuration.md`;
- `docs/pt-BR/configuration.md`;
- `docs/en/getting-started.md`;
- `docs/pt-BR/getting-started.md`;
- `docs/en/analyzers.md`;
- `docs/pt-BR/analyzers.md`;
- architecture/package documentation where applicable.

Documentation must describe:

- all `complexity_analyzers.*` options;
- defaults and invalid fallback behavior;
- feature flag effects;
- budget limits and hard limits;
- `maximum_complexity` grammar;
- `BIG1006`;
- standard `dotnet_diagnostic` severity configuration;
- package contract and local consumption;
- symbol package/source debugging decision;
- compatibility and CI validation;
- that no NuGet.org publication or GitHub Release exists unless independently published later.

## Security and Supply Chain

Phase 7 must preserve or evaluate:

- NuGet audit settings already present in `Directory.Build.props`;
- controlled GitHub Actions versions;
- least-privilege GitHub token permissions;
- no secret required for normal restore/build/test/pack validation;
- no NuGet publish token;
- deterministic build settings already present;
- no inherited project references or packaged inherited assemblies;
- no new external tool solely to satisfy a checklist.

Security changes must be directly useful to the analyzer package and validated by tests or CI.

## Out of Scope

Explicitly out of scope:

- NuGet.org publication;
- GitHub Release publication;
- package signing;
- certificate management;
- automatic CodeFix;
- Visual Studio VSIX;
- Rider plugin;
- telemetry collection;
- remote service;
- network calls;
- confidence scoring, unless it already exists factually;
- new recurrence families;
- new BCL/LINQ catalog expansion;
- whole-solution analysis;
- manual `.editorconfig` parser;
- custom severity system replacing `dotnet_diagnostic`;
- publishing credentials or release automation.

## Acceptance Criteria

- AC01 — configuration usa `AnalyzerConfigOptionsProvider` ou mecanismo Roslyn equivalente.
- AC02 — opções usam prefixo consistente.
- AC03 — defaults preservam comportamento atual.
- AC04 — config inválida não lança.
- AC05 — `interprocedural_analysis=false` é respeitado.
- AC06 — `recursion_analysis=false` é respeitado.
- AC07 — `max_call_depth` é configurável dentro de limites seguros.
- AC08 — `max_methods_per_root` é configurável dentro de limites seguros.
- AC09 — hard limits não podem ser removidos via config.
- AC10 — `maximum_complexity=none` não produz threshold diagnostic.
- AC11 — threshold comparável acima do limite produz `BIG1006`.
- AC12 — complexity igual ao threshold não produz diagnostic.
- AC13 — complexity abaixo não produz diagnostic.
- AC14 — `Unknown` não produz threshold diagnostic.
- AC15 — `Incomparable` não produz threshold diagnostic.
- AC16 — standard `dotnet_diagnostic` severity continua funcionando.
- AC17 — analyzer possui workload de performance repetível.
- AC18 — hot path não realiza I/O/network/process execution.
- AC19 — analyzer continua bounded.
- AC20 — performance reporting é validado quando suportado pelo compiler/toolchain.
- AC21 — package metadata está completo e factual.
- AC22 — `.nupkg` contém analyzer em `analyzers/dotnet/cs`.
- AC23 — package não contém assemblies herdados.
- AC24 — Roslyn não é dependency transitiva do consumidor.
- AC25 — symbol package é gerado se aprovado pela spec.
- AC26 — package validation/contract tests passam.
- AC27 — consumer smoke test passa.
- AC28 — compatibility matrix passa.
- AC29 — CI não publica package.
- AC30 — diagnostics existentes não regrediram.
- AC31 — docs EN e pt-BR descrevem config e distribution.
- AC32 — build Release passa.
- AC33 — tests Release passam.
- AC34 — upstream herdado permanece intacto.

## Definition of Done

Phase 7 is done when custom analyzer configuration is implemented through Roslyn analyzer config APIs, defaults preserve Phase 6 behavior, invalid configuration falls back safely, interprocedural and recursion feature flags work independently, public budgets are configurable but bounded by hard limits, `BIG1006` reports only comparable threshold violations, performance is validated through structural guarantees and a repeatable synthetic workload, NuGet metadata is complete and factual, package symbols/source debugging are either implemented or explicitly rejected with evidence, package contract and consumer smoke tests pass across the supported compatibility matrix, CI validates restore/build/test/pack without publishing, English and pt-BR documentation are updated, Release build/tests pass, and inherited upstream files remain untouched.

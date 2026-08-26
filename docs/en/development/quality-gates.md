# Release Quality Governance

[English](quality-gates.md) | [Português (Brasil)](../../pt-BR/development/quality-gates.md)

This is the canonical Definition of Done for `ComplexityAnalysis.Analyzers`.
Issue-specific specifications, contributor guidance, agents, and pull requests
should point here instead of redefining the same policy.

## Principle

Validation must be proportional to risk.

A README typo does not need the performance harness. A Roslyn dependency change
does need host and package compatibility evidence. A diagnostic behavior change
does need triggering and non-triggering tests, documentation, and release
tracking when the public analyzer contract changes.

Start every change by answering:

1. What type of change is this?
2. Which public behavior, package, performance, compatibility, documentation,
   security, or release risk does it carry?
3. Which evidence proves those risks are handled?
4. Which validations are intentionally not applicable?

## Change-Type Matrix

| Change type | Examples | Required evidence | Not normally required |
| --- | --- | --- | --- |
| Documentation only | README, docs, typo, links, examples that do not alter behavior. | Markdown review, coherent links, EN/PT-BR alignment when public equivalent content changes. | Analyzer performance harness, package consumer matrix, release tracking. |
| Test only | Characterization, regression tests, fixtures. | Restore/build/test; prove no unintended production behavior change. | Performance validation unless harness, workload, or performance infrastructure changes. |
| Analyzer behavior | New analysis, heuristic change, complexity composition, recursion or interprocedural behavior. | Triggering and non-triggering tests; #31 characterization remains green or intentional before/after is documented; false-positive review; conservative `Unknown`; cancellation/concurrency review; performance assessment; public docs when behavior is user-visible. | Package validation beyond normal gates unless dependencies, target framework, packaging, or consumer loading change. |
| New diagnostic | New rule ID, descriptor, message, category, severity, enablement, reporting logic. | Unique ID; descriptor contract; triggering/non-triggering tests; diagnostic UX per #34; EN/PT-BR catalog docs; config docs if configurable; `AnalyzerReleases.Unshipped.md`; performance impact review; package compatibility preserved. | Release publication or version bump automation. |
| Diagnostic message or UX | Message text, diagnostic properties, location, guidance. | #34 convention; deterministic message/properties tests; release tracking when public diagnostic metadata changes; docs when public guidance changes; prove triggering did not change unintentionally. | Performance harness unless explanation generation touches hot paths materially. |
| Configuration change | New key, default, valid values, fallback, boundaries. | Key documented; default and allowed values documented; invalid fallback tested; boundary tests; EN/PT-BR docs; release tracking; backwards compatibility review. | Package consumer matrix unless package or host behavior changes. |
| Performance-sensitive change | Traversal, caches, recurrence solver, semantic resolution, interprocedural logic, known operations. | #32 structural gates; relevant performance tests/harness; boundedness; cancellation; cache ownership review; regression justification for intentional variance. | Fragile wall-clock thresholds as the only proof. |
| Roslyn or dependency change | `Microsoft.CodeAnalysis*`, analyzer authoring packages, test/build dependencies. | Reason; #33 compatibility matrix; package contract; supported host evidence; dependency leakage analysis; `PrivateAssets` review; no Workspaces without explicit architecture decision; Dependabot policy respected. | Public docs unless support policy or consumer behavior changes. |
| Packaging change | `.csproj` pack settings, `.nupkg` layout, package metadata, consumer loading. | Local pack; `.nupkg` inspection; analyzer path validation; no runtime `lib/` regression; no dependency leakage; consumer test; compatibility impact review. | Diagnostic characterization beyond normal tests unless behavior changed. |
| CI or workflow change | GitHub Actions, permissions, required checks, workflow names/jobs. | Least privilege; event and permission review; pin/version review; branch protection/ruleset compatibility; no weakened gates; no new `continue-on-error` on critical validation; release side effects assessed. | Analyzer behavior tests unless workflow change alters test selection or validation semantics. |
| Release change | Release workflow, tags, NuGet, GitHub Packages, GitHub Release, version semantics. | Explicit maintainer intent; main-branch and semantic-version behavior; retry/idempotency; Trusted Publishing/OIDC permissions; tag immutability; package publication targets. | Performing a production release during ordinary development. |
| Future CLI or project-level change | Project scanning, aggregate reports, filesystem/project loading, JSON/console output. | Keep project-level work outside `DiagnosticAnalyzer` hot paths; test filesystem/project loading separately; prove analyzer package runtime remains clean. | Treating CLI I/O as acceptable analyzer hot-path I/O. |

## Definition of Done

Use the dimensions that apply to the change.

### Correctness

- Expected behavior is specified before implementation when behavior changes.
- Triggering, non-triggering, and regression tests are added or updated when
  analyzer behavior changes.
- #31 characterization remains green unless a deliberate behavior change records
  `Before`, `After`, and `Reason`.
- Unsupported or unproven cases remain `Unknown`.
- No known high-confidence false positive is introduced.

### Performance

- Analyzer work remains bounded, demand-driven, cancellation-aware, and safe for
  concurrent execution.
- Hot paths do not add filesystem I/O, network I/O, process execution,
  telemetry, mandatory whole-solution scans, or mandatory whole-compilation call
  graph construction.
- Changes to traversal, recursion, caching, semantic resolution, or known
  operations satisfy #32 structural gates and record any material variance.

### Compatibility And Packaging

- The analyzer target framework remains `netstandard2.0` unless an explicit
  architecture decision changes the support policy.
- Roslyn compatibility follows the #33 baseline.
- The package keeps the analyzer assembly under `analyzers/dotnet/cs/` and does
  not regress into runtime `lib/` assets.
- Roslyn authoring dependencies remain private; consumers do not receive
  unnecessary compile/runtime/transitive dependencies.
- Supported SDK host evidence is provided for Roslyn, dependency, packaging, or
  consumer-loading changes.

### Diagnostic Experience

- Public diagnostics explain only evidence the analyzer can prove.
- Messages and properties are deterministic, concise, and stable.
- Guidance is conditional when semantic suitability cannot be proven.
- Diagnostic locations point to useful user code.
- New or changed diagnostics follow the #34 convention.

### Documentation

- Public behavior changes update the analyzer catalog, configuration docs,
  architecture/getting-started docs, or README only where relevant.
- Equivalent public content in `docs/en` and `docs/pt-BR` stays semantically
  aligned.
- Internal architecture notes and CI-only details do not require duplicate
  translation unless they become public user/contributor guidance.

### Release Tracking And Versioning

- Update `src/ComplexityAnalysis.Analyzers/AnalyzerReleases.Unshipped.md` for
  new diagnostics, changed diagnostics, removed diagnostics, public
  configuration, or public analyzer behavior relevant to release notes.
- Public changes consider semantic version impact: fixes are usually patch,
  backward-compatible capabilities are usually minor, and breaking diagnostic,
  configuration, or package-contract changes are major candidates.
- Do not automate version bumps as part of ordinary analyzer changes unless a
  separate release issue asks for it.

### Security And Dependencies

- Analyzer hot paths do not access secrets, network, filesystem, telemetry, or
  processes.
- Dependency changes document purpose, security impact, compatibility, and
  package impact.
- Workflow and release changes review `GITHUB_TOKEN`, OIDC, environments,
  package registries, and permissions.
- Do not commit secrets, local packages, coverage output, or build artifacts.

## Required Checks And CI Mapping

The active `main` ruleset requires these status checks by exact name:

| Quality dimension | Required check | What it covers |
| --- | --- | --- |
| Correctness and coverage signal | `Validate analyzer` | Restore, Release build, tests with OpenCover verification, coverage artifact. |
| Code quality and security analysis | `SonarQube Cloud` | Sonar analysis and quality gate after validation. |
| Analyzer package layout | `Pack analyzer` | Pack and inspect analyzer placement under `analyzers/dotnet/cs/`; reject runtime `lib/` analyzer asset. |
| Core solution validation | `Quality` | Restore, Release build, broad test suite excluding focused package/performance suites. |
| Package contract | `Package` | Pack, run package contract tests, upload local `.nupkg` artifact. |
| Performance | `Performance` | Structural performance suite and compiler `ReportAnalyzer=true` reporting path. |
| Host compatibility | `Compatibility (8.0.x)` | Real package consumer smoke under .NET 8 SDK host. |
| Host compatibility | `Compatibility (9.0.x)` | Real package consumer smoke under .NET 9 SDK host. |
| Host compatibility | `Compatibility (10.0.x)` | Real package consumer smoke under .NET 10 SDK host. |

Additional branch rules require pull requests, linear history, thread resolution,
code-scanning limits, and protected release tags matching `v*`.

Do not rename required workflow or job names casually. If a check name must
change, treat it as an explicit governance change and document the ruleset impact.

## Local Validation Menu

The common broad validation path is:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

Add focused checks according to risk:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageConsumerContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerHostCompatibilityContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Documentation-only changes may record a lighter validation such as Markdown
review, link review, `git diff --check`, and no analyzer behavior impact.

## Branch, PR, And Issue Flow

Use the simple flow:

```text
issue
  -> dedicated branch
  -> implementation
  -> proportional local validation
  -> pull request
  -> required checks
  -> review
  -> merge
  -> release separately when requested
```

Use Conventional Commits:

- `feat:` for backward-compatible user-facing capability;
- `fix:` for defects;
- `perf:` for performance changes;
- `test:` for test-only changes;
- `docs:` for documentation;
- `refactor:` for non-behavioral code structure;
- `build:` for build system or package construction;
- `ci:` for workflow changes;
- `chore:` for maintenance that does not fit the categories above.

Future feature issues should ideally include context, goal, scope, non-goals,
dependencies, acceptance criteria, and a Definition of Done. Trivial bugs and
small docs fixes can stay lighter when a full template would not add clarity.

## Release Safety

Normal validation is not a production release.

Do not create tags, publish NuGet packages, publish GitHub Packages, create
GitHub Releases, move release tags, or trigger production release workflows as
part of ordinary development. Those actions require explicit maintainer intent.

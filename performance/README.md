# Analyzer performance baseline

This directory contains repeatable performance validation for
`ComplexityAnalysis.Analyzers`.

The analyzer executes inside compiler and IDE hosts, so performance is part of
functional behavior. The project therefore separates deterministic structural
gates from timing measurements that vary by hardware and shared CI runners.

## Performance model

The analyzer must remain:

- free of filesystem I/O, network I/O, process execution, and telemetry in
  analyzer hot paths;
- free of mandatory whole-solution analysis and mandatory whole-compilation call
  graph construction;
- demand-driven for source-method traversal;
- bounded for interprocedural traversal and recurrence solving;
- cancellation-aware in non-trivial analysis paths;
- safe for concurrent execution;
- conservative at boundaries, preferring `Unknown` over unbounded or unsafe
  analysis.

Generated code remains excluded from syntax-node analysis.

## Workloads

The baseline uses four workload groups.

| Workload | Representative shape | Purpose |
| --- | --- | --- |
| Tiny | Straight-line methods, one known operation, one simple loop. | Basic analyzer overhead and ordinary method reporting. |
| Small | Loops, LINQ, simple source calls, and known BCL operations. | Common per-method semantic analysis. |
| Medium | Multiple methods, shared callees, interprocedural chains, nested iteration, deferred LINQ consumption, supported direct recursion. | Representative compiler analyzer execution. |
| Stress/adversarial synthetic | Call chains near `max_call_depth`, fanout near `max_methods_per_root`, cycles, repeated calls to the same callee, unsupported recurrence shapes, numerical solver exhaustion, cancellation. | Termination, boundedness, cache ownership, and conservative fallback. |

Workload sources are committed in:

- `performance/ComplexityAnalysis.Analyzers.Performance/TimingWorkload.cs`
- `tests/ComplexityAnalysis.Analyzers.Tests/PerformanceSyntheticCorpusTests.cs`
- `tests/ComplexityAnalysis.Analyzers.Tests/AnalyzerPerformanceBudgetContractTests.cs`

## Budgets

The current budgets come from the implementation, not from documentation-only
assumptions.

| Budget | Default | Hard maximum | Behavior when exceeded |
| --- | --- | --- | --- |
| Source call depth | `5` | `16` | Affected source call remains `Unknown`; traversal does not cross the boundary. |
| Uncached source-method expansions per root | `32` | `128` | Affected source call remains `Unknown`; unique completed template count is capped for deterministic fixtures. |
| Restricted Akra-Bazzi bracket expansions | `16` | Internal fixed default | Numerical inconclusiveness, then `Unknown` at analyzer level. |
| Restricted Akra-Bazzi bisection iterations | `64` | Internal fixed default | Numerical inconclusiveness, then `Unknown` at analyzer level. |
| Restricted Akra-Bazzi high exponent | `1024` | Internal fixed default | Numerical inconclusiveness, then `Unknown` at analyzer level. |

Other recurrence solvers are bounded by the extracted recurrence shape and the
fixed solver family list.

## Cache contract

| Cache | Owner | Lifetime | Expected bound |
| --- | --- | --- | --- |
| Known operation registry | Immutable static singleton | Process lifetime | Fixed mapping table. |
| Interprocedural template cache | `InterproceduralAnalysisContext` | One compilation analysis context | Reached source methods times distinct option variants. |
| Direct recurrence cache | `InterproceduralAnalysisContext` | One compilation analysis context | Reached recursive methods times distinct option variants. |
| Semantic model cache | `InterproceduralAnalysisContext` | One compilation analysis context | Reached syntax trees. |
| Analyzer options cache | `InterproceduralAnalysisContext` | One compilation analysis context | Reached syntax trees. |

The mutable caches are per compilation and use concurrent dictionaries. No
static mutable cross-compilation analyzer cache is part of the baseline.

## Hard structural gates

These checks are suitable for CI blocking because they are deterministic:

- traversal stops at configured call-depth and methods-per-root budgets;
- fanout at the public hard maximum succeeds and fanout over the maximum stops;
- repeated calls reuse the same completed callee template;
- cycles terminate conservatively;
- zero budgets conservatively disable source expansion;
- recurrence numerical limits return inconclusive rather than spinning or
  guessing;
- cancelled tokens stop analysis without cache growth in covered paths;
- generated-code exclusion and concurrent execution remain explicit;
- package layout and consumer compatibility tests remain green;
- production analyzer source remains free of forbidden hot-path I/O, network,
  process, and telemetry symbols, checked through Roslyn semantic binding rather
  than substring matching.

The primary structural suites are:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
```

The full test suite also includes the characterization baseline from issue #31:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
```

## Informational measurements

Elapsed time and compiler analyzer time are useful trend signals, but they are
not narrow CI gates in this baseline.

Run the synthetic corpus test to print local elapsed time:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
```

Run the compiler analyzer reporting path:

```bash
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

The useful compiler signal is the analyzer execution summary emitted for:

```text
ComplexityAnalysis.Analyzers.ComplexityAnalyzer
```

`ReportAnalyzer=true` is a local compiler/MSBuild reporting path, not telemetry
in the distributed analyzer.

## CI behavior

The CI performance job blocks on structural tests and verifies that compiler
analyzer reporting includes `ComplexityAnalysis.Analyzers.ComplexityAnalyzer`.
Timing data from the compiler report is uploaded as an artifact for inspection
and trend comparison.

CI must not be made green by weakening budgets, disabling tests, adding
`continue-on-error`, or replacing structural gates with fragile millisecond
thresholds.

## Material regression policy

A PR should treat performance as materially regressed when it does any of the
following without explicit justification and validation evidence:

- raises a public or internal structural budget;
- bypasses or removes bounded traversal checks;
- introduces mandatory whole-compilation or whole-solution scans into analyzer
  callbacks;
- increases cache lifetime, scope, key cardinality, or retention risk;
- introduces static mutable cross-compilation state;
- adds filesystem I/O, network I/O, process execution, telemetry, or heavy
  reflection to analyzer execution paths;
- materially increases compiler-reported analyzer time across representative
  workloads after repeated local runs;
- materially increases allocations in hot paths when profiling evidence is
  available;
- weakens cancellation responsiveness;
- changes `Unknown` fallback into a guessed known estimate merely to improve a
  benchmark.

Intentional variance should be documented in the PR with the affected workload,
reason, before/after evidence, and why analyzer behavior remains safe.

## Baseline validation

For issue #32, run from the repository root:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

When recording before/after timing, include the SDK, operating system,
configuration, command, run count, and observed variance. Do not make absolute
claims from a single elapsed-time run.

# Analyzer performance baseline

This directory contains repeatable performance validation for `ComplexityAnalysis.Analyzers`.

The primary CI-friendly harness is the xUnit suite in `tests/ComplexityAnalysis.Analyzers.Tests/PerformanceSyntheticCorpusTests.cs`. It builds deterministic synthetic sources in memory and checks structural performance invariants instead of narrow elapsed-time thresholds.

The analyzer is designed to be bounded. Source-method traversal defaults to call depth `5` and `32` uncached source-method expansions per root analysis. Public configuration can lower those values or raise them up to hard limits of `16` and `128`.

## Workloads

- 520 trivial methods.
- Loop-heavy methods with nested array traversal.
- BCL/LINQ-heavy methods with filtering, ordering, projection, and materialization.
- Shared-callee roots that should reuse one cached method template.
- Deep bounded call chains.
- Supported direct recursion.
- Unsupported recursion that should remain unknown.

## Run

From `analyzer/`:

```bash
dotnet test --configuration Release --filter PerformanceSyntheticCorpusTests
```

The full validation command also runs this harness:

```bash
dotnet test --configuration Release --no-build
```

## Compiler analyzer timing report

The official compiler/MSBuild switch is `ReportAnalyzer=true`. The compiler emits analyzer timing details only with detailed MSBuild verbosity.

Reference: https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-options/errors-warnings#reportanalyzer

From `analyzer/`, after restore:

```bash
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

The useful signal is the analyzer execution summary emitted by the compiler for `ComplexityAnalysis.Analyzers.ComplexityAnalyzer`. This is a local reporting path supplied by the toolchain, not telemetry in the distributed analyzer.

## What matters

- The synthetic corpus is deterministic for the same builder inputs.
- The analyzer completes the corpus in Release tests.
- Shared callees produce one reusable template cache entry.
- Unreachable methods are not expanded into the interprocedural cache.
- Traversal stops at configured depth and method budgets.
- Analyzer options are cached per syntax tree outside repeated method analysis.
- Semantic models are reused per syntax tree within the compilation context.
- Solved direct-recursion results are reused when possible.
- Analyzer hot paths stay free of network access, filesystem I/O, process launch, and telemetry.
- Generated code handling, concurrent execution, and cancellation remain explicit.

## What is not a gate yet

Elapsed time printed by the test harness is informational. A single machine's milliseconds are not an SLA, and this baseline intentionally does not fail CI because a runner is modestly slower.

Use larger timing changes as an investigation signal together with structural regressions, not as a standalone proof of analyzer quality.

# Issue 32 Analyzer Performance Baseline

This SDD artifact records the analyzer performance model, discovery findings,
regression gates, and review policy for issue #32.

Issue #31 is complete. PR #37 was merged into `main` at merge commit
`9ee805733ea74bf78a82543986676b6077319a2b` and added
`specs/issue-31-analyzer-characterization-baseline.md` plus
`AnalyzerCharacterizationBaselineTests`. The behavior characterized there is the
functional contract preserved by this performance baseline.

## Specification

Performance is functional behavior for `ComplexityAnalysis.Analyzers` because
the analyzer executes inside compiler and IDE hosts. The performance model is
therefore based on bounded work and conservative fallback, not on optimistic
wall-clock timing claims.

### Invariants

| Invariant | Contract |
| --- | --- |
| Filesystem I/O | No filesystem I/O in analyzer hot paths. Test projects and package validation may read files as part of validation. |
| Network I/O | No network I/O from analyzer execution. |
| Process execution | No process launch from analyzer execution. |
| Telemetry | No telemetry or external reporting from analyzer execution. |
| Whole-solution analysis | No mandatory solution-wide analysis. |
| Whole-compilation call graph | No mandatory full-compilation call graph construction. |
| Interprocedural traversal | Demand-driven, bounded, cancellation-aware, and conservative at boundaries. |
| Recursion/recurrence solving | Bounded by syntax shape and numerical solver limits; cancellation-aware before extraction/solving paths; inconclusive results remain `Unknown`. |
| Caches | Owned by a per-compilation analysis context or immutable static registry; lifetimes and keys are explicit. |
| Generated code | Syntax-node analysis remains disabled for generated code. |
| Concurrent execution | Analyzer concurrent execution remains enabled and state ownership remains thread-safe. |
| Conservative fallback | `Unknown` remains preferable to unbounded, unsafe, or unproven analysis. |

### Workloads

| Workload | Shape | Purpose | Gate type |
| --- | --- | --- | --- |
| Tiny | Straight-line methods, one known operation, one simple loop. | Capture basic analyzer overhead and ordinary method reporting. | Structural in tests; timing informational. |
| Small | Loops, LINQ, simple safe source calls, and known operations. | Cover common per-method analysis with semantic resolution. | Structural in tests; timing informational. |
| Medium | Multiple methods, shared callees, interprocedural chains, nested iteration, deferred LINQ, supported recursion. | Exercise representative compiler analyzer execution. | Structural in tests; `ReportAnalyzer=true` informational. |
| Stress/adversarial synthetic | Call chains near `max_call_depth`, fanout near `max_methods_per_root`, cycles, repeated calls to the same callee, unsupported recurrence shapes, numerical budget exhaustion, and cancellation. | Prove termination, boundedness, cache ownership, and conservative fallback. | Hard deterministic gates. |

The committed workload fixtures live in:

- `tests/ComplexityAnalysis.Analyzers.Tests/PerformanceSyntheticCorpusTests.cs`
- `tests/ComplexityAnalysis.Analyzers.Tests/AnalyzerPerformanceBudgetContractTests.cs`
- `performance/ComplexityAnalysis.Analyzers.Performance/TimingWorkload.cs`

### Metrics

| Metric | Reliable as hard gate? | Current use |
| --- | --- | --- |
| Configured call depth | Yes | Asserted against implementation defaults and hard maximums. |
| Configured methods per root | Yes | Asserted at default and hard-maximum fanout boundaries. |
| Template cache entry count | Yes, for deterministic fixtures | Asserts demand-driven traversal, repeated-callee reuse, and fanout caps. |
| Semantic model/options cache count | Yes, for deterministic fixtures | Existing performance tests assert one entry per syntax tree. |
| Direct recurrence cache count | Yes, for deterministic fixtures | Existing tests assert recurrence reuse between analyzer passes. |
| Akra-Bazzi bracket/bisection limits | Yes | Asserted with solver-level deterministic budget exhaustion. |
| Cancellation observation | Yes, when driven by already-cancelled or test-controlled token | Asserted without tight timing thresholds. |
| Compiler analyzer execution time | No | Captured with `ReportAnalyzer=true` as informational trend data. |
| Elapsed time | No | Printed by the synthetic corpus test as informational only. |
| Allocations | Not yet | Review signal for future profiling, not a CI gate in this issue. |

No new public counters are added. Test-only structural assertions use existing
internal counts already exposed to the test assembly through
`InternalsVisibleTo`.

## Discovery

| Area | Current protection | Current test | Current limit | Risk |
| --- | --- | --- | --- | --- |
| Analyzer initialization | `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`, `EnableConcurrentExecution()`, compilation-start state. | #31 generated-code/cancellation smoke; descriptor tests. | Per compilation start. | Accidental global state or generated-code regression. |
| Interprocedural depth | `InterproceduralRootAnalysisState.CurrentDepth` checked before cache lookup and before entering a callee. | `PhaseFiveInterproceduralContractTests`, `PerformanceSyntheticCorpusTests`. | Default `5`, public maximum `16`. | A future cache lookup could bypass depth if check order changes. |
| Methods per root | `RootExpansionCounter` shared by a root state and reserved before expansion. | Phase 5 default boundary; performance method-budget test. | Default `32`, public maximum `128`. | Wide fanout could expand too many unique callees if counter is bypassed. |
| Cycles | Active call path stores `MethodSymbolKey` and returns cycle boundary. | Phase 5 cycle tests. | Current root path length. | Deadlock or unbounded traversal if in-progress cache reservations are mishandled. |
| Repeated calls | Completed method templates are reused and do not consume additional per-root budget. | Phase 5 and performance shared-callee tests. | One completed template per method/options key. | Repeated call sites could reanalyze callees if cache key changes accidentally. |
| Template cache | Per-compilation `MethodComplexityTemplateCache`, `ConcurrentDictionary`, key includes method/options budget flags. | `InterproceduralAnalysisContextTests`, phase 5/6, performance tests. | Naturally limited by reached source methods times distinct options in the compilation context. | Retention until compilation context is released; option variants multiply entries. |
| Direct recurrence cache | Per-compilation `ConcurrentDictionary<MethodComplexityCacheKey, ComplexityExpression>`. | Phase 6 and performance recurrence reuse tests. | Reached direct-recursive methods times distinct options. | Same retention/variant risk as template cache. |
| Semantic model cache | Per-compilation `ConcurrentDictionary<SyntaxTree, SemanticModel>`. | Performance synthetic corpus test. | One per reached syntax tree. | Retains semantic models until compilation context is released. |
| Options cache | Per-compilation `ConcurrentDictionary<SyntaxTree, ComplexityAnalyzerOptions>`. | Performance synthetic corpus test. | One per reached syntax tree. | Incorrect tree-specific reuse if keyed too coarsely. |
| Known operations | Immutable static registry and static resolver over immutable mappings. | Known-operation and characterization tests. | Fixed mapping table size. | Repeated symbol resolution cost in hot paths; no unbounded cache risk. |
| Recurrence solving | Solver chain over fixed families; restricted Akra-Bazzi has numeric caps. | Phase 6 solver tests. | Bracket expansions `16`, bisection iterations `64`, high exponent cap `1024`. | Unsupported numerical cases must not spin or become guessed estimates. |
| Configuration | Roslyn analyzer config APIs; invalid values fall back. | Configuration reader and pipeline tests. | `max_call_depth`: `0..16`; `max_methods_per_root`: `0..128`. | Documentation and implementation can drift. |
| Cancellation | Tokens passed to Roslyn APIs and checked in traversal/extraction loops. | Existing cancellation tests plus issue #32 gates. | Host-controlled token. | Future hot paths may omit token checks. |
| Compiler analyzer execution | CI builds performance project with `ReportAnalyzer=true` and greps analyzer name. | Workflow `performance` job. | Informational compiler report. | Timing noise on shared runners. |
| Package/consumer | Analyzer DLL packed under `analyzers/dotnet/cs/`, compatibility smoke on .NET 8/9/10. | Package contract and compatibility CI. | Analyzer package only, no `lib/` analyzer asset. | Future dependency/package drift. |

## Design

The issue #32 implementation is intentionally contract-first:

1. Add a versioned SDD artifact for the performance model and policy.
2. Add deterministic structural tests that assert budgets, cache bounds,
   cancellation behavior, and recurrence numerical limits.
3. Expand the performance workload source so `ReportAnalyzer=true` runs through
   tiny, small, medium, and stress/adversarial code paths.
4. Update performance documentation with commands, budgets, cache review,
   structural gates, timing classification, and material regression policy.

No production analyzer code is changed by this issue. No public diagnostic,
severity, configuration key, package version, dependency, or Roslyn baseline is
changed.

## Budgets

| Budget | Default | Hard maximum | Behavior when exceeded |
| --- | --- | --- | --- |
| Source call depth | `5` | `16` | Affected source call returns `Unknown`; traversal does not cross the boundary. |
| Uncached source-method expansions per root | `32` | `128` | Affected source call returns `Unknown`; cache count does not exceed the budget for the fixture. |
| Restricted Akra-Bazzi bracket expansions | `16` | Internal fixed default | Numerical inconclusiveness, then `Unknown` at analyzer level. |
| Restricted Akra-Bazzi bisection iterations | `64` | Internal fixed default | Numerical inconclusiveness, then `Unknown` at analyzer level. |
| Restricted Akra-Bazzi high exponent | `1024` | Internal fixed default | Numerical inconclusiveness, then `Unknown` at analyzer level. |

Other recurrence solvers are bounded by the size and shape of the extracted
recurrence relation rather than by a separate public counter. They inspect a
fixed solver family list and do not perform general symbolic solving.

## Cache Review

| Cache | Owner | Key | Value | Lifetime | Expected bound | Thread safety | Cancellation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Known operation registry | Static immutable singleton | `KnownOperationIdentity` | `KnownOperationMapping` | Process lifetime | Fixed mapping table | Immutable | Resolver checks token before work. |
| Template cache | `InterproceduralAnalysisContext` | `MethodComplexityCacheKey` including method and options | In-progress/completed interprocedural result | Compilation analysis context | Reached source methods times distinct option variants | `ConcurrentDictionary` | Checks token before operations; abandoned reservations are removed. |
| Direct recurrence cache | `InterproceduralAnalysisContext` | `MethodComplexityCacheKey` | Solved complexity expression | Compilation analysis context | Reached recursive methods times distinct option variants | `ConcurrentDictionary` | Checks token before get/store. |
| Semantic model cache | `InterproceduralAnalysisContext` | `SyntaxTree` | `SemanticModel` | Compilation analysis context | Reached syntax trees | `ConcurrentDictionary` | Checks token before and after model retrieval. |
| Options cache | `InterproceduralAnalysisContext` | `SyntaxTree` | `ComplexityAnalyzerOptions` | Compilation analysis context | Reached syntax trees | `ConcurrentDictionary` | Checks token before lookup. |

No static mutable cross-compilation cache was found. The only static analyzer
state used by hot paths is immutable or readonly and fixed-size.

## Hard Deterministic Gates

These checks are suitable for CI blocking:

- implementation constants match documented budget defaults and hard maximums;
- invalid public budget values fall back to defaults;
- fanout at the public hard maximum succeeds and fanout over the maximum stops
  without growing the template cache beyond the budget;
- repeated calls to the same callee reuse one cache entry under a budget of one;
- call chains over the public hard maximum return `Unknown`;
- zero budgets conservatively disable source expansion;
- restricted Akra-Bazzi numerical limits return inconclusive instead of spinning
  or guessing;
- already-cancelled tokens stop non-trivial interprocedural and recursive
  analysis before cache growth;
- analyzer source remains free of forbidden hot-path I/O/network/process/
  telemetry symbols, checked through Roslyn semantic binding rather than
  substring matching;
- generated-code exclusion, package contract, compatibility, and #31
  characterization suites remain green.

## Performance Measurements

Timing-oriented checks are not hard gates in this baseline:

- xUnit elapsed time printed by `PerformanceSyntheticCorpusTests` is
  informational;
- compiler analyzer timing emitted by `ReportAnalyzer=true` is informational;
- local before/after comparisons should include SDK, configuration, command,
  run count, and observed variance;
- a single slower run on shared CI is an investigation signal only when not
  paired with a deterministic structural regression.

## Material Regression Policy

A future PR should treat performance as materially regressed when it does any of
the following without an explicit design justification and validation evidence:

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
  timing result.

Intentional variance must be documented in the PR with the affected workload,
reason, before/after evidence, and why the functional behavior remains safe.

## Validation Plan

Run from the repository root:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Before commit, run `git diff --check` and review the full diff. Confirm that no
behavior characterized by issue #31 changed intentionally.

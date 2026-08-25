# Issue 31 Analyzer Characterization Baseline

This SDD artifact records the intended correctness baseline before changing tests or
production code for issue #31.

## Specification

The baseline preserves the current observable behavior of
`ComplexityAnalysis.Analyzers`. It does not add metrics, diagnostics, executable
member abstractions, code fixes, whole-solution analysis, or package dependencies.

### Behaviors To Preserve

| Area | Preserved behavior |
| --- | --- |
| Basic methods | Empty block, straight-line constant work, primitive arithmetic/comparison, expression-bodied methods, and block-bodied methods produce `O(1)` when all operations are known. Unsupported expression forms remain `Unknown`. |
| Iteration | Supported `foreach`, `for`, `while`, and `do` bounds compose as linear, logarithmic, nested, sequential, or independent-input complexity. Unsupported loop bounds remain `Unknown`. |
| Known BCL operations | Supported `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>`, array, and string operations are recognized by Roslyn symbol identity. Custom same-name members are not treated as BCL operations by name alone. |
| Known LINQ operations | Supported `Enumerable` terminal/immediate and deferred operations are recognized by resolved symbol identity. Custom same-name extension methods are not treated as LINQ operations by name alone. |
| LINQ deferred behavior | Deferred pipeline creation is setup work (`O(1)`) until the pipeline is consumed. Enumeration by `foreach` or supported terminals charges the pipeline cost. Ordering is charged only when consumption is proven. |
| Interprocedural analysis | Demand-driven source-method expansion works for safe dispatch in the same compilation, including call chains, same callee reuse, argument substitution, independent parameters, constant arguments, source callees in loops, and cross-syntax-tree callees. |
| Conservative interprocedural fallback | External, unresolved, unsafe virtual/interface, cyclic, disabled, and budget-limited source calls remain `Unknown` or no diagnostic as applicable. Known BCL/LINQ operations keep precedence over source analysis. |
| Direct recursion | Supported direct-recursion recurrence families solve to the documented Big-O classes. Missing base case, non-reducing recursion, unsupported shapes, mutual recursion, unknown local work, and numerical inconclusiveness remain conservative. |
| Unknown | `Unknown` is a first-class result. It suppresses opt-in estimate diagnostics and threshold diagnostics rather than being coerced into an approximate known class. |
| Diagnostics | `BIG0001`, `BIG1001`, `BIG1002`, `BIG1003`, `BIG1004`, `BIG1005`, `BIG1006`, and `BIG9000` preserve ID, category, default severity, default enablement, triggering, non-triggering, and source-location behavior. |
| Configuration | `complexity_analyzers.interprocedural_analysis`, `complexity_analyzers.recursion_analysis`, `complexity_analyzers.max_call_depth`, `complexity_analyzers.max_methods_per_root`, and `complexity_analyzers.maximum_complexity` preserve absent, valid, invalid, boundary, hard-limit, tree-specific, and fallback behavior. |
| Analyzer safety | Generated code remains excluded from syntax-node diagnostics. Concurrent execution remains enabled and safe. Cancellation tokens are checked. Analyzer hot paths perform no filesystem I/O, network I/O, process execution, telemetry, required whole-solution analysis, or unbounded whole-compilation traversal. |
| Package contract | The analyzer remains a `netstandard2.0` Roslyn analyzer package with the assembly only under `analyzers/dotnet/cs/`, no runtime `lib/` asset, no inherited DLLs, and no transitive Roslyn dependency. |

### Known Operation Support Matrix

| Family | Operations characterized as known |
| --- | --- |
| `List<T>` | `Count`, indexer, `Contains`, `IndexOf`, `Sort` overloads. |
| `Dictionary<TKey,TValue>` | `ContainsKey`, `ContainsValue`. |
| `HashSet<T>` | `Contains`. |
| Arrays and strings | Array `Length`, array element access, string `Length`, `foreach` over array/string inputs. |
| LINQ terminal/immediate | `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max`, `Aggregate`. |
| LINQ deferred | `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct`, `GroupBy`. |

## Discovery

The repository already contains substantial coverage from previous phases. The goal
for this issue is to formalize it and fill characterization gaps rather than
rewrite adequate tests.

| Area | Scenario | Current coverage | Observed behavior | Documented behavior | Gap |
| --- | --- | --- | --- | --- | --- |
| Basic methods | Empty, expression-bodied, straight-line, primitive operations | `MethodComplexityExtractorTests`, `ComplexityAnalyzerTests` | Known constant cases report `O(1)` when `BIG0001` is enabled | README and analyzer catalog describe known estimates | Public analyzer-level matrix can be clearer |
| Iterations | Simple, nested, independent, logarithmic, while/do, unsupported bounds | `MethodComplexityExtractorTests` | Supported loops compose; unsupported loops return `Unknown` | README/docs describe supported loop and conservative behavior indirectly | Current coverage is sufficient; matrix should reference it |
| BCL operations | `List<T>`, dictionary, hash set, arrays, strings | `KnownOperationRegistryTests`, `MethodComplexityExtractorTests`, diagnostic tests | Symbol-resolved known operations produce documented costs | README/docs list families | Some observable extractor coverage for all mapped operations is spread or missing |
| LINQ terminal/deferred | Terminal operations, deferred creation, pipeline enumeration | `KnownOperationRegistryTests`, `MethodComplexityExtractorTests`, actionable diagnostic tests | Deferred creation is `O(1)`; terminal/foreach consumption charges source/pipeline | README/docs describe creation vs enumeration | Missing one compact full-operation characterization matrix |
| Symbol identity negatives | Custom `Contains`, `Where`, `ToList`, `Count` | Existing tests cover several names at registry/analyzer levels | Custom same-name operations are not matched as BCL/LINQ | README/docs explicitly require symbol identity | Add compact public baseline for all required names together |
| Interprocedural | A->B, chains, loops, substitution, constants, safe/unsafe dispatch, cycles, budgets | `PhaseFiveInterproceduralContractTests`, `MethodComplexityExtractorTests`, config/performance tests | Safe source calls expand; unsafe/cyclic/budget-limited calls remain `Unknown` | README/docs document bounded demand-driven expansion | Current coverage is sufficient; matrix should reference it |
| Recursion | Decrement, summation, exponential, Fibonacci-like, Master, restricted Akra-Bazzi, unsupported cases | `PhaseSixRecurrenceContractTests`, recurrence solver/extractor tests, analyzer diagnostics | Supported recurrence families solve; unsupported remain `Unknown`; exponential direct recursion reports `BIG1005` | README/docs document supported families and limits | Current coverage is sufficient |
| Unknown | Unsupported expressions, loops, calls, recursion, threshold incomparability | Multiple test suites | `Unknown` suppresses `BIG0001` and `BIG1006`; no forced approximation | README/docs identify `Unknown` as first-class | Add baseline doc row and keep explicit tests |
| Diagnostics | IDs, categories, severities, enablement, messages, locations | `ComplexityAnalyzerTests`, config pipeline tests | Descriptor metadata and locations match docs | Analyzer catalog and config docs list descriptors | Probe descriptor metadata lacks the same category assertion depth |
| Configuration | Defaults, valid/invalid values, tree-specific overrides, threshold boundaries | `ComplexityAnalyzerOptionsReaderTests`, `ComplexityAnalyzerConfigurationPipelineTests` | Invalid values fall back; hard limits enforced; tree options win | Config docs match code | Current coverage is sufficient |
| Generated code | Analyzer generated-code policy | `ComplexityAnalyzer.Initialize`, probe test, extractor-layer marker test | Generated syntax-node analysis is disabled; compilation-end probe can still report | Architecture docs say generated-code analysis is disabled | Add analyzer-level test proving method diagnostics are excluded for generated code |
| Cancellation | Extractor and caches | `MethodComplexityExtractorTests`, `RecursiveCallAnalyzerTests`, `InterproceduralAnalysisContextTests` | Already-cancelled tokens throw and cache is not poisoned | Performance docs and architecture list cancellation as invariant | Add public analyzer-pipeline cancellation smoke |
| Concurrency | Registry/cache/context concurrent reads | `KnownOperationRegistryTests`, `InterproceduralAnalysisContextTests`, `MethodComplexityExtractorTests` | Concurrent analysis completes deterministically | Docs say concurrent execution enabled | Existing coverage is sufficient |
| Package/consumer | Package layout and metadata | `AnalyzerPackageContractTests`, CI compatibility job | Analyzer DLL is analyzer-only and consumer build loads package in CI | Getting started/architecture docs describe package boundary | Existing coverage is sufficient |

Possible bugs discovered during discovery: none confirmed. Existing limitations are
intentional conservative behavior unless later issues decide otherwise.

## Design

### Organization

Add one focused test file under `tests/ComplexityAnalysis.Analyzers.Tests/` named
`AnalyzerCharacterizationBaselineTests.cs`. It will not replace existing phase
contract tests. It will collect the missing issue-31 baseline scenarios in a
small number of matrices:

- public descriptor baseline;
- public analyzer Big-O estimate baseline for basic and iteration cases;
- full known-operation estimate matrix for current BCL/LINQ mappings;
- semantic identity negative matrix for custom same-name members/extensions;
- generated-code and cancellation smoke tests.

Existing phase tests remain the source for detailed interprocedural, recurrence,
configuration, performance, and package behavior.

### Contract Versus Implementation

Tests should use observable behavior:

```text
input C# -> analyzer or extractor -> expected diagnostic / expected Big-O / Unknown
```

Tests should not assert private call order, private helper sequencing, or
implementation-specific traversal details except where existing cache/budget
contract tests already intentionally protect bounded behavior.

### Golden Tests

No external snapshot framework is needed. Explicit assertions are clearer for
diagnostic IDs, messages, source spans, and Big-O strings. The only "golden" shape
is a deterministic table embedded in the test source.

### Determinism

The added tests use in-memory syntax trees, trusted platform references, invariant
culture for message formatting, ordinal ordering, no network, no external
filesystem state, and no wall-clock thresholds.

## Validation Plan

Run from the repository root:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Before commit, review `git diff --check` and the full diff. Confirm no public
behavior, diagnostic IDs, severities, enablement, configuration keys, package
version, dependencies, generated artifacts, or future architectural features were
changed.

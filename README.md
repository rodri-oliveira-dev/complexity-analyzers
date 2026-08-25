# ComplexityAnalysis.Analyzers

English | [Portugues (Brasil)](README.pt-BR.md)

ComplexityAnalysis.Analyzers is a standalone Roslyn analyzer package for surfacing algorithmic-complexity information in C# builds and IDEs.

The analyzer is developed directly from the repository root. The former `analyzer/` workspace boundary has been removed, and the repository now represents the analyzer product itself. The original `complexity-hints` project may still be used as external reference material when useful, but this package has no `ProjectReference`, binary dependency, or local package dependency on it.

## Current Status

Phase 1 through Phase 7 are implemented.

| Phase | Status | Delivered |
| --- | --- | --- |
| Phase 1 - Analyzer Foundation | Complete | Isolated `netstandard2.0` analyzer project, package layout, and `BIG9000` infrastructure probe. |
| Phase 2 - Complexity Model | Complete | Roslyn-free Big-O expression model, deterministic formatting, growth comparison, composition, independent variables, and `Unknown`. |
| Phase 3 - Roslyn Extraction | Complete | Intraprocedural method extraction from Roslyn syntax and semantics. |
| Phase 4 - BCL, LINQ, and Actionable Diagnostics | Complete | Semantic known-operation mappings for a documented BCL/LINQ subset, `BIG0001`, and actionable `BIG100x` diagnostics. |
| Phase 5 - Interprocedural Analysis | Complete | Bounded demand-driven propagation from safe source methods in the same compilation, source-call loop diagnostic `BIG1004`, cycle detection, cache, and internal limits. |
| Phase 6 - Recursion & Recurrence Solving | Complete | Bounded direct-recursion extraction, summation recurrences, simple exponential recursion, Master Theorem, a restricted/bounded Akra-Bazzi subset, fractional powers, and `BIG1005`. |
| Phase 7 - Configuration, Performance & NuGet Readiness | Complete | Analyzer config options, bounded public budgets, configurable threshold diagnostic `BIG1006`, performance harness, package contract tests, local consumer validation, and CI compatibility checks. |

The analyzer can follow supported source methods in the same compilation when dispatch is safe and the call is reached from the current root method. It can also solve selected direct recursive methods when base-case evidence, argument reduction, local work, and recurrence shape are all proven. It does not build a whole-compilation call graph, solve mutual recursion, or use `Microsoft.CodeAnalysis.Workspaces`.

## Diagnostics

| ID | Title | Category | Default severity | Enabled by default |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | No |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | Yes |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | Yes |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | Yes |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | Yes |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | Yes |
| `BIG1006` | Method complexity exceeds configured threshold | `Complexity` | `Info` | Yes |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | No |

`BIG0001` is informational and disabled by default. It reports a known method complexity estimate at the method identifier when explicitly enabled.

`BIG1005` reports supported direct recursive methods whose solved recurrence is exponential, such as Fibonacci-like recursion.

`BIG1006` reports when `complexity_analyzers.maximum_complexity` is configured and a method's known, comparable estimate exceeds that threshold. Unknown and incomparable estimates do not report.

`BIG9000` is an infrastructure probe. It proves the analyzer package loaded and ran when explicitly enabled; it is not a performance recommendation.

See [Analyzer Catalog](docs/en/analyzers.md).

## Interprocedural Analysis

Phase 5 adds source-method interprocedural analysis: when a caller invokes a supported method declared in the same Roslyn `Compilation`, the analyzer can analyze the callee once as a caller-independent template and substitute the caller's arguments into that template.

Supported source methods are ordinary C# methods with safe dispatch, including static methods, private methods, non-virtual methods, and sealed dispatch when the runtime target is proven. Known BCL and LINQ operations keep precedence over source-method analysis.

Traversal is demand-driven. A callee is analyzed only when the current root method reaches that invocation. The analyzer does not pre-scan every syntax tree or build a complete call graph. Public options expose bounded limits for call depth and methods expanded per root analysis.

Unsupported, unresolved, unsafe, budget-limited, cancelled, or cyclic calls remain `Unknown`. Direct recursion may be solved only by the bounded recurrence pipeline. Mutual recursion is detected but not solved.

Examples:

```text
A -> B O(n)           => A O(n)
loop n -> B O(n)     => O(n^2)
loop n -> B O(m)     => O(n * m)
B(left) + B(right)   => O(n + m)
B(constant)          => O(1)
A -> B -> C O(log n) => O(log n)
```

## Direct Recursion and Recurrences

The analyzer recognizes direct recursive calls by Roslyn symbol identity and requires compatible base-case evidence before solving. Recursive calls in mutually exclusive branches are counted per path, so binary-search-style code with two syntactic calls in exclusive branches remains `O(log n)`, not `O(n)`.

Supported recurrence families include:

- summation/decrement recurrences such as `T(n)=T(n-1)+1`, `T(n)=T(n-1)+n`, and `T(n)=T(n-1)+log n`;
- simple exponential direct recursion such as `2T(n-1)+1` and Fibonacci-like `T(n-1)+T(n-2)+1`;
- Master Theorem forms such as `T(n)=T(n/2)+1`, `2T(n/2)+n`, `2T(n/2)+n^2`, and `3T(n/2)+n`;
- a restricted/bounded Akra-Bazzi subset with scale-only recursive terms and polylogarithmic tolls, for example `T(n)=T(n/3)+T(2n/3)+n`.

Fractional polynomial powers are represented deterministically, so `3T(n/2)+n` reports `O(n^1.585)`. Unknown local work, missing base cases, non-reducing arguments, unsupported recurrence shapes, numerically inconclusive solving, cancellation, and mutual recursion remain `Unknown`. The analyzer does not claim full Akra-Bazzi, symbolic recurrence solving, memoization detection, or proof of general termination.

## Known Operation Scope

Phase 4 maps selected operations by Roslyn symbols, not by method names alone. Custom methods named `Contains`, `Where`, `ToList`, or similar remain unmapped unless their resolved symbol is part of the supported subset.

Implemented examples include:

- `List<T>.Contains`, `List<T>.IndexOf`, `List<T>.Sort`, `List<T>.Count`, and `List<T>` indexer.
- `Dictionary<TKey,TValue>.ContainsKey` and `Dictionary<TKey,TValue>.ContainsValue`.
- `HashSet<T>.Contains`.
- Array and string `Length`.
- LINQ `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max`, `Aggregate`.
- Deferred LINQ pipeline operations including `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct`, and `GroupBy`.

Deferred LINQ creation is not charged as a full enumeration. Enumeration cost is counted when a supported terminal operation or `foreach` consumes the pipeline.

Unsupported or unresolved operations produce `Unknown`. `Unknown` is not treated as `O(1)` or `O(n)`, and it is not reported by `BIG0001`.

## Quick Start

Prerequisites:

- .NET SDK `10.0.100` or a compatible SDK selected by `global.json`.
- A shell capable of running `dotnet` commands.

From the repository root:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

The package is documented as a local build/package source. Do not assume a NuGet.org release unless one exists independently.

See [Getting Started](docs/en/getting-started.md).

## Configuration

Phase 7 adds custom analyzer behavior options through Roslyn analyzer config. Diagnostic severities still use standard `dotnet_diagnostic.<RULE_ID>.severity`.

```ini
[*.cs]

complexity_analyzers.interprocedural_analysis = true
complexity_analyzers.recursion_analysis = true
complexity_analyzers.max_call_depth = 5
complexity_analyzers.max_methods_per_root = 32
complexity_analyzers.maximum_complexity = n_log_n

dotnet_diagnostic.BIG0001.severity = suggestion
dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
dotnet_diagnostic.BIG1005.severity = warning
dotnet_diagnostic.BIG1006.severity = warning
dotnet_diagnostic.BIG9000.severity = none
```

Defaults preserve existing behavior: interprocedural analysis and recursion analysis are enabled, `max_call_depth` is `5`, `max_methods_per_root` is `32`, and `maximum_complexity` is `none`. Threshold reporting only applies to known comparable estimates.

See [Configuration](docs/en/configuration.md).

## Performance and Compatibility

The analyzer is designed to be bounded: no network access, no analyzer hot-path filesystem I/O, no process launch, no telemetry, no mandatory whole-solution scan, bounded source-method traversal, bounded recurrence solving, concurrent execution, generated-code exclusion, and cancellation checks.

The repeatable performance harness is documented in [performance/README.md](performance/README.md). It validates structural behavior and compiler analyzer execution reporting with `ReportAnalyzer=true`; elapsed time is informational because hardware and CI runners vary.

CI validates local package consumption on currently supported SDK hosts from the project matrix: .NET 8 LTS, .NET 9 STS, and .NET 10 LTS. The package is not published to NuGet.org by this repository workflow.

## Architecture

The package is a compile-time analyzer package, not a runtime library. Consumer applications do not call analyzer classes at runtime.

```text
application source
        |
        | compiled by
        v
Roslyn compiler / IDE host
        |
        | loads
        v
ComplexityAnalysis.Analyzers
```

The analyzer assembly is packed under:

```text
analyzers/dotnet/cs/
```

See [Architecture](docs/en/architecture.md).

## Documentation

- [Getting Started](docs/en/getting-started.md)
- [Analyzer Catalog](docs/en/analyzers.md)
- [Architecture](docs/en/architecture.md)
- [Configuration](docs/en/configuration.md)
- [Documentacao em portugues](README.pt-BR.md)

## Limitations

- Source-method analysis is limited to ordinary safe-dispatch methods in the same compilation.
- There is no whole-compilation or whole-solution call graph.
- Recurrence solving is limited to supported direct-recursion shapes with base-case evidence.
- Mutual recursion is detected but not solved.
- Akra-Bazzi support is only a restricted/bounded Akra-Bazzi subset, not the full theorem.
- General characteristic polynomials, general numerical integration, MathNet, SymPy, and inherited solver projects are not used.
- No `CodeFixProvider` is included.
- `Microsoft.CodeAnalysis.Workspaces` is not used.
- Unsupported or unproven behavior prefers `Unknown` over unsafe guesses.

## License

MIT, matching the repository license declaration.

# ComplexityAnalysis.Analyzers

English | [Português (Brasil)](README.pt-BR.md)

[![Build & Tests](https://github.com/rodri-oliveira-dev/complexity-analyzers/actions/workflows/complexity-analyzers-ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/complexity-analyzers/actions/workflows/complexity-analyzers-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_complexity-analyzers&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_complexity-analyzers)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/ComplexityAnalysis.Analyzers?logo=nuget&label=NuGet)](https://www.nuget.org/packages/ComplexityAnalysis.Analyzers)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ComplexityAnalysis.Analyzers?logo=nuget&label=Downloads)](https://www.nuget.org/packages/ComplexityAnalysis.Analyzers)
[![GitHub Release](https://img.shields.io/github/v/release/rodri-oliveira-dev/complexity-analyzers?logo=github&label=Release)](https://github.com/rodri-oliveira-dev/complexity-analyzers/releases/latest)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_complexity-analyzers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_complexity-analyzers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/license/mit)

`ComplexityAnalysis.Analyzers` is a compile-time Roslyn analyzer for C# that estimates algorithmic complexity and reports diagnostics for costly patterns without adding runtime dependencies or instrumentation to consumer applications.

The analyzer is deliberately conservative: when complexity cannot be proven safely from the available syntax and semantic information, it returns `Unknown` instead of guessing.

## What it does

- Estimates Big-O complexity for supported C# methods using Roslyn syntax, symbols, and semantic information.
- Detects costly operations inside iteration, including linear lookups, collection materialization, and ordering.
- Understands a documented subset of BCL and LINQ operations by resolved symbol identity rather than method name alone.
- Performs bounded, demand-driven interprocedural analysis for safe source-method calls in the same compilation.
- Solves selected direct-recursion recurrence families, including decrement recurrences, simple exponential recursion, Master Theorem forms, and a restricted Akra-Bazzi subset.
- Supports configurable analysis budgets and a maximum-complexity threshold through `.editorconfig`/analyzer config.
- Runs as a normal Roslyn analyzer during builds and IDE analysis; consumer code does not call the analyzer at runtime.

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

`BIG0001` is an opt-in informational diagnostic that reports a known method-complexity estimate at the method identifier.

`BIG1005` reports supported direct recursive methods whose solved recurrence is exponential, such as Fibonacci-like recursion.

`BIG1006` reports when `complexity_analyzers.maximum_complexity` is configured and a known, comparable estimate exceeds the configured threshold. `Unknown` and incomparable estimates are not reported.

`BIG9000` is an infrastructure probe used to prove that the analyzer package loaded and executed. It is not a performance recommendation.

See the [Analyzer Catalog](docs/en/analyzers.md) for rule details.

## Analysis model

### Known BCL and LINQ operations

Known operations are mapped by Roslyn symbol identity. Custom methods named `Contains`, `Where`, `ToList`, or similar are not treated as BCL/LINQ operations unless their resolved symbol belongs to the supported subset.

Implemented examples include:

- `List<T>.Contains`, `List<T>.IndexOf`, `List<T>.Sort`, `List<T>.Count`, and the `List<T>` indexer.
- `Dictionary<TKey,TValue>.ContainsKey` and `Dictionary<TKey,TValue>.ContainsValue`.
- `HashSet<T>.Contains`.
- Array and string `Length`.
- LINQ `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max`, and `Aggregate`.
- Deferred LINQ operations including `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct`, and `GroupBy`.

Deferred LINQ pipeline creation is not charged as a full enumeration. Enumeration cost is counted when a supported terminal operation or `foreach` consumes the pipeline.

### Interprocedural analysis

When a caller invokes a supported source method declared in the same Roslyn `Compilation`, the analyzer can derive a caller-independent callee template and substitute the caller arguments into that template.

Supported source methods require safe dispatch, such as static methods, private methods, non-virtual methods, or sealed dispatch where the runtime target is proven. Known BCL/LINQ operations take precedence over source-method analysis.

Traversal is demand-driven and bounded. A callee is analyzed only when reached from the current root method. The analyzer does not pre-scan every syntax tree or construct a whole-compilation call graph.

Examples:

```text
A -> B O(n)           => A O(n)
loop n -> B O(n)     => O(n^2)
loop n -> B O(m)     => O(n * m)
B(left) + B(right)   => O(n + m)
B(constant)          => O(1)
A -> B -> C O(log n) => O(log n)
```

Unsupported, unresolved, unsafe, budget-limited, cancelled, or cyclic calls remain `Unknown`.

### Direct recursion and recurrence solving

The analyzer recognizes direct recursive calls by Roslyn symbol identity and requires compatible base-case evidence before solving a recurrence. Recursive calls in mutually exclusive branches are counted per path, so binary-search-style code remains `O(log n)` rather than being over-counted as linear.

Supported recurrence families include:

- decrement/summation forms such as `T(n)=T(n-1)+1`, `T(n)=T(n-1)+n`, and `T(n)=T(n-1)+log n`;
- simple exponential recursion such as `2T(n-1)+1` and Fibonacci-like `T(n-1)+T(n-2)+1`;
- Master Theorem forms such as `T(n)=T(n/2)+1`, `2T(n/2)+n`, `2T(n/2)+n^2`, and `3T(n/2)+n`;
- a restricted, bounded Akra-Bazzi subset with scale-only recursive terms and polylogarithmic tolls, for example `T(n)=T(n/3)+T(2n/3)+n`.

Fractional polynomial powers are represented deterministically, so `3T(n/2)+n` reports `O(n^1.585)`.

Missing base cases, non-reducing arguments, unsupported recurrence shapes, unknown local work, numerical inconclusiveness, cancellation, and mutual recursion remain `Unknown`.

## Build from source

Prerequisites:

- .NET SDK `10.0.400` or a compatible SDK selected by `global.json`.
- A shell capable of running `dotnet` commands.

From the repository root:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

The repository currently documents local package creation and consumption. Do not assume a NuGet.org release unless one exists independently.

See [Getting Started](docs/en/getting-started.md).

## Configuration

Analyzer behavior is configurable through Roslyn analyzer config. Diagnostic severities continue to use standard `dotnet_diagnostic.<RULE_ID>.severity` entries.

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

Defaults keep interprocedural and recursion analysis enabled, `max_call_depth` at `5`, `max_methods_per_root` at `32`, and `maximum_complexity` at `none`. Threshold reporting only applies to known, comparable estimates.

See [Configuration](docs/en/configuration.md).

## Performance and compatibility

The analyzer is designed to remain bounded and suitable for compiler/IDE execution: no network access, no analyzer hot-path filesystem I/O, no process launch, no telemetry, no mandatory whole-solution scan, bounded source-method traversal, bounded recurrence solving, concurrent execution, generated-code exclusion, and cancellation checks.

The repeatable performance harness is documented in [performance/README.md](performance/README.md). It validates structural behavior and compiler analyzer execution reporting with `ReportAnalyzer=true`; elapsed time is informational because hardware and CI runners vary.

CI validates local package consumption on .NET 8, .NET 9, and .NET 10 SDK hosts to catch analyzer loading and compatibility regressions.

## Architecture

The package is a compile-time analyzer, not a runtime library:

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
- [Release Quality Governance](docs/en/development/quality-gates.md)
- [Documentação em português](README.pt-BR.md)

## Limitations

- Source-method analysis is limited to ordinary safe-dispatch methods in the same compilation.
- There is no whole-compilation or whole-solution call graph.
- Recurrence solving is limited to supported direct-recursion shapes with base-case evidence.
- Mutual recursion is detected but not solved.
- Akra-Bazzi support is a restricted/bounded subset, not the full theorem.
- General characteristic polynomials, general numerical integration, MathNet, SymPy, and inherited solver projects are not used.
- No `CodeFixProvider` is included.
- `Microsoft.CodeAnalysis.Workspaces` is not used.
- Unsupported or unproven behavior prefers `Unknown` over unsafe guesses.

## License

MIT, matching the package license declaration.

# Architecture

[English](architecture.md) | [Português (Brasil)](../pt-BR/architecture.md)

`ComplexityAnalysis.Analyzers` is a standalone Roslyn analyzer package. The repository root is the product boundary: analyzer source lives under `src/`, tests under `tests/`, performance validation under `performance/`, and documentation under `docs/`.

The design favors conservative results, bounded analysis, deterministic behavior, and compatibility with compiler and IDE hosts.

## Analysis pipeline

```text
C# source
    |
    v
Roslyn syntax + SemanticModel
    |
    v
Executable-member analysis
    (supported C# executable constructs)
    |
    +-- analyzer configuration
    +-- input-size resolution
    +-- basic operations
    +-- loop bounds
    +-- known BCL/LINQ operations
    +-- safe source-method calls
    +-- direct-recursion extraction
    +-- recurrence solving
    +-- structural control-flow metrics
    |
    v
Complexity model
    |
    +-- atomic expressions
    +-- growth comparison
    +-- composition
    +-- Unknown
    |
    v
DiagnosticAnalyzer
    |
    +-- BIG0001 estimated complexity
    +-- BIG1001 linear lookup inside iteration
    +-- BIG1002 materialization inside iteration
    +-- BIG1003 ordering inside iteration
    +-- BIG1004 source call inside iteration
    +-- BIG1005 exponential recursive growth
    +-- BIG1006 configured threshold exceeded
    +-- BIG2001 cyclomatic threshold exceeded
    +-- BIG2002 maximum nesting threshold exceeded
    +-- BIG2003 method NLOC threshold exceeded
    +-- BIG2004 statement-count threshold exceeded
    +-- BIG2005 token-count threshold exceeded
    `-- BIG9000 execution probe
```

## Package boundary

The package is loaded by compiler and IDE hosts:

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

The analyzer project targets `netstandard2.0` for host compatibility and is packed as an analyzer asset under:

```text
analyzers/dotnet/cs/
```

It is not a runtime library. Consumer applications do not call analyzer classes, and Roslyn authoring dependencies are kept private rather than exposed transitively.

The repository build SDK is a separate concern. `global.json` selects SDK `10.0.400` for repository restore, build, tests, and pack, while supported compiler hosts are validated by installing the produced `.nupkg` into temporary consumer projects.

## Compatibility contracts

| Contract | Current value |
| --- | --- |
| Repository build SDK | `.NET SDK 10.0.400` from `global.json`. |
| Repository C# language version | `12.0`. |
| Analyzer target framework | `netstandard2.0`, preserved for compiler/IDE host compatibility. |
| Roslyn compiler API baseline | `Microsoft.CodeAnalysis.CSharp` `4.8.0`, resolving `Microsoft.CodeAnalysis.Common` `4.8.0`. |
| Analyzer authoring rules | `Microsoft.CodeAnalysis.Analyzers` `3.11.0`. |
| Supported SDK host matrix | `.NET 8`, `.NET 9`, and `.NET 10` consumer builds in CI. |
| Package analyzer path | `analyzers/dotnet/cs/ComplexityAnalysis.Analyzers.dll`. |
| Runtime package assets | No analyzer `lib/` asset and no transitive Roslyn dependency group. |

Compiling against newer Roslyn packages can introduce API references that older compiler or IDE hosts cannot load. Roslyn upgrades are therefore conservative: update proposals must keep Roslyn assets private, inspect the generated package, run the package/consumer contract tests, and validate every supported SDK host before merge.

## Repository structure

The main product areas are:

```text
src/ComplexityAnalysis.Analyzers/
    Analysis/
    Configuration/
    Diagnostics/
    Model/
    ComplexityAnalyzer.cs

tests/
performance/
docs/
```

The repository no longer depends on inherited implementation projects. The analyzer is developed and validated directly from the root solution.

## Complexity model

The Roslyn-free model lives under:

```text
src/ComplexityAnalysis.Analyzers/Model/
```

It represents complexity independently from C# syntax so mathematical behavior remains immutable, deterministic, and testable without compiler APIs.

The model supports:

- constant, polynomial/logarithmic, exponential, factorial, and `Unknown` forms;
- deterministic formatting, including fractional powers such as `O(n^1.585)`;
- growth comparison for comparable expressions;
- conservative incomparability for independent variables;
- sequential, nested, and branch composition.

When the analyzer cannot prove a safe result, the model preserves `Unknown` rather than coercing the operation to a guessed complexity class.

Cyclomatic Complexity and Maximum Control-Flow Nesting Depth are separate
integer structural metrics. They are computed from executable-member control
flow and are not combined with the Big-O complexity model or with each other.

## Roslyn analysis

The main analysis layer lives under:

```text
src/ComplexityAnalysis.Analyzers/Analysis/
```

Analysis starts from an internal executable-member abstraction and evaluates supported syntax and semantic facts. The analyzer normalizes ordinary methods, constructors, property/event accessors, operators, conversion operators, local functions, lambdas, anonymous methods, and supported expression-bodied forms into the same member pipeline when symbol identity, body ownership, and diagnostic location are available. Responsibilities include member/body extraction, input-size resolution, basic-operation classification, loop-bound analysis, known-operation mapping, source-call propagation, direct-recursion handling, and structural control-flow metrics such as Cyclomatic Complexity and Maximum Control-Flow Nesting Depth.

Representative components include:

- `ExecutableMember` for the analyzed member identity, body, display name, and diagnostic location;
- `ExecutableMemberSyntax` for executable-body boundaries that keep nested local functions, lambdas, and anonymous methods from inflating their lexical parent;
- `MethodComplexityExtractor` for method/body composition;
- `MethodAnalysisContext` for semantic and input-size context;
- `InputSizeResolver` for canonical dimensions such as `n`, `m`, `k`, `p`, and later variables;
- `BasicOperationAnalyzer` for proven basic work;
- `LoopBoundAnalyzer` for supported loop bounds;
- `KnownOperationComplexityAnalyzer` for supported BCL/LINQ costs.
- `CyclomaticComplexityAnalyzer` for structural path-complexity scoring that stays independent from Big-O.
- `MaximumNestingDepthAnalyzer` for maximum control-flow nesting depth scoring that stays independent from Big-O and Cyclomatic Complexity.

The analyzer does not require a whole-compilation or whole-solution call graph.
Nested executable constructs are analyzed as their own roots. A parent member does
not automatically traverse a local function, lambda, or anonymous method body
unless that nested executable is reached through a supported call path.

## Known BCL and LINQ operations

Known operations are resolved by Roslyn symbol identity, not text-only method names. This prevents a user-defined method named `Contains`, `Where`, or `ToList` from being treated as a framework operation accidentally.

Known-operation infrastructure lives under:

```text
src/ComplexityAnalysis.Analyzers/Analysis/KnownOperations/
```

Deferred LINQ operations such as `Where` and `OrderBy` are not charged as full enumeration merely when created. Enumeration or sorting cost is applied when supported consumption is proven, such as through a terminal operation or `foreach`.

Unsupported or unresolved operations remain `Unknown`.

## Interprocedural source calls

Interprocedural analysis can include the cost of a supported source callee in the caller estimate. Callee results can be represented as templates relative to the callee parameters and then substituted using caller arguments.

```text
Caller
  |
  v
Invocation resolution
  |
  +-- known BCL/LINQ operation
  |
  `-- supported source method
          |
      template/cache
          |
      argument substitution
          |
          v
Caller complexity
```

Traversal is demand-driven. A source method is analyzed only when reached from the current root, known-operation resolution does not apply, dispatch is safe, and the configured/internal analysis budget allows expansion.

Supported source dispatch includes static methods, private methods, ordinary non-virtual methods, sealed dispatch when the runtime target is proven, and direct local-function invocations. Unsafe virtual/interface dispatch, dynamic dispatch, delegates, reflection, external metadata-only methods, constructors, property access, event access, operators, conversions, lambdas, and anonymous methods as callees remain outside the supported interprocedural scope.

Cycles are detected conservatively. Direct recursion can be handled by the recurrence pipeline; mutual recursion is detected but not solved.

## Direct recursion and recurrence solving

Recurrence analysis lives under:

```text
src/ComplexityAnalysis.Analyzers/Analysis/Recursion/
```

The recursive pipeline separates detection, recurrence extraction, and solving. A supported recurrence requires semantic direct recursion, compatible base-case evidence, a provably reducing argument, and known local work.

Supported solver families include:

- summation/decrement recurrences;
- a bounded simple exponential subset;
- Master Theorem forms;
- a restricted, bounded Akra-Bazzi subset.

The implementation is deterministic and bounded. It does not perform general symbolic recurrence solving, general numerical integration, process execution, network access, MathNet/SymPy integration, or calls into external solver projects.

Unsupported shapes, missing base cases, non-reducing arguments, unknown local work, cancellation, numerical inconclusiveness, and mutual recursion remain `Unknown`.

## Configuration

Configuration is read through Roslyn `AnalyzerConfigOptionsProvider`; the analyzer does not parse `.editorconfig` files manually.

Public behavior options are:

- `complexity_analyzers.interprocedural_analysis`;
- `complexity_analyzers.recursion_analysis`;
- `complexity_analyzers.max_call_depth`;
- `complexity_analyzers.max_methods_per_root`;
- `complexity_analyzers.maximum_complexity`;
- `complexity_analyzers.maximum_cyclomatic_complexity`;
- `complexity_analyzers.cyclomatic_complexity_mode`;
- `complexity_analyzers.maximum_nesting_depth`;
- `complexity_analyzers.maximum_method_nloc`;
- `complexity_analyzers.maximum_statement_count`;
- `complexity_analyzers.maximum_token_count`.

Tree-specific analyzer config values override global values for that syntax tree. Invalid values fall back to documented defaults rather than producing analyzer failures.

See [Configuration](configuration.md) for details.

## Diagnostics

`ComplexityAnalyzer` exposes:

- `BIG0001` for opt-in method complexity estimates;
- `BIG1001` for supported linear lookups inside analyzable iteration;
- `BIG1002` for supported materialization inside analyzable iteration;
- `BIG1003` for supported consumed ordering inside analyzable iteration;
- `BIG1004` for supported input-dependent source calls inside analyzable iteration;
- `BIG1005` for supported direct recursion with solved exponential growth;
- `BIG1006` for known comparable estimates above a configured threshold;
- `BIG2001` for supported executable members above a configured Cyclomatic Complexity threshold;
- `BIG2002` for supported executable members above a configured Maximum Control-Flow Nesting Depth threshold;
- `BIG2003` for supported executable members above a configured NLOC threshold;
- `BIG2004` for supported executable members above a configured statement-count threshold;
- `BIG2005` for supported executable members above a configured token-count threshold;
- `BIG9000` as an opt-in execution probe.

Generated-code analysis is disabled and concurrent analyzer execution is enabled.

See the [Analyzer Catalog](analyzers.md) for rule-level behavior.

## Performance and package validation

The analyzer is designed for compiler/IDE execution and keeps hot paths free of network access, process execution, telemetry, and mandatory whole-solution scans.

Performance validation uses deterministic synthetic workloads and structural invariants rather than narrow machine-specific timing thresholds. The compiler `ReportAnalyzer=true` path is used to verify analyzer execution reporting.

Performance budgets are part of the analyzer contract. Source-method traversal defaults to call depth `5` and `32` uncached source-method expansions per root, with public hard maximums of `16` and `128`. Recurrence solving remains bounded by supported recurrence shapes and fixed numerical limits for the restricted Akra-Bazzi solver. At budget boundaries, the analyzer keeps affected results conservative, normally `Unknown`.

CI treats deterministic structural checks as hard gates: bounded traversal, cache ownership, cancellation behavior, generated-code exclusion, package layout, and consumer compatibility. Elapsed time and compiler analyzer timing are informational trend signals unless repeated evidence shows a material regression. The detailed regression policy and local commands are documented in [`performance/README.md`](../../performance/README.md).

Package contract validation ensures that `ComplexityAnalysis.Analyzers.dll` is packed under `analyzers/dotnet/cs/`, not `lib/`, and that authoring dependencies do not become consumer runtime dependencies.

CI also validates package consumption with .NET 8, .NET 9, and .NET 10 SDK hosts. Each compatibility job restores and builds a temporary consumer using the real `.nupkg`, enables `BIG9000` as a package-load probe, enables `BIG1006` on a known quadratic method, rejects analyzer load failures, and checks that the package contributes analyzer assets rather than compile/runtime assets.

## Why no Workspaces dependency

`Microsoft.CodeAnalysis.Workspaces` is intentionally absent. The project does not implement a `CodeFixProvider`, solution loading, whole-project workspace analysis, or other IDE workspace features that require that dependency.

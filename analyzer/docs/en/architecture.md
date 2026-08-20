# Architecture

[English](architecture.md) | [Portugues (Brasil)](../pt-BR/architecture.md)

`ComplexityAnalysis.Analyzers` is an isolated Roslyn analyzer package workspace. Through Phase 7, it contains analyzer infrastructure, a Roslyn-free complexity model, method extraction, semantic known-operation mapping, bounded source-method interprocedural analysis, bounded direct-recursion recurrence solving, analyzer configuration, performance validation, package contract validation, and public diagnostics.

## Current Pipeline

```text
C# source
    |
    v
Roslyn Syntax + SemanticModel
    |
    v
Analysis
    |
    +-- analyzer config options
    +-- input-size resolution
    +-- basic operations
    +-- loop bounds
    +-- known BCL/LINQ operations
    +-- safe source-method calls
    +-- direct-recursion extraction
    +-- recurrence solving
    +-- method extraction
    |
    v
Complexity Model
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
    `-- BIG9000 infrastructure probe
```

## Analyzer Package Boundary

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

The analyzer project targets `netstandard2.0` for host compatibility. It is packed as an analyzer asset under:

```text
analyzers/dotnet/cs/
```

It is not packed as a normal runtime library. Consumer applications do not call analyzer classes at runtime.

## Project Isolation

The inherited `complexity-hints` implementation remains a conceptual reference. There is no `ProjectReference`, binary dependency, or local package dependency from the isolated analyzer to inherited projects.

This keeps the analyzer package small, deterministic, and independent. Roslyn dependencies used to author the analyzer are private package assets and should not become transitive consumer dependencies.

## Complexity Model

The model lives under:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Model/
```

It is intentionally Roslyn-free. It represents complexity values independently from C# syntax so mathematical operations remain immutable, deterministic, and testable without compiler APIs.

Implemented model behavior includes:

- atomic forms such as constant, polynomial-logarithmic, exponential, factorial, and `Unknown`;
- formatting for common Big-O forms, including deterministic fractional powers such as `O(n^1.585)`;
- growth comparison for same-variable expressions;
- conservative incomparability for independent variables;
- sequential, nested, and branching composition.

## Roslyn Extraction

The analysis layer lives under:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/
```

It starts from one method at a time. The analyzer can follow supported source-method calls on demand and solve selected direct recursion, but it does not build a whole-compilation call graph, solve mutual recursion, or inspect unrelated method bodies.

Main responsibilities are split across:

- `MethodComplexityExtractor`: coordinates method, block, statement, loop, branch, and switch analysis.
- `MethodAnalysisContext`: stores method-local semantic context, canonical input-size variables, local loop-bound facts, and cancellation.
- `InputSizeResolver`: maps eligible parameters to deterministic variables such as `n`, `m`, `k`, `p`, and `v5`.
- `BasicOperationAnalyzer`: classifies proven constant-time statements and expressions and delegates supported known operations.
- `LoopBoundAnalyzer`: recognizes supported constant, linear, logarithmic, and known enumerable loop bounds.
- `KnownOperationComplexityAnalyzer`: composes known BCL/LINQ invocation, property, element-access, terminal operation, and consumed deferred-pipeline costs.

## Interprocedural Source Calls

Interprocedural analysis means the analyzer can include the cost of a supported source callee in the caller's estimate. The callee result is cached as a template relative to the callee's own parameters, then argument substitution maps that template back to the caller's dimensions.

Conceptual flow:

```text
Caller
  |
  v
Invocation resolution
  |
  +-- Known BCL/LINQ
  |
  `-- Source method
          |
      cache/template
          |
      substitution
          |
          v
Caller complexity
```

Cycle boundary:

```text
Cycle detected
      |
      v
Unknown
      |
      v
Unknown unless direct recursion is separately extracted and solved
```

Traversal is demand-driven. A source callee is analyzed only when an invocation is visited from the current root method, BCL/LINQ known-operation resolution does not apply, dispatch is safe, and the internal budget allows expansion. The analyzer does not pre-analyze every method and does not create a complete compilation graph.

Safe source dispatch includes static methods, private methods, non-virtual ordinary methods, and sealed dispatch when the runtime target is proven. Interface dispatch, unsafe virtual dispatch, dynamic dispatch, delegate invocation, reflection, external metadata-only methods, constructors, properties, operators, local functions, and lambdas as independent call targets remain out of scope.

Public configuration exposes bounded call depth and methods expanded per root analysis. The defaults are call depth `5` and methods per root `32`; hard public limits are `16` and `128`. Unknown results remain conservative for unresolved calls, unsafe dispatch, unavailable source, unproven argument binding, budget boundaries, cancellation, and cycles. Direct recursion can be solved only by the recurrence pipeline below. Mutual recursion is detected but not solved.

## Direct Recursion and Recurrence Solving

Recurrence infrastructure lives under:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/Recursion/
```

The extractor and solvers are separate. `RecursiveCallAnalyzer` identifies semantically direct recursive invocations and summarizes recursive execution paths. `RecurrenceExtractor` requires base-case evidence, selects the recurrence dimension, excludes direct recursive invocations from local-work cost, and builds an internal `RecurrenceRelation`. `RecurrenceSolver` tries bounded solvers and returns explicit solved, unsupported, invalid, or numerically inconclusive results.

Implemented solver families are summation/decrement recurrences, a simple constant-coefficient exponential subset, Master Theorem, and a restricted/bounded Akra-Bazzi subset. Numerical work is deterministic and bounded by internal iteration caps. The analyzer does not perform general numerical integration, subprocess execution, reflection-based solving, I/O, network access, MathNet, SymPy, Workspaces, whole-compilation recurrence scans, or inherited solver project calls.

Mutually exclusive recursive branches are path-sensitive: binary-search-style branches with one recursive call per branch produce one recursive term per path. Sequential recursive calls on the same path may add multiplicity.

Unsupported recurrence shapes, unknown local work, missing base cases, non-reducing arguments, cancellation, numerical inconclusiveness, and mutual recursion remain `Unknown`.

## Known Operations

Known operation infrastructure lives under:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/KnownOperations/
```

Mappings carry semantic identity, complexity, execution kind, provenance, metadata, and case information where relevant. Resolution uses Roslyn symbols and operation identities, not text-only method names.

Deferred LINQ operations such as `Where` and `OrderBy` are charged as setup when created. Their enumeration or sorting cost is counted when a supported terminal operation or `foreach` consumes the pipeline.

Unsupported or unresolved invocations remain `Unknown`.

## Diagnostic Layer

`ComplexityAnalyzer` exposes:

- `BIG0001` at the method identifier when the estimated method complexity is known and the diagnostic is enabled.
- `BIG1001` at a linear lookup invocation inside an analyzable iteration.
- `BIG1002` at a materializing invocation inside an analyzable iteration.
- `BIG1003` at a deferred ordering invocation only when supported consumption is proven inside an analyzable iteration.
- `BIG1004` at a supported source-method call with known input-dependent complexity inside an analyzable iteration.
- `BIG1005` at a supported recursive method whose solved direct recurrence is exponential.
- `BIG1006` at a method identifier when `complexity_analyzers.maximum_complexity` is configured and the known comparable estimate exceeds the threshold.
- `BIG9000` once per compilation when explicitly enabled.

Generated code analysis is disabled, concurrent execution is enabled, and analyzer hot paths must remain free of I/O, network access, process execution, and reflection-heavy behavior.

## Configuration Layer

Configuration is read through Roslyn analyzer config APIs from `AnalyzerConfigOptionsProvider`; the analyzer does not parse `.editorconfig` files manually. Tree-specific options override global options for that syntax tree.

The public behavior options are:

- `complexity_analyzers.interprocedural_analysis`;
- `complexity_analyzers.recursion_analysis`;
- `complexity_analyzers.max_call_depth`;
- `complexity_analyzers.max_methods_per_root`;
- `complexity_analyzers.maximum_complexity`.

Invalid values fall back to defaults and do not report analyzer failures. Diagnostic severity remains standard Roslyn `dotnet_diagnostic.<RULE_ID>.severity` configuration.

## Performance and Package Validation

Performance validation uses deterministic synthetic workloads and structural invariants rather than narrow millisecond thresholds. The compiler `ReportAnalyzer=true` path is used to verify analyzer execution reporting when supported by the toolchain.

The package contract keeps `ComplexityAnalysis.Analyzers.dll` under `analyzers/dotnet/cs/`, not under `lib/`. Roslyn authoring dependencies are private assets and are not exposed transitively to consumers. The current package embeds debug symbols in the analyzer DLL and does not emit a `.snupkg`.

## Why No Workspaces

`Microsoft.CodeAnalysis.Workspaces` is intentionally absent. The analyzer does not implement a `CodeFixProvider`, whole-project graph analysis, solution loading, or IDE workspace features that would justify that dependency.

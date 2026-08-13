# Architecture

[English](architecture.md) | [Portugues (Brasil)](../pt-BR/architecture.md)

`ComplexityAnalysis.Analyzers` is an isolated Roslyn analyzer package workspace. Through Phase 4, it contains analyzer infrastructure, a Roslyn-free complexity model, intraprocedural extraction, semantic known-operation mapping, and public diagnostics.

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
    +-- input-size resolution
    +-- basic operations
    +-- loop bounds
    +-- known BCL/LINQ operations
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
- formatting for common Big-O forms;
- growth comparison for same-variable expressions;
- conservative incomparability for independent variables;
- sequential, nested, and branching composition.

## Roslyn Extraction

The analysis layer lives under:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/
```

It analyzes one method at a time. The extractor does not build a call graph, follow project-local calls, solve recursion, or inspect other method bodies.

Main responsibilities are split across:

- `MethodComplexityExtractor`: coordinates method, block, statement, loop, branch, and switch analysis.
- `MethodAnalysisContext`: stores method-local semantic context, canonical input-size variables, local loop-bound facts, and cancellation.
- `InputSizeResolver`: maps eligible parameters to deterministic variables such as `n`, `m`, `k`, `p`, and `v5`.
- `BasicOperationAnalyzer`: classifies proven constant-time statements and expressions and delegates supported known operations.
- `LoopBoundAnalyzer`: recognizes supported constant, linear, logarithmic, and known enumerable loop bounds.
- `KnownOperationComplexityAnalyzer`: composes known BCL/LINQ invocation, property, element-access, terminal operation, and consumed deferred-pipeline costs.

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
- `BIG9000` once per compilation when explicitly enabled.

Generated code analysis is disabled, concurrent execution is enabled, and analyzer hot paths must remain free of I/O, network access, process execution, and reflection-heavy behavior.

## Why No Workspaces

`Microsoft.CodeAnalysis.Workspaces` is intentionally absent. Phase 4 does not implement a `CodeFixProvider`, project-wide analysis, solution loading, or IDE workspace features that would justify that dependency.

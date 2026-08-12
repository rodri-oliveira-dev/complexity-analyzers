# Architecture

[English](architecture.md) | [Português (Brasil)](../pt-BR/architecture.md)

`ComplexityAnalysis.Analyzers` is an isolated Roslyn analyzer package workspace. Through Phase 3, it contains three layers: analyzer infrastructure, a Roslyn-free complexity model, and intraprocedural extraction from C# syntax and semantics into that model.

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
    | implemented internally
    X product Big-O diagnostics not wired yet
    |
DiagnosticAnalyzer
    `-- BIG9000 infrastructure probe
```

The extraction layer currently returns internal `ComplexityExpression` values. It does not report product diagnostics to users.

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

The analyzer project targets `netstandard2.0` for broad host compatibility. It is packed as an analyzer asset under:

```text
analyzers/dotnet/cs/
```

It is not packed as a normal runtime library. Consumer applications do not call analyzer classes at runtime.

## Project Isolation

The inherited `complexity-hints` implementation remains a conceptual reference:

```text
inherited implementation
        |
        | conceptual/reference source
        v
ComplexityAnalysis.Analyzers
```

There is no `ProjectReference`, binary dependency, or local package dependency from the isolated analyzer to inherited projects. This keeps the analyzer package small, deterministic, and independent.

## Complexity Model

The model lives under:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Model/
```

It is intentionally Roslyn-free. It represents complexity values independently from C# syntax so the mathematical operations can remain small, immutable, deterministic, and testable without compiler APIs.

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

It analyzes one method at a time. This is intraprocedural by design in Phase 3. The extractor does not build a call graph, follow project-local calls, solve recursion, or inspect other method bodies.

Main responsibilities are split across:

- `MethodComplexityExtractor`: coordinates method, block, statement, loop, branch, and switch analysis.
- `MethodAnalysisContext`: stores method-local semantic context, canonical input-size variables, local loop-bound facts, and cancellation.
- `InputSizeResolver`: maps eligible parameters to deterministic variables such as `n`, `m`, `k`, `p`, and `v5`.
- `BasicOperationAnalyzer`: classifies only proven constant-time statements and expressions.
- `LoopBoundAnalyzer`: recognizes supported constant, linear, and logarithmic loop bounds.

## Extraction Examples

These examples describe internal extraction results covered by tests. They are not user-facing diagnostics in Phase 3.

```csharp
void M(int[] items)
{
    foreach (var item in items)
    {
        var x = item + 1;
    }
}
```

Internal result: `O(n)`.

```csharp
void M(int[] items)
{
    foreach (var outer in items)
    {
        foreach (var inner in items)
        {
            var x = outer + inner;
        }
    }
}
```

Internal result: `O(n^2)`.

```csharp
void M()
{
    Visit();
}
```

Internal result: `Unknown`, because project-local method calls are not resolved in Phase 3.

## Unknown

`Unknown` is a safety decision. It means the analyzer could not prove a safe asymptotic complexity for the construct.

`Unknown` does not mean `O(1)`, does not mean `O(n)`, and does not by itself represent a performance problem. It prevents unsupported behavior from being turned into unsafe guesses.

## Current Diagnostic Layer

The current `DiagnosticAnalyzer` exposes exactly one diagnostic:

- `BIG9000` - analyzer execution probe.

It is registered through a compilation action, disabled by default, and reports at most once per compilation when explicitly enabled. It is infrastructure-only and does not consume the Phase 3 complexity extraction result.

## Why No Workspaces

`Microsoft.CodeAnalysis.Workspaces` is intentionally absent. Phase 3 does not implement a `CodeFixProvider`, project-wide analysis, solution loading, or IDE workspace features that would justify that dependency.

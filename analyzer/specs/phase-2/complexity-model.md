# Phase 2 — Complexity Model

## Status

Specification initialized. Production implementation has not started.

Phase 2 depends on the completed Phase 1 analyzer foundation. The current repository state was checked before this specification was written: the analyzer workspace builds in Release, the Phase 1 tests pass, and the analyzer project still packs as a Roslyn analyzer package asset.

## Objective

Define and implement a lightweight asymptotic complexity model inside `ComplexityAnalysis.Analyzers`.

The model must be independent from Roslyn extraction, immutable, deterministic, small, cheap to allocate and compare, suitable for frequent analyzer execution, able to represent the main asymptotic classes, and safe when multiple independent input variables coexist.

The preferred location is:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Model/
```

No second NuGet package is introduced in this phase.

## Context

Phase 1 created the isolated analyzer product boundary, analyzer packaging, tests, and the infrastructure-only diagnostic `BIG9000`. Phase 2 adds only the in-memory model needed by later analyzer phases to talk about complexity. It does not inspect C# syntax, semantic symbols, loops, calls, LINQ, or control flow.

The inherited project under `src/` remains a reference implementation only. It must not be referenced by `ProjectReference`, binary reference, local package, or transitive dependency.

## Reference Implementation

The following inherited files were read as conceptual references only:

| File | Concepts considered |
| --- | --- |
| `src/ComplexityAnalysis.Core/Complexity/ComplexityExpression.cs` | Expression model, primitive complexity forms, composition shape, Big-O notation examples |
| `src/ComplexityAnalysis.Core/Complexity/PolyLogComplexity.cs` | Unified `n^k · log^j(n)` representation and multiplication of same-variable polylog terms |
| `src/ComplexityAnalysis.Core/Complexity/ComplexityComposition.cs` | Sequential, nested, and branching composition semantics |
| `docs/articles/expression-types.md` | Public taxonomy of expression types and larger features intentionally deferred |

Intentional differences from the inherited implementation:

- Do not copy the full hierarchy.
- Do not include visitor APIs in the initial model unless tests prove a need.
- Do not include numerical evaluation, variable substitution, recurrence types, builders, conditional expressions, min/best-case operations, or symbolic algebra engines.
- Do not preserve multiplicative coefficients for initial Big-O forms.
- Do not preserve logarithm bases for logarithmic equivalence; constant log-base factors are normalized away.
- Do not depend on `ComplexityAnalysis.Core` or any inherited project.

## Architectural Decisions

Phase 2 should use a compact closed model based conceptually on:

```text
ConstantComplexity

PolynomialLogComplexity
    polynomialDegree
    logExponent
    variable

ExponentialComplexity
    base
    variable

FactorialComplexity
    variable

UnknownComplexity

CompositeComplexity
    left
    operation
    right
```

This shape captures the required asymptotic classes without transporting the heavier expression system from the inherited Core project.

`PolynomialLogComplexity` is the preferred representation for constants, logs, linear, n log n, and polynomial terms because `n^k · log^j(n)` covers the common analyzer output with a small value shape.

The implementation may use sealed records, readonly structs, or another immutable value-oriented design, but public equality must be value equality and construction must normalize obvious equivalent Big-O forms.

## Supported Complexity Forms

Phase 2 must represent at least:

- `O(1)`
- `O(log n)`
- `O(n)`
- `O(n log n)`
- `O(n²)`
- `O(n³)`
- `O(n^k)`
- `O(2^n)`
- `O(b^n)`
- `O(n!)`
- `Unknown`

The polylog form is:

```text
n^k · log^j(n)
```

Examples:

| `k` | `j` | Big-O |
| --- | --- | --- |
| 0 | 0 | `O(1)` |
| 0 | 1 | `O(log n)` |
| 1 | 0 | `O(n)` |
| 1 | 1 | `O(n log n)` |
| 2 | 0 | `O(n²)` |
| 3 | 0 | `O(n³)` |
| 4 | 0 | `O(n^4)` |

Multiplicative constants are deliberately not part of the initial public model. Inputs such as `O(2n)` and `O(100n)` normalize to `O(n)`.

## Variables

Define an immutable value object for input variables.

It must:

- represent symbols such as `n`, `m`, `V`, and `E`;
- compare by value;
- be deterministic when formatted;
- validate that the symbol is non-empty and suitable for Big-O output;
- avoid Roslyn semantics entirely.

It must not know about:

- `IParameterSymbol`
- `ISymbol`
- `SyntaxNode`
- `SemanticModel`

Mapping C# parameters or symbols to model variables belongs to Phase 3.

The model must allow different variables to coexist without assuming a growth relationship between them.

## Growth Comparison

Define an explicit comparison result:

```text
Less
Equivalent
Greater
Incomparable
```

For the same variable, support at least:

```text
O(1)
<
O(log n)
<
O(n)
<
O(n log n)
<
O(n²)
<
O(n³)
<
O(b^n)
<
O(n!)
```

Rules:

- Same-variable polylog forms compare by polynomial degree first, then log exponent.
- Same-variable exponential forms grow faster than same-variable polylog forms.
- Same-variable factorial forms grow faster than same-variable exponential forms.
- Different exponential bases greater than 1 may be compared for the same variable by base.
- Equal normalized forms are `Equivalent`.
- Unknown compared with anything is `Incomparable` unless both sides are the same unknown singleton/value.
- Independent variables are not ordered automatically.

Therefore:

```text
Compare(O(n), O(m)) => Incomparable
```

The model must never invent a greater/less relationship between independent variables without an explicit relationship supplied by a later phase.

## Composition Rules

Define three operations:

```text
Sequential
Nested
Branching
```

Semantics:

- `Sequential`: `T1 + T2`
- `Nested`: `T1 × T2`
- `Branching`: `max(T1, T2)`

When comparison is safe, simplify to the dominant or combined result:

```text
Sequential(O(n), O(n²)) => O(n²)
Branching(O(n), O(n²)) => O(n²)
Nested(O(n), O(n)) => O(n²)
Nested(O(n), O(log n)) => O(n log n)
```

For independent variables, preserve composition:

```text
Sequential(O(n), O(m)) => O(n + m)
Nested(O(n), O(m)) => O(n · m)
Branching(O(n), O(m)) => O(max(n, m))
```

Minimum simplification expectations:

- `Sequential(O(1), O(f(n)))` may simplify to `O(f(n))` when `O(f(n))` is known to dominate constant work.
- `Nested(O(1), O(f(n)))` may simplify to `O(f(n))`.
- `Branching(O(f(n)), O(f(n)))` simplifies to `O(f(n))`.
- `Nested` of same-variable polylog terms adds polynomial and log exponents.
- Operations involving unknown must not silently produce a known result unless the result is mathematically guaranteed.

Do not simplify a relationship that cannot be justified.

## Unknown Complexity

The model must represent inconclusive analysis explicitly.

`Unknown` does not mean `O(1)`.

`Unknown` must not be silently converted into any known complexity. In operations where a known result cannot be guaranteed, preserve `Unknown` or a composite expression containing `Unknown`, according to the public composition contract chosen in implementation.

The formatting for a pure unknown result is:

```text
Unknown
```

## Big-O Formatting

Formatting must be deterministic and must not depend on `CurrentCulture`.

Required examples:

```text
O(1)
O(log n)
O(n)
O(n log n)
O(n²)
O(n³)
O(n^4)
O(2^n)
O(n!)
O(n + m)
O(n · m)
O(max(n, m))
Unknown
```

Formatting rules:

- Use invariant formatting for numeric bases and exponents.
- Use deterministic operand ordering only when it is mathematically safe and documented.
- Use compact polynomial superscripts for 2 and 3.
- Use caret notation for other powers, such as `O(n^4)`.
- Do not include normalized-away multiplicative constants.

## Immutability

All model values must be immutable after construction.

No mutable static state is allowed. Shared canonical values such as `O(1)` or `Unknown` are allowed if they are immutable.

Collections, if introduced, must be immutable or private defensive copies.

## Performance Constraints

The model will run in analyzer hot paths in later phases.

Requirements:

- Avoid network access, file I/O, process execution, reflection-heavy behavior, and global environment reads.
- Prefer small objects and simple comparisons.
- Keep allocation costs predictable.
- Avoid unbounded simplification recursion.
- Avoid culture-sensitive formatting.
- Make equality and hashing stable.
- Keep operations deterministic under concurrent analyzer execution.

## Dependencies

The model lives inside:

```text
ComplexityAnalysis.Analyzers
```

Allowed dependencies are the existing analyzer project dependencies and BCL APIs already available to `netstandard2.0`.

Do not add:

- `ProjectReference` to inherited projects
- local inherited package references
- binary references to inherited assemblies
- `MathNet.Numerics`
- `SymPy`
- `Microsoft.CodeAnalysis.Workspaces`
- `Microsoft.CodeAnalysis.CSharp.Workspaces`

No Roslyn APIs are required by the model itself.

## Out of Scope

The following are explicitly out of scope for Phase 2:

- Roslyn extraction
- `SyntaxNode` analysis
- `SemanticModel` analysis
- `ControlFlowGraph`
- loops
- LINQ/BCL mappings
- interprocedural analysis
- call graph
- recurrence solving
- Master Theorem
- Akra-Bazzi
- MathNet
- SymPy
- amortized analysis
- probabilistic analysis
- parallel analysis
- memory complexity
- confidence scoring
- `CodeFixProvider`
- Workspaces

## Testing Strategy

Tests for Phase 2 must cover the public behavior of the model without invoking Roslyn extraction.

Required coverage:

- Construction/factory methods for each supported atomic form.
- Value equality for variables and complexity values.
- Formatting examples listed in this specification.
- Same-variable growth comparison.
- Independent-variable incomparability.
- Sequential composition simplification and preservation cases.
- Nested composition simplification and preservation cases.
- Branching composition simplification and preservation cases.
- Unknown representation and propagation/preservation behavior.
- Immutability expectations observable through the public API.
- Existing Phase 1 analyzer tests, including `BIG9000`, continue to pass.
- Release build, Release test, and analyzer pack continue to pass.

## Acceptance Criteria

- AC01 — modelo fica dentro do projeto isolado do analyzer.
- AC02 — nenhuma dependência dos projetos originais é criada.
- AC03 — modelo não depende de APIs Roslyn.
- AC04 — tipos do modelo são imutáveis.
- AC05 — `O(1)` pode ser representado.
- AC06 — `O(log n)` pode ser representado.
- AC07 — `O(n)` pode ser representado.
- AC08 — `O(n log n)` pode ser representado.
- AC09 — `O(n^k)` pode ser representado.
- AC10 — `O(b^n)` pode ser representado.
- AC11 — `O(n!)` pode ser representado.
- AC12 — `Unknown` pode ser representado.
- AC13 — variáveis diferentes podem coexistir.
- AC14 — comparação de crescimento não inventa relação entre variáveis independentes.
- AC15 — `Sequential` possui semântica definida.
- AC16 — `Nested` possui semântica definida.
- AC17 — `Branching` possui semântica definida.
- AC18 — Big-O formatting é determinístico.
- AC19 — testes cobrem os comportamentos públicos do modelo.
- AC20 — `BIG9000` e todos os testes da Fase 1 continuam funcionando.
- AC21 — `dotnet build` Release passa.
- AC22 — `dotnet test` Release passa.
- AC23 — `dotnet pack` continua gerando um Roslyn Analyzer package válido.
- AC24 — nenhum projeto herdado foi modificado.

## Definition of Done

Phase 2 is done when the model is implemented under `ComplexityAnalysis.Analyzers`, all acceptance criteria pass, tests document the supported public behavior, the package remains a valid Roslyn analyzer package, inherited projects remain unchanged, and the handoff identifies the next phase or step with only minimal semantic context.

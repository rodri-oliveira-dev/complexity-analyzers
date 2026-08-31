# C# Halstead Metrics

[English](halstead-metrics.md) | [Português (Brasil)](../pt-BR/halstead-metrics.md)

`ComplexityAnalysis.Analyzers` defines an internal, C#-specific Halstead metric
capability for supported executable members. It is implemented on top of the
same executable-member abstraction used by the public analyzer rules, so member
ownership, nested executable isolation, generated-code policy, cancellation, and
concurrent analyzer execution keep the same architectural boundaries.

No public Halstead diagnostic or `.editorconfig` threshold is exposed yet. The
project does not currently have a single derived Halstead metric with a
sufficiently clear, defensible maintainability threshold for all C# projects.
Adding a `BIG2xxx` rule only for feature symmetry would make the public analyzer
contract noisier without evidence-backed guidance.

## Primitive Counts

The primitive counts are:

```text
n1 = distinct operators
n2 = distinct operands
N1 = total operators
N2 = total operands
```

Counts are source-based, deterministic, and scoped to one supported executable
member. Comments, whitespace, preprocessor directives, and syntax trivia do not
contribute.

## Derived Metrics

The project convention derives:

```text
vocabulary                  n = n1 + n2
length                      N = N1 + N2
calculated length           N^ = n1 * log2(n1) + n2 * log2(n2)
volume                      V = N * log2(n)
difficulty                  D = (n1 / 2) * (N2 / n2)
effort                      E = D * V
estimated implementation time T = E / 18
estimated delivered bugs    B = V / 3000
```

Degenerate inputs are handled explicitly so empty and trivial members produce
finite, non-negative values. Formatting uses invariant culture and round-trip
`G17` formatting for deterministic numeric text.

## Classification

Operators are C# syntax constructs that perform, select, invoke, access, create,
transfer control, or otherwise change executable meaning. Punctuation is not
counted mechanically; it counts only when it is part of a documented operation.

The convention includes arithmetic, comparison, equality, logical, bitwise,
assignment, null-coalescing, null-conditional, invocation, member/element access,
creation, lambda/expression-body arrows, range/index, collection expression
spread, pattern combinators, switch arms and guards, control flow, `await`,
`yield`, `throw`, `using`, `lock`, `fixed`, `checked`, and `unchecked` constructs.

Operands are source-level values, symbolic references, declared value names, and
type names that participate in executable code. Identifier identities use Roslyn
symbol information when it is already available and useful; unresolved names
fall back to stable syntax text. Literal identities use logical literal values
where Roslyn exposes them cheaply. Renaming identifiers may change operand
identity, but it does not change operator counts.

## Ownership

Nested local functions, lambdas, and anonymous methods are independent
executable roots. A parent member does not include the nested executable body in
its Halstead counts. Header syntax that belongs to the parent expression or
statement, such as a lambda arrow or local-function declaration name, may still
be counted for the parent according to the classification convention.

## Non-Equivalence

These values are a reproducible convention for this project. No exact numerical
equivalence is claimed with Lizard, Visual Studio, or any other Halstead
implementation unless a future compatibility test proves that equivalence.

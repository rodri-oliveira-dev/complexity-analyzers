# Phase 6 — Recursion & Recurrence Solving

## Status

Complete.

Phase 6 closes with bounded direct-recursion extraction and recurrence solving implemented inside the isolated analyzer. Supported direct recursion requires semantic identity, base-case evidence, proven argument reduction, path-aware recursive-call multiplicity, and known non-recursive local work. Unsupported, invalid, numerically inconclusive, and mutual-recursive cases remain `Unknown`.

## Objective

Transform supported direct recursive methods into internal recurrence relations and solve them when there is enough syntactic and semantic evidence to do so safely.

The phase starts from the Phase 5 behavior where recursive cycles are detected and expansion stops at an `Unknown`/cycle boundary. Phase 6 may replace that boundary with a known complexity only for direct recursion patterns that satisfy this specification.

Example:

```csharp
int Sum(int n)
{
    if (n <= 1)
        return 1;

    return Sum(n - 1) + n;
}
```

Conceptually this can produce:

```text
T(n) = T(n - 1) + O(1)
```

or the non-recursive local work actually derived by the current analyzer pipeline, and then solve the recurrence.

## Context

Phase 5 added demand-driven interprocedural analysis with per-compilation caching, bounded call expansion, caller-independent templates, argument substitution, source-method safe dispatch, and cycle boundaries. It intentionally did not solve recursion.

The current analyzer has:

- `ComplexityExpression` as the public internal output model for Big-O estimates.
- `PolynomialLogComplexity` with integer polynomial and log exponents.
- `ExponentialComplexity` with invariant `double` bases.
- Phase 4 and Phase 5 diagnostics, including `BIG0001` through `BIG1004`, plus `BIG9000`.
- Direct and mutual recursion currently terminating safely as `Unknown`.

The inherited solver under `src/ComplexityAnalysis.Solver/` is reference-only. Phase 6 must not reference, wrap, or copy it wholesale.

## Architecture

Conceptual flow:

```text
recursive method
      |
      v
RecursiveCallAnalyzer
      |
      +-- base case evidence
      |
      +-- recursive call shapes
      |
      +-- non-recursive local work
      |
      v
RecurrenceRelation
      |
      v
RecurrenceSolver
      |
      +-- summation/decrement
      +-- constant-coefficient subset
      +-- Master Theorem
      +-- restricted Akra-Bazzi
      |
      v
RecurrenceSolution
      |
      v
ComplexityExpression
      |
      v
existing analyzer pipeline
```

Extraction and solving are separate responsibilities:

- `RecursiveCallAnalyzer` inspects one direct recursive method and extracts evidence.
- `RecurrenceRelation` stores normalized mathematical structure.
- `RecurrenceSolver` consumes only recurrence model values and produces a solver result.
- Integration code converts a solved recurrence into `ComplexityExpression`.

Do not hide extraction decisions inside theorem-specific solvers.

## Recurrence Model

Create an internal recurrence model specific to recurrence analysis. It must not be the same type hierarchy as `ComplexityExpression`.

The model should include concepts equivalent to:

- `RecurrenceRelation`
- `RecurrenceTerm`
- `RecurrenceArgumentRelation`
- `RecurrenceSolution`
- `RecurrenceSolverKind`

A recursive term must represent at least:

```text
T(n - c)
T(n / b)
```

or normalized equivalents:

```text
decrement: c
scale factor: 1 / b
multiplicity: a
```

The representation must support examples such as:

```text
2T(n / 2) + O(n)
T(n / 3) + T(2n / 3) + O(n)
```

without depending on textual recurrence parsing.

The recurrence model may store:

- target method symbol identity;
- primary recurrence variable;
- recursive terms with multiplicity;
- argument relation kind;
- constant decrement or scale factor;
- non-recursive local work as `ComplexityExpression`;
- base-case evidence metadata;
- extraction status or failure reason for tests.

The model must remain immutable or effectively immutable once constructed.

## Recursive Call Identification

Direct recursion must be identified semantically with Roslyn symbols, not method names.

A call is direct recursion only when the invoked `IMethodSymbol` resolves to the same method definition being analyzed, using the same semantic identity policy as Phase 5 cache keys where appropriate.

Calls to a homonymous method must not be treated as recursion:

```csharp
void M(int n) { Other.M(n - 1); }
```

is not direct recursion unless semantic identity proves it calls the current method.

Mutual recursion, such as `A -> B -> A`, remains outside the solver.

## Base Case Evidence

Do not solve a recursive method merely because an argument appears to decrease.

The extractor must require base-case evidence compatible with the recurrence variable and recursive paths. Recognizable examples include:

```csharp
if (n <= 1)
    return ...;

if (n == 0)
    return ...;
```

Equivalent semantically safe forms may be supported when they can be proven from syntax and semantic constants.

Base-case evidence must show that at least one path exits without making a recursive call under a bounded condition for the recurrence variable. The phase does not need to prove general program termination.

If sufficient base-case evidence is missing, the recurrence result is `Unknown`.

## Argument Reduction

Recognize only transformations that are provably reducing.

Initial supported forms:

```text
n - c
n + (-c)
n / b
n * q
```

where:

- `c > 0` is a compile-time constant;
- `b > 1` is a compile-time constant;
- `0 < q < 1` is provable as a compile-time constant;
- the expression refers to the same recurrence variable.

Do not accept:

```text
T(n)
T(n + 1)
T(unknown(n))
```

as reducing. These outcomes must be `Unknown`.

For Phase 6, simple non-recursive Phase 5 bindings such as `n / 2` and `n - 1` are not enough by themselves; recursive reductions must satisfy this stricter evidence policy.

## Local Work

The non-recursive local work of a recursive method is computed through the existing analyzer pipeline, excluding the recursive invocation cost itself.

Recursive invocations must not be classified as unknown operations while calculating local work. They should be treated as recurrence terms and removed or neutralized from the local-work expression in a controlled way.

Example:

```text
MergeSort:
2 recursive calls
+
merge loop O(n)
=>
local work O(n)
```

If local work cannot be extracted without unsupported constructs unrelated to recursion, the recurrence is `Unknown`.

## Recurrence Extraction

The extractor must account for execution paths. Do not count all `InvocationExpressionSyntax` nodes in the method body as if they execute on the same path.

For mutually exclusive branches:

```csharp
if (condition)
    return Search(left);
else
    return Search(right);
```

there is one recursive call per path, not two. If both reductions are equivalent to `n / 2`, the recurrence is:

```text
T(n) = T(n / 2) + O(1)
```

not:

```text
T(n) = 2T(n / 2) + O(1)
```

Sequential recursive calls on the same path may add multiplicity:

```csharp
Visit(n / 2);
Visit(n / 2);
```

can produce:

```text
2T(n / 2) + local work
```

The path model may be conservative and return `Unknown` when control flow cannot be summarized safely. It must not over-count branch alternatives.

## Summation Recurrences

Support the initial decrement form:

```text
T(n) = T(n - c) + f(n)
```

where `c` is a positive constant and `f(n)` belongs to the supported polylog subset:

```text
O(n^k log^j n)
```

For this subset:

```text
T(n) = O(n^(k + 1) log^j n)
```

Required examples:

```text
T(n) = T(n - 1) + O(1)     => O(n)
T(n) = T(n - 1) + O(log n) => O(n log n)
T(n) = T(n - 1) + O(n)     => O(n²)
```

Constant decrement values other than one do not change the Big-O class.

## Constant-Coefficient Recurrences

Support a lightweight constant-coefficient subset without MathNet, eigendecomposition, or general root finding.

Prioritize:

```text
aT(n - c) + O(polylog)
```

and order-2 recurrences with positive recursive coefficients when a simple closed form can be derived safely.

Required examples:

```text
T(n) = 2T(n - 1) + O(1)        => O(2^n)
T(n) = T(n - 1) + T(n - 2) + O(1) => O(phi^n)
```

The Fibonacci-like result should be represented with a deterministic exponential base close to phi:

```text
O(1.618^n)
```

using the model's deterministic numeric formatting policy.

Do not implement:

- general characteristic polynomials;
- arbitrary degree root finding;
- matrix eigendecomposition;
- complex-root classification.

If a recurrence exceeds the supported subset, return `Unknown`.

## Master Theorem

Support:

```text
T(n) = aT(n / b) + f(n)
```

where:

- `a >= 1`;
- `b > 1`;
- `f(n)` is in the supported polylog subset.

Compute:

```text
p = log_b(a)
```

using BCL `Math.Log` only.

Implement the three standard cases when mathematically provable:

- Case 1: `f(n)` is polynomially smaller than `n^p`, result `O(n^p)`.
- Case 2: `f(n) = O(n^p log^j n)`, result `O(n^p log^(j + 1) n)`.
- Case 3: `f(n)` is polynomially larger than `n^p` and regularity is guaranteed for supported polylog tolls, result `O(f(n))`.

Use an explicit comparison epsilon and a documented polynomial-gap threshold. If comparison is too close or the toll cannot be classified, return `NumericallyInconclusive` or `Unsupported`, which integrates as `Unknown`.

Required examples:

```text
T(n) = T(n / 2) + O(1)  => O(log n)
T(n) = 2T(n / 2) + O(n) => O(n log n)
T(n) = 2T(n / 2) + O(1) => O(n)
T(n) = 3T(n / 2) + O(n) => O(n^1.585)
```

## Restricted Akra-Bazzi

Support only recurrences of the form:

```text
T(n) = Σ ai T(bi n) + g(n)
```

where:

- every `ai > 0`;
- every `0 < bi < 1`;
- `g(n) = O(n^k log^j n)`;
- there are no perturbation terms `h_i(n)`;
- all argument reductions are proven scale reductions.

Find `p` such that:

```text
Σ ai * bi^p = 1
```

Use a simple deterministic bounded numerical method such as bisection. The implementation must define:

- comparison epsilon;
- maximum bracket expansion;
- maximum bisection iterations;
- behavior when no valid bracket is found.

Do not implement symbolic integrals. For supported polylog tolls, classify the result by comparing `k` with `p`:

- `k < p`: result `O(n^p)`;
- `k == p`: result `O(n^p log^(j + 1) n)`;
- `k > p`: result `O(n^k log^j n)`.

If the comparison is numerically inconclusive, return `NumericallyInconclusive`.

Required coverage includes at least one unequal split, such as:

```text
T(n) = T(n / 3) + T(2n / 3) + O(n)
```

within the restricted model.

## Solver Result

The solver must return an explicit result equivalent to:

```text
Solved
Unsupported
Invalid
NumericallyInconclusive
```

When solved, include:

- resulting `ComplexityExpression`;
- `RecurrenceSolverKind`;
- minimal metadata for tests and diagnostics, such as recurrence family, computed exponent, exponential base, or matched theorem case.

Never convert a solver error, invalid recurrence, unsupported shape, numerical failure, or cancellation into a known complexity.

## Fractional Polynomial Powers

The current `PolynomialLogComplexity` stores polynomial degrees as `int`. Master Theorem and restricted Akra-Bazzi can produce positive real exponents, for example:

```text
O(n^1.585)
```

Phase 6 must add the smallest model extension needed to represent positive real polynomial exponents.

Requirements:

- preserve existing integral formatting:
  - `O(n²)`
  - `O(n³)`
  - `O(n^4)`
- format non-integer exponents deterministically:
  - `O(n^1.585)`
- avoid noisy values such as:
  - `O(n^1.584962500721156)`
- use `CultureInfo.InvariantCulture`;
- normalize exponents within an explicit integer tolerance;
- round or format non-integer exponents with a documented fixed precision, initially three decimal places unless tests justify a different precision;
- remove trailing zeros after the decimal point when safe and deterministic;
- reject non-finite, zero, or negative polynomial exponents in non-constant polynomial-log terms unless a later spec expands the model.

Growth comparison and composition must be updated for fractional same-variable polylog terms.

## Unknown Policy

`Unknown` remains conservative.

Return `Unknown` when:

- base-case evidence is missing;
- recursive argument reduction is not proven;
- recursive call identity is ambiguous;
- branches cannot be summarized safely;
- local non-recursive work is unknown;
- recurrence model validation fails;
- solver kind is unsupported;
- numerical result is inconclusive;
- cancellation occurs;
- recursion is mutual rather than direct.

Do not infer a fallback complexity from naming, comments, or method shape.

## Mutual Recursion Policy

Mutual recursion remains explicitly outside Phase 6 solving.

Example:

```text
A -> B -> A
```

continues to produce an `Unknown`/`CycleBoundary` outcome.

The existing Phase 5 safety guarantees must remain: no stack overflow, no cache deadlock, no permanent cache poisoning, and no whole-compilation SCC graph requirement.

Do not introduce SCC/Tarjan or whole-call-graph construction only to solve mutual recursion.

## Diagnostics Integration

`BIG0001` must report recursive complexity when a direct recursive method is solved and the diagnostic is enabled.

Unknown, unsupported, invalid, and numerically inconclusive recurrence results must not be reported by `BIG0001`.

Phase 6 also specifies:

```text
BIG1005 — Exponential recursive growth
```

Purpose:

Identify a recursive method whose solved recurrence has exponential time complexity.

Descriptor policy:

- ID: `BIG1005`
- Title: `Exponential recursive growth`
- Category: `Complexity`
- Severity: `Info`
- Enabled by default: follow the current `BIG100x` policy, which is enabled by default, unless implementation evidence records a contrary decision before diagnostics are added.
- Message format: informative and non-prescriptive.

Conceptual message:

```text
Recursive method '{0}' has estimated exponential time complexity {1}.
```

Do not automatically recommend memoization.

Do not emit `BIG1005` for:

- `Unknown`;
- polynomial results;
- logarithmic results;
- unsafe or inconclusive recurrence results;
- unresolved mutual recursion.

This specification step does not change diagnostics. Descriptor implementation belongs to a later Phase 6 step.

## Numerical Stability

Numerical code must be deterministic and bounded.

Define internal documented constants for:

- comparison epsilon;
- polynomial gap threshold for theorem comparisons;
- integer normalization tolerance;
- maximum bisection iterations;
- maximum bracket expansion attempts;
- non-integer exponent display precision.

Do not depend on `CurrentCulture`.

Do not use unbounded numerical loops.

Do not swallow numerical failures and report a known complexity. Use `NumericallyInconclusive` and integrate as `Unknown`.

## Performance Constraints

Analyzer performance remains a functional requirement.

Phase 6 must:

- analyze only methods demanded by the existing pipeline;
- avoid whole-compilation call graph construction;
- avoid whole-solution analysis;
- avoid file I/O, network access, process execution, reflection-heavy behavior, and runtime profiling in analyzer hot paths;
- respect `CancellationToken` during syntax traversal, semantic lookup, recurrence extraction, local-work analysis, and solving;
- keep recurrence extraction bounded by method syntax size and existing Phase 5 budgets;
- keep numerical loops bounded;
- use immutable or local mutable data only;
- preserve concurrent analyzer execution safety;
- avoid cache designs that can deadlock on recursive cycles.

## Out of Scope

Explicitly out of scope:

- general symbolic recurrence solver;
- general characteristic polynomials;
- matrix eigendecomposition;
- `MathNet.Numerics`;
- `SymPy`;
- general numerical integration;
- full Akra-Bazzi;
- recurrence perturbations `h_i(n)`;
- mutual recursion solving;
- whole-compilation SCC graph;
- proof of program termination;
- memoization detection;
- `CodeFixProvider`;
- `Microsoft.CodeAnalysis.Workspaces`;
- `Microsoft.CodeAnalysis.CSharp.Workspaces`;
- `ProjectReference` to `ComplexityAnalysis.Solver`;
- `ProjectReference` to `ComplexityAnalysis.Core`;
- production diagnostics changes in the specification-only step.

## Testing Strategy

Tests must cover recurrence model behavior, extraction behavior, solver behavior, analyzer integration, diagnostics integration when implemented, and regression safety for previous phases.

Required categories:

- semantic direct-recursion detection;
- homonymous non-recursive calls;
- base-case evidence recognition and absence;
- reducing and non-reducing recursive arguments;
- path-aware recursive call multiplicity;
- recursive calls excluded from local-work unknown classification;
- summation/decrement solving;
- supported constant-coefficient solving;
- Master Theorem cases;
- restricted Akra-Bazzi with unequal split;
- fractional polynomial power formatting and comparison;
- solver result kinds;
- numerical bound and tolerance behavior;
- mutual recursion remains `Unknown`;
- `BIG0001` reports known solved recursion only when enabled;
- `BIG1005` reports only exponential recursive growth when implemented;
- Phase 1-5 diagnostics do not regress;
- cancellation;
- Release build, Release tests, and pack validation;
- package audit for forbidden dependencies and inherited references.

## Documentation Requirements

When implementation lands, update English and pt-BR documentation.

Documentation must describe:

- direct-recursion solving scope;
- required base-case evidence;
- supported argument reductions;
- supported recurrence families;
- fractional exponent formatting;
- conservative `Unknown` outcomes;
- mutual recursion exclusion;
- `BIG0001` recursive estimates;
- `BIG1005` when implemented;
- explicit exclusions for MathNet, SymPy, Workspaces, inherited project references, full Akra-Bazzi, and general symbolic solving.

Documentation must not claim support for mutual recursion, memoization detection, or general recurrence solving.

## Acceptance Criteria

- AC01 — direct recursive calls are identified semantically.
- AC02 — calls to homonymous methods are not confused with recursion.
- AC03 — base case evidence is required.
- AC04 — non-reducing recursion results in `Unknown`.
- AC05 — recursive calls in mutually exclusive branches are not added incorrectly.
- AC06 — sequential recursive calls can produce multiplicity.
- AC07 — local non-recursive work is extracted without marking recursive invocation as `Unknown`.
- AC08 — recurrence model is independent from the inherited project.
- AC09 — `T(n)=T(n-1)+O(1)` => `O(n)`.
- AC10 — `T(n)=T(n-1)+O(n)` => `O(n²)`.
- AC11 — `T(n)=T(n-1)+O(log n)` => `O(n log n)`.
- AC12 — `T(n)=2T(n-1)+O(1)` => `O(2^n)` when supported.
- AC13 — Fibonacci-like supported recurrence => exponential result near `phi^n`.
- AC14 — binary search recurrence => `O(log n)`.
- AC15 — `2T(n/2)+O(n)` => `O(n log n)`.
- AC16 — `2T(n/2)+O(1)` => `O(n)`.
- AC17 — `3T(n/2)+O(n)` can represent a fractional exponent correctly.
- AC18 — restricted Akra-Bazzi covers at least one supported unequal split.
- AC19 — numerical loops have deterministic limits.
- AC20 — unsupported recurrence => `Unknown`.
- AC21 — mutual recursion continues as `Unknown`.
- AC22 — `BIG0001` reports recursive result when known.
- AC23 — `BIG1005` reports only exponential recursive growth when applicable.
- AC24 — previous diagnostics do not regress.
- AC25 — Phase 5 cache/cycle handling remains safe.
- AC26 — `CancellationToken` is respected.
- AC27 — MathNet is not added.
- AC28 — SymPy is not added.
- AC29 — Workspaces is not added.
- AC30 — no `ProjectReference` to inherited projects.
- AC31 — Release build passes.
- AC32 — Release tests pass.
- AC33 — package remains valid.
- AC34 — documentation EN/pt-BR is updated.
- AC35 — upstream remains intact.

## Definition of Done

Phase 6 is done when bounded direct-recursion recurrence extraction and solving are implemented inside `analyzer/`, only supported patterns with base-case evidence produce known complexity, recursive local work is separated from recursive terms, fractional polynomial powers are deterministic, unsupported and mutual recursive patterns remain `Unknown`, `BIG0001` and `BIG1005` follow the diagnostic policy, previous diagnostics and Phase 5 cache/cycle safety do not regress, Release build/test/pack pass, documentation is updated in English and pt-BR, no forbidden dependency or inherited project reference is added, and inherited upstream code remains untouched.

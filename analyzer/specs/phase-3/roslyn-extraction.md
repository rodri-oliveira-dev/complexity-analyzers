# Phase 3 — Roslyn Extraction

## Status

Complete.

Phase 3 closes with deterministic intraprocedural Roslyn extraction implemented under the isolated analyzer project. The extractor maps one method body to the Phase 2 `ComplexityExpression` model, handles proven constant work, deterministic input-size variables, linear and logarithmic loops, branching worst-case composition, cancellation, and conservative `Unknown` results for unsupported work.

The isolated analyzer workspace has been validated in Release, packaged as a Roslyn analyzer asset, audited for prohibited dependencies and premature later-phase features, and checked against the inherited upstream solution without modifying upstream files.

This phase defines the Roslyn extraction layer only. It must not introduce product diagnostics, BCL/LINQ mappings, call graph analysis, recursion handling, or changes to the Phase 2 complexity model.

## Objective

Transform the syntax and semantic information for a single C# method into a `ComplexityExpression`:

```text
C# syntax + SemanticModel
        ↓
ComplexityExpression
```

The extraction is deterministic, conservative, cancellation-aware, intraprocedural, and implemented exclusively inside the isolated analyzer workspace under `analyzer/`.

The extractor uses only the Complexity Model created in Phase 2 for representation and composition.

## Context

Phase 1 created the isolated Roslyn analyzer package and the disabled-by-default `BIG9000` execution probe.

Phase 2 created the Roslyn-free complexity model used by later phases. That model is the only output language for Phase 3.

The inherited implementation under `src/ComplexityAnalysis.Roslyn/Analysis/` is a reference for concepts only. It is not a dependency, must not be referenced by `ProjectReference`, binary reference, local package, or transitive dependency, and must not be copied wholesale.

Phase 3 should be implemented in small, separated components rather than recreating the monolithic inherited extractor.

## Input Contract

The primary input is one method-like declaration and its `SemanticModel`.

Supported entry points should cover ordinary method declarations first:

```text
MethodDeclarationSyntax + SemanticModel + CancellationToken
```

Expression-bodied methods are included when the body can be analyzed locally.

The extractor may later be adapted for constructors, local functions, operators, or accessors, but those are not required by this phase unless tests introduce them explicitly.

The caller provides a `CancellationToken` when available. All syntax walking and semantic lookup paths must observe it.

The extractor must not require:

- `Compilation`
- `Workspace`
- file-system access
- network access
- process execution
- global analyzer state
- project-wide method indexing

## Output Contract

The output is a `ComplexityExpression` from the Phase 2 model.

Required output behavior:

- a method with no input-dependent work returns `O(1)`;
- known sequential work is composed with `ComplexityComposer.Sequential`;
- loop iteration cost is composed with `ComplexityComposer.Nested`;
- conditional and switch branch alternatives are composed with `ComplexityComposer.Branching`;
- inconclusive analysis returns or propagates `Unknown`;
- `Unknown` is never converted to `O(1)` or `O(n)`;
- no diagnostic is emitted by the extraction layer in Phase 3.

The extractor returns a single conservative worst-case expression for the current method body.

## Architecture

Phase 3 should add an `Analysis/` area inside the analyzer project, conceptually:

```text
analyzer/src/ComplexityAnalysis.Analyzers/
├── Model/
└── Analysis/
    ├── MethodComplexityExtractor.cs
    ├── MethodAnalysisContext.cs
    ├── InputSizeResolver.cs
    ├── BasicOperationAnalyzer.cs
    └── LoopBoundAnalyzer.cs
```

Names may change, but responsibilities must remain separated:

- `MethodComplexityExtractor` coordinates analysis of one method body and composes statement results.
- `MethodAnalysisContext` stores method-local semantic context.
- `InputSizeResolver` maps eligible parameters and size expressions to canonical `ComplexityVariable` values.
- `BasicOperationAnalyzer` classifies only proven constant-time syntax and expressions.
- `LoopBoundAnalyzer` recognizes deterministic linear and logarithmic loop bounds.

Avoid a single stateful walker equivalent to the inherited `RoslynComplexityExtractor`. Small local visitors or helpers are allowed when their state is scoped to one method or one construct.

No new project is introduced for Phase 3.

## Semantic Context

The method analysis context must be immutable or effectively local to one method analysis.

It should know only:

- `SemanticModel`;
- current `IMethodSymbol`;
- a mapping from eligible parameter symbols to `ComplexityVariable`;
- `CancellationToken` when needed by the code path.

The context may maintain method-local facts needed by deterministic loop recognition, such as simple local aliases for size values, if those facts are derived inside the current method and do not require global data-flow.

The context must not include:

- `CallGraph`;
- recursion depth;
- recurrence or solver state;
- BCL or LINQ mappings;
- progress infrastructure;
- global mutable caches;
- confidence scoring.

## Input Size Variables

The extractor assigns canonical complexity variables deterministically:

```text
n
m
k
p
...
```

The first eligible input-size parameter receives `n`, the second receives `m`, the third receives `k`, and so on. Additional names must be deterministic and documented by implementation tests.

The final Big-O notation should use canonical variables rather than raw parameter names when raw names would make output unstable or noisy.

Eligible input-size candidates include:

- arrays, where size means array `Length`;
- `string`, where size means string `Length`;
- collection or enumerable parameters when the type is safely recognized as size-bearing;
- integral parameters used directly as loop bounds.

Do not assign a complexity variable to a parameter merely because of its position in the signature.

Parameters that are clearly irrelevant to input size should not consume canonical names, including:

- `bool`;
- `enum`;
- delegates;
- cancellation tokens;
- ordinary scalar values not used as loop bounds.

Example:

```csharp
void Process(int[] items, string pattern)
```

maps conceptually to:

```text
items   => n
pattern => m
```

For:

```csharp
void Process(bool enabled, int[] items)
```

`enabled` does not consume `n`; `items` maps to `n`.

The exact type-recognition implementation belongs to Phase 3 implementation steps, but the observable behavior must remain deterministic.

## Basic Operations

Phase 3 may classify the following as `O(1)` only when the syntax and semantics make that classification safe:

- trivial local declaration;
- assignment;
- primitive arithmetic;
- primitive comparison;
- increment and decrement;
- `return`;
- array `Length`;
- string `Length`;
- array element access.

Do not generalize these rules to:

- any property access;
- any indexer access;
- any method invocation;
- any object creation;
- any collection operation.

Array element access is `O(1)` only when semantic information proves the receiver is an array. String indexing is not automatically included unless explicitly implemented and tested as a safe intrinsic.

Unsupported or unresolved expressions that may contain non-constant work must produce `Unknown` according to the Unknown policy.

## Method and Block Analysis

A method with no work dependent on input size returns `O(1)`:

```csharp
int GetValue() => 42;
```

Result:

```text
O(1)
```

The extractor analyzes only the current method body.

Sequential statements are combined with:

```csharp
ComplexityComposer.Sequential(left, right)
```

An empty block or a block containing only proven constant-time operations is `O(1)`.

The extractor must not follow method calls, inspect other method bodies, or use project-wide call information.

## Linear Loops

Phase 3 must support deterministic linear loop patterns.

Minimum `for` patterns:

```csharp
for (var i = 0; i < n; i++)
for (var i = 0; i < items.Length; i++)
```

Minimum `foreach` pattern:

```csharp
foreach (var item in items)
```

where `items` is an eligible input-size parameter.

Loop complexity is:

```text
iterations × body
```

and must be represented with:

```csharp
ComplexityComposer.Nested(iterationComplexity, bodyComplexity)
```

A constant-bound loop does not become `O(n)`:

```csharp
for (var i = 0; i < 10; i++)
```

If the body is `O(1)`, the loop remains `O(1)`.

Nested loops compose multiplicatively:

```text
O(n) × O(n) => O(n²)
O(n) × O(m) => O(n · m)
```

These simplifications rely on the Phase 2 composer.

## Logarithmic Loops

Phase 3 must recognize only proven multiplicative progressions.

Minimum patterns:

```csharp
for (var i = 1; i < n; i *= 2)
for (var i = n; i > 1; i /= 2)
```

Result:

```text
O(log n)
```

The progression and bound must refer to the same loop variable and an eligible input-size bound. The factor or divisor must be a constant greater than one.

If the pattern is not sufficiently proven, return `Unknown`.

Do not infer logarithmic complexity from method names, comments, binary-search-like code shape, or arbitrary division inside the loop body.

## While and Do-While

`while` and `do-while` support is limited to deterministic patterns where initialization, condition, and progress can be proven within the current method.

Linear example:

```csharp
var i = 0;
while (i < n)
{
    i++;
}
```

Result:

```text
O(n)
```

Logarithmic example:

```csharp
var i = 1;
while (i < n)
{
    i *= 2;
}
```

Result:

```text
O(log n)
```

Equivalent decrementing or divisive forms may be supported when the implementation can prove the same relation.

If the bound or progress cannot be proven, the result is `Unknown`.

Never invent `O(n)` for an unprovable `while` or `do-while`.

## Branching

`if`/`else` uses worst-case branch composition:

```text
max(T_true, T_false)
```

and should call:

```csharp
ComplexityComposer.Branching(trueBranch, falseBranch)
```

A missing `else` branch contributes `O(1)` unless the condition itself is `Unknown`.

`switch` uses worst-case composition over all analyzed cases:

```text
max(T_case1, T_case2, ...)
```

and should fold case results with `ComplexityComposer.Branching`.

Example:

```csharp
if (condition)
{
    // O(n)
}
else
{
    // O(n²)
}
```

Result:

```text
O(n²)
```

provided the branch costs can be determined intraprocedurally.

## Method Invocations

In Phase 3, any invocation whose complexity is not intrinsically known by a basic rule of this phase is unresolved.

Unresolved invocations must produce or propagate `Unknown`.

Phase 3 must not implement special cases for:

- `Enumerable.Any`;
- `Contains`;
- `OrderBy`;
- `ToList`;
- `Dictionary`;
- `HashSet`;
- `List` operations;
- project-local method calls.

BCL and LINQ mappings belong to Phase 4.

Following project methods belongs to Phase 5 interprocedural analysis.

Recursive calls remain unsupported in Phase 3 and must not trigger recurrence analysis.

## Unknown and Unsupported Constructs

`Unknown` means the extractor could not determine complexity safely.

Unknown must never be interpreted as:

```text
O(1)
O(n)
```

Unsupported constructs should produce `Unknown` when they may affect complexity. Examples include:

- unsupported method invocation;
- unsupported object construction;
- unsupported property or indexer access;
- unrecognized loop bounds;
- unproven loop progress;
- unsupported pattern matching or query syntax when it may execute user code;
- exception constructs whose body behavior cannot be represented by current rules.

Unsupported constructs that are syntactically present but proven irrelevant to runtime work may remain `O(1)` only when tests document the rule.

The propagation policy must respect the Phase 2 model, where composition involving `Unknown` remains inconclusive.

## Cancellation

The extractor must observe cancellation:

- before analyzing a method;
- while iterating statements;
- before semantic model lookups;
- inside loop and input-size resolution helpers;
- during any syntax traversal.

Cancellation should be passed through from Roslyn analyzer callbacks when Phase 3 is integrated.

Cancellation handling must not swallow `OperationCanceledException` in a way that turns a cancelled analysis into a known complexity.

## Performance Constraints

Analyzer performance is a functional requirement.

Phase 3 must:

- analyze only the current method body;
- avoid project-wide scans;
- avoid global mutable state;
- avoid file I/O;
- avoid network access;
- avoid process execution;
- avoid reflection-heavy behavior;
- avoid unbounded recursion in syntax traversal;
- keep semantic lookups targeted and cancellable;
- allocate predictably for hot analyzer execution;
- remain deterministic under concurrent analyzer execution.

Any caches introduced later must be local, immutable, or concurrency-safe and must not leak cross-compilation state.

## Reference Implementation

The following inherited files may inform Phase 3 concepts:

| File | Concepts considered |
| --- | --- |
| `src/ComplexityAnalysis.Roslyn/Analysis/RoslynComplexityExtractor.cs` | method/block traversal, sequential composition, loop/body multiplication, branch maximums, basic operation categories |
| `src/ComplexityAnalysis.Roslyn/Analysis/LoopAnalyzer.cs` | loop-bound recognition concepts for `for`, `foreach`, `while`, and `do-while` |
| `src/ComplexityAnalysis.Roslyn/Analysis/AnalysisContext.cs` | method-local semantic context and canonical variable naming concept |

These files are reference-only. The Phase 3 implementation must not depend on `ComplexityAnalysis.Roslyn`, `ComplexityAnalysis.Core`, `ComplexityAnalysis.Solver`, inherited BCL mappings, inherited call graph code, or inherited recurrence code.

## Intentional Differences

Phase 3 deliberately differs from the inherited `RoslynComplexityExtractor`:

- split extraction responsibilities across small components instead of one monolithic syntax walker;
- use the Phase 2 `ComplexityExpression` model exclusively;
- return `Unknown` for unresolved calls instead of applying name-based heuristics;
- do not default unresolved calls or unknown loop bounds to `O(1)` or `O(n)`;
- do not consult BCL or LINQ mappings;
- do not follow methods in the same project;
- do not construct or query a call graph;
- do not detect or solve recursion;
- do not include confidence scoring or review flags;
- do not include progress callbacks;
- do not include solver, recurrence, Master Theorem, or Akra-Bazzi concepts;
- recognize only safe basic operations, not arbitrary properties or indexers.

## Out of Scope

The following are explicitly out of scope for Phase 3:

- BCL mappings;
- LINQ mappings;
- product diagnostics;
- `BIG000x`;
- `BIG100x`;
- call graph;
- interprocedural analysis;
- recursion;
- Master Theorem;
- Akra-Bazzi;
- advanced `ControlFlowGraph` analysis;
- advanced data-flow analysis;
- amortized complexity;
- parallel complexity;
- probabilistic complexity;
- memory complexity;
- confidence scoring;
- `CodeFixProvider`;
- Workspaces;
- solver integration;
- calibration;
- changing the Phase 2 Complexity Model;
- changing `BIG9000`.

`BIG9000` from Phase 1 remains unchanged and disabled by default.

## Testing Strategy

Phase 3 tests should validate observable extraction behavior through small C# snippets compiled with Roslyn test helpers.

Required test coverage:

- trivial method returns `O(1)`;
- expression-bodied trivial method returns `O(1)`;
- eligible input parameters receive deterministic canonical variables;
- irrelevant parameters do not consume canonical variables;
- sequential constant work remains `O(1)`;
- sequential variable-dependent work uses `ComplexityComposer.Sequential`;
- `foreach` over an eligible input maps to `O(n)` with `O(1)` body;
- linear `for` over an integral bound maps to `O(n)`;
- linear `for` over `array.Length` maps to `O(n)`;
- constant-bound `for` with `O(1)` body remains `O(1)`;
- nested same-input loops map to `O(n²)`;
- nested independent-input loops map to `O(n · m)`;
- multiplicative `for` maps to `O(log n)`;
- proven linear `while` maps to `O(n)`;
- proven logarithmic `while` maps to `O(log n)`;
- unproven `while` and `do-while` return `Unknown`;
- `if`/`else` uses worst-case branching;
- `switch` uses worst-case branching;
- unsupported invocation returns or propagates `Unknown`;
- BCL and LINQ methods are not special-cased;
- no project or binary dependency on inherited projects exists;
- Phase 1 and Phase 2 tests still pass;
- Release build, Release test, and pack remain valid.

Tests should assert `ComplexityExpression` behavior, not diagnostics, because Phase 3 does not add product diagnostics.

## Acceptance Criteria

- AC01 — extraction utiliza o Complexity Model da Phase 2.
- AC02 — nenhuma dependência dos projetos herdados é criada.
- AC03 — análise é intraprocedural.
- AC04 — parâmetros de tamanho recebem variáveis canônicas determinísticas.
- AC05 — método trivial resulta `O(1)`.
- AC06 — blocos sequenciais são compostos corretamente.
- AC07 — `foreach` sobre entrada dimensionável resulta `O(n)`.
- AC08 — `for` linear sobre `n` resulta `O(n)`.
- AC09 — `for` com constant bound permanece `O(1)`, assumindo corpo `O(1)`.
- AC10 — loops aninhados sobre mesma entrada resultam `O(n²)`.
- AC11 — loops aninhados sobre entradas diferentes resultam `O(n · m)`.
- AC12 — loop multiplicativo reconhecido resulta `O(log n)`.
- AC13 — `while` linear comprovável resulta `O(n)`.
- AC14 — `while` logarithmic comprovável resulta `O(log n)`.
- AC15 — loop não comprovável resulta `Unknown`.
- AC16 — `if`/`else` usa worst-case branching.
- AC17 — `switch` usa worst-case branching.
- AC18 — invocation não suportada não é classificada arbitrariamente como `O(1)`.
- AC19 — BCL/LINQ mappings não são implementados nesta fase.
- AC20 — call graph não é implementado.
- AC21 — recursion não é implementada.
- AC22 — análise respeita `CancellationToken` quando aplicável.
- AC23 — análise não realiza I/O/network/process execution.
- AC24 — testes de Phase 1 e Phase 2 continuam passando.
- AC25 — `BIG9000` permanece inalterado.
- AC26 — build Release passa.
- AC27 — test Release passa.
- AC28 — pack continua válido.
- AC29 — nenhum projeto herdado é modificado.

## Definition of Done

Phase 3 is done when deterministic intraprocedural Roslyn extraction is implemented under the isolated analyzer project, all acceptance criteria pass, Phase 1 and Phase 2 behavior remains unchanged, the analyzer package remains valid, inherited projects remain reference-only, and the handoff identifies the next phase or step with minimal semantic context.

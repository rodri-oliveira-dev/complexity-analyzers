# Phase 5 — Interprocedural Analysis

## Status

Complete.

## Objective

Add bounded, demand-driven propagation of complexity from source methods available in the current Roslyn `Compilation`.

Until Phase 4, invocations can contribute known complexity only when they resolve to supported BCL or LINQ operations. Phase 5 adds a second resolution path for safe C# source methods in the same compilation:

```csharp
void Process(int[] items)
{
    Validate(items);
}

void Validate(int[] values)
{
    foreach (var value in values)
    {
    }
}
```

`Process` should be able to report `O(n)` without duplicating the implementation of `Validate` in the caller.

Phase 5 must not implement recursion solving, Master Theorem, Akra-Bazzi, recurrence extraction, or diagnostic changes beyond the behavior specified here.

## Context

Phase 1 established the isolated analyzer package and `BIG9000`.

Phase 2 introduced the immutable Roslyn-free `ComplexityExpression` model.

Phase 3 introduced deterministic intraprocedural extraction for a single method.

Phase 4 introduced semantically resolved BCL/LINQ known operations and diagnostics:

- `BIG0001`
- `BIG1001`
- `BIG1002`
- `BIG1003`
- `BIG9000`

The current analyzer is concurrent, cancellation-aware, and intraprocedural. Known operations are resolved through Roslyn symbols and `KnownOperationRegistry.Default`. Unsupported or unresolved invocations remain `Unknown`.

Phase 5 extends this pipeline after known-operation resolution, while preserving all Phase 4 behavior and severity policy.

## Architecture

Target flow:

```text
Root method
    |
    v
MethodComplexityExtractor
    |
    +-- BCL/LINQ known operation
    |       |
    |       v
    |   Phase 4
    |
    +-- source method invocation
            |
            v
      SourceMethodResolver
            |
            v
   InterproceduralAnalyzer
            |
       cache lookup
            |
       +----+----+
       |         |
      hit       miss
       |         |
       |    analyze callee
       |         |
       +----+----+
            |
            v
 Complexity template
            |
     argument substitution
            |
            v
 Caller complexity
```

The architecture is demand-driven. The analyzer must not build a complete call graph for the compilation and must not scan every syntax tree at startup just to discover possible calls.

Expected component responsibilities:

- `SourceMethodResolver`: resolves invocation targets to analyzable source declarations.
- `InterproceduralAnalyzer`: coordinates bounded callee analysis, cycle detection, cache use, argument binding, and substitution.
- `MethodComplexityTemplate`: represents a callee result relative to the callee's own parameters.
- `ArgumentBinding`: captures proven caller-to-callee dimension relationships.
- `ComplexitySubstitution`: performs minimal replacement of `ComplexityVariable` values in the closed Phase 2 model.
- `InterproceduralAnalysisState`: stores per-compilation cache and budget state in a concurrency-safe form.

Names may change during implementation, but these responsibilities must remain separated enough to keep cache, resolution, and extraction behavior testable.

## Source Method Resolution

Source method resolution must be semantic.

The resolver must use `IMethodSymbol` obtained from Roslyn semantic APIs, such as `SemanticModel.GetSymbolInfo(invocation, cancellationToken)`. It must not resolve callees by text-only matching, method name, arity alone, or containing type name alone.

A call is a candidate for interprocedural analysis only when all of the following are true:

- the target invocation resolves to a single `IMethodSymbol`;
- the resolved target has source syntax available in the current `Compilation`;
- the source syntax is one of the supported method-like constructs for this phase;
- runtime dispatch can be determined safely;
- the call is not more appropriately handled by the Phase 4 known-operation resolver.

Use `IMethodSymbol.OriginalDefinition` or an equivalent Roslyn-stable identity for generic method templates. Use `DeclaringSyntaxReferences` or the Roslyn equivalent to locate source syntax. Metadata-only methods, external assembly methods, and methods whose declarations are unavailable remain `Unknown`.

If `SymbolInfo` is ambiguous, unresolved, dynamic, candidate-only, or requires speculative resolution, the source method path must return `Unknown`.

## Safe Dispatch

The symbol returned by `SemanticModel.GetSymbolInfo` is not always a safe runtime target. Phase 5 must be conservative.

Supported initial dispatch:

- `static` methods;
- `private` methods;
- non-virtual ordinary methods;
- sealed dispatch when it is proven by the target method/type symbol, for example a sealed containing type or a sealed override where the runtime target cannot vary.

Unsafe or unsupported initial dispatch:

- `virtual` dispatch when the runtime override may differ;
- `abstract` methods;
- interface dispatch;
- dynamic dispatch;
- delegate invocation;
- reflection-mediated calls;
- calls where the receiver type cannot be proven precise enough.

When runtime dispatch could point to an unknown implementation, return `Unknown`. Do not perform full hierarchy analysis, points-to analysis, or whole-solution override discovery in Phase 5.

## Analysis Scope

Initial support is limited to ordinary C# source methods inside the same `Compilation`.

Prioritize:

- ordinary `MethodDeclarationSyntax`;
- static methods;
- private methods;
- non-virtual methods;
- proven sealed dispatch.

Out of initial scope unless the current code already has a safe local mechanism:

- constructors;
- property getters and setters;
- operators;
- conversion operators;
- local functions;
- lambdas as independently callable targets;
- anonymous functions invoked through delegates;
- methods available only through metadata or external assemblies.

BCL and LINQ operations continue to be resolved by Phase 4. A BCL/LINQ known operation must not be routed through source analysis even if a source-like declaration is visible in test scaffolding.

## Per-Compilation State

Interprocedural state belongs to the current `Compilation`.

The analyzer must not use a global shared cache across compilations. State should be created from the analyzer's compilation callback and passed into syntax-node analysis for methods in that compilation.

Per-compilation state may contain:

- a concurrency-safe cache of method complexity templates;
- in-progress markers for cycle detection;
- bounded counters for a root method analysis;
- immutable or concurrency-safe references to known-operation infrastructure.

State must not contain:

- file-system data;
- workspace or solution data;
- external process data;
- static mutable dictionaries;
- cross-compilation cache entries.

## Method Complexity Templates

A cached callee result is a template relative to the callee's own parameters, not the caller's arguments.

Example:

```csharp
void Search(List<int> values)
{
    values.Contains(42);
}
```

The cached template is `O(n)` where `n` means the size of `values` in `Search`.

At call sites:

```text
Search(primary)   => O(n) using the caller dimension for primary
Search(secondary) => O(m) using the caller dimension for secondary
```

The cache must never store a caller-bound expression as the canonical callee result. This prevents the first caller analyzed from poisoning later callers that pass a different input.

Templates may include:

- the callee `ComplexityExpression`;
- the callee parameter-to-variable map;
- metadata indicating whether the template is known, unknown, cycle-boundary, or budget-boundary;
- optional semantic notes needed by Phase 6, without solving recursion.

## Argument Binding

Argument binding maps callee parameters to caller dimensions only when the relationship is proven by local syntax and semantic information.

Initial supported relations:

- Direct pass-through: `Helper(items)`.
- Property-size pass-through for integral size parameters: `Helper(items.Length)` or equivalent safe collection/string size access already recognized by earlier phases.
- Constant value: `Helper(10)`.
- Simple size-preserving transformations when mathematically safe for non-recursive Big-O: `Helper(n / 2)`, `Helper(n - 1)`, and comparable constant additive or multiplicative forms.

Binding must preserve secondary inputs:

```csharp
void Caller(int[] primary, int[] secondary)
{
    Helper(secondary);
}
```

If `Helper` is linear in its parameter, the caller contribution is `O(m)`, not `O(n)`.

Phase 5 must not introduce general symbolic algebra. If a relation cannot be proven, the bound parameter substitution is `Unknown`.

## Complexity Substitution

Phase 2 did not require a general substitution API. Phase 5 may introduce a minimal, controlled substitution mechanism for `ComplexityVariable` within the closed `ComplexityExpression` model.

The mechanism must support:

- `O(1)`;
- `O(log n)`;
- `O(n)`;
- `O(n log n)`;
- `O(n^2)`;
- `O(b^n)`;
- `O(n!)`;
- supported composites;
- `Unknown`.

Substitution rules:

- replacing a variable with a proven caller variable rewrites only that variable;
- replacing a variable with a proven constant can reduce size-dependent terms to `O(1)` when mathematically valid;
- unknown bindings produce `Unknown` for the affected template;
- composites are substituted recursively only through the known closed model shapes;
- unsupported model shapes, if introduced later, must fail conservatively to `Unknown` until handled explicitly.

Do not add a broad visitor framework unless the model expands enough to justify it. Pattern matching over the existing closed model is preferred.

## Demand-Driven Traversal

Analyze a callee only when:

- the invocation is actually visited during analysis of the caller;
- no Phase 4 `KnownOperationMapping` is more appropriate;
- no valid template exists in the per-compilation cache;
- the source target is dispatch-safe;
- the root analysis budget allows expansion.

Do not pre-analyze all methods. Do not construct a whole-compilation call graph. Do not walk every syntax tree as analyzer startup work.

For a method chain `A -> B -> C`, `C` may be analyzed only because analysis of `A` reached `B`, and analysis of `B` reached `C`.

## Cache

The cache must be scoped to one `Compilation`.

The key must identify the method template semantically. Prefer `IMethodSymbol.OriginalDefinition` with `SymbolEqualityComparer.Default`, or an equivalent representation that distinguishes overloads, generic definitions, containing types, and metadata/source identity safely.

Cache entries must store caller-independent templates. They may store:

- `Known(template)`;
- `Unknown(reason)`;
- `InProgress` or equivalent cycle marker;
- budget/cancellation-neutral metadata.

The cache must support concurrent analyzer execution. Do not use a mutable `Dictionary` in shared state without synchronization. Avoid global locks. Avoid `Lazy<T>` patterns that can deadlock when a cycle is encountered.

## Analysis Budget

Phase 5 must define internal limits to protect IDE and build hosts.

Initial internal defaults:

```text
MaximumCallDepth = 5
MaximumMethodsPerRootAnalysis = 32
```

These limits are internal implementation details in Phase 5. Do not add public configuration or custom `.editorconfig` keys yet.

When a limit is reached:

- stop expanding the call chain;
- return `Unknown` for that call boundary;
- do not continue silently beyond the budget;
- preserve enough reason metadata for tests and future handoff, without changing public diagnostics in this step.

All budget checks must respect `CancellationToken`.

## Cycle Handling

Phase 5 detects cycles but does not solve them.

Examples:

```text
A -> A
A -> B -> A
```

When a cycle is detected:

- stop expansion at the cycle boundary;
- return `Unknown` or an equivalent inconclusive boundary result according to the implementation contract;
- avoid stack overflow;
- avoid cache deadlock;
- avoid leaving permanent poisoned cache entries that make unrelated analyses incorrect;
- retain enough internal information for Phase 6 to recognize direct or mutual recursion later if the architecture keeps such metadata.

Do not calculate recurrences. Do not apply Master Theorem. Do not apply Akra-Bazzi. Do not require Tarjan/SCC as a production mechanism in Phase 5.

## Unknown Policy

The following outcomes remain `Unknown`:

- unresolved invocation;
- unsafe dispatch;
- source unavailable;
- unsupported method-like construct;
- argument relation unprovable;
- budget exceeded;
- cycle detected and not solvable in this phase;
- ambiguous symbol resolution;
- cancellation before completion.

Never convert:

```text
Unknown => O(n)
Unknown => O(1)
```

Known BCL/LINQ behavior from Phase 4 keeps its existing mapping policy. Unsupported project-local calls remain `Unknown`.

## Diagnostics Integration

Phase 5 improves the estimate used by `BIG0001`.

When a source callee is analyzed successfully, the caller's method complexity includes the substituted callee cost.

Example:

```text
Process()
  -> Validate O(n)

BIG0001 in Process:
O(n)
```

`BIG0001` remains:

- `Info`;
- disabled by default;
- emitted only when the method estimate is known.

Phase 5 may specify a future actionable diagnostic:

```text
BIG1004 — Input-dependent call inside iteration
```

Purpose: detect a source method call with input-dependent known complexity executed repeatedly inside an outer loop.

Example:

```csharp
foreach (var customer in customers)
{
    CheckAgainstBlacklist(customer, blocked);
}
```

If `CheckAgainstBlacklist(..., blocked)` is `O(m)`, the combined pattern is `O(n * m)`.

`BIG1004`, if implemented under this spec, must:

- be `Info` initially;
- follow the existing Phase 4 severity policy;
- report only when the source call and containing iteration are both known;
- avoid reporting when the callee is `Unknown`, dispatch is unsafe, or binding is unproven.

This specification step does not create the diagnostic descriptor or modify `AnalyzerReleases.Unshipped.md`.

## Performance Constraints

Analyzer performance remains a functional requirement.

Phase 5 must:

- perform source traversal only on demanded callees;
- avoid whole-compilation call graph construction;
- avoid whole-solution analysis;
- avoid `Microsoft.CodeAnalysis.Workspaces`;
- avoid I/O, network access, process execution, and reflection-heavy behavior in analyzer hot paths;
- respect `CancellationToken` before semantic lookups, syntax retrieval, cache waits, traversal, and substitution;
- bound call depth and methods analyzed per root;
- avoid unbounded recursion in syntax traversal or interprocedural expansion;
- keep cache keys and comparisons deterministic;
- avoid retaining compilation state after the compilation analysis is complete.

## Concurrency

The analyzer uses `EnableConcurrentExecution()`. Any shared state inside one compilation must be concurrency-safe.

Requirements:

- use immutable data or synchronization designed for concurrent reads/writes;
- do not use unsynchronized mutable dictionaries for shared per-compilation state;
- do not use static mutable state for per-compilation analysis;
- avoid global locks;
- avoid cache initialization patterns that can deadlock on direct or mutual cycles;
- ensure in-progress markers are removed or finalized reliably;
- make repeated analyses deterministic even when method callbacks run in parallel.

## Out of Scope

Explicitly outside Phase 5:

- recurrence extraction;
- recursive complexity solving;
- mutual recursion solving;
- Tarjan/SCC as a production requirement;
- Master Theorem;
- Akra-Bazzi;
- MathNet;
- SymPy;
- whole-solution call graph;
- whole-compilation call graph built eagerly;
- cross-project source analysis;
- external assembly decompilation;
- complete dynamic dispatch resolution;
- points-to analysis;
- complete virtual hierarchy analysis;
- reflection;
- runtime profiling;
- `CodeFixProvider`;
- public analyzer configuration for interprocedural budgets;
- modifying inherited projects;
- creating a `ProjectReference` to upstream code;
- introducing `Microsoft.CodeAnalysis.Workspaces`.

## Testing Strategy

Tests must cover observable behavior through small compilations and direct component tests where appropriate.

Required categories:

- source call resolution uses `IMethodSymbol`;
- same-name source methods and overloads are distinguished semantically;
- BCL/LINQ mappings keep precedence over source-method analysis;
- safe static/private/nonvirtual methods are analyzed;
- unsupported external methods remain `Unknown`;
- unsafe virtual/interface/dynamic dispatch remains `Unknown`;
- direct argument binding substitutes the correct variable;
- secondary input binding preserves `m` rather than incorrectly reusing `n`;
- constant arguments reduce size-dependent callee templates to `O(1)` when valid;
- simple `n / 2` and `n - 1` relations preserve Big-O for non-recursive calls;
- `A -> B -> C` propagates callee complexity;
- a source linear call inside a loop composes multiplicatively;
- cache reuse is observable without caller-bound poisoning;
- maximum call depth is enforced;
- maximum methods per root is enforced;
- direct recursion and mutual recursion do not overflow or deadlock;
- cycles remain `Unknown` in Phase 5;
- `BIG0001` reports interprocedural known complexity when enabled;
- existing Phase 1-4 diagnostics do not regress;
- cancellation is respected;
- concurrent analyzer execution remains deterministic;
- Release build, Release tests, and pack validation pass;
- no inherited project is modified or referenced.

## Documentation Requirements

When implementation lands, update English and pt-BR documentation.

Documentation must describe:

- source-method interprocedural analysis scope;
- demand-driven traversal;
- safe dispatch policy;
- supported argument binding forms;
- cache and budget behavior at a user-facing level;
- conservative `Unknown` outcomes;
- `BIG0001` interprocedural estimates;
- `BIG1004`, if implemented;
- explicit exclusions for recursion solving, Master Theorem, Akra-Bazzi, Workspaces, and inherited dependencies.

Documentation must not claim recursion solving or full call graph analysis exists in Phase 5.

## Acceptance Criteria

- AC01 — source calls are resolved semantically.
- AC02 — resolution does not use only textual names.
- AC03 — BCL/LINQ Phase 4 continues with appropriate precedence.
- AC04 — safe source method can be analyzed interprocedurally.
- AC05 — external unresolved method remains `Unknown`.
- AC06 — unsafe virtual/interface dispatch remains `Unknown`.
- AC07 — analysis is demand-driven.
- AC08 — there is no mandatory scan of the full `Compilation` to build a call graph.
- AC09 — cache belongs to the `Compilation`.
- AC10 — cache does not bind results to the wrong caller.
- AC11 — direct argument binding works.
- AC12 — secondary input binding preserves the correct variable.
- AC13 — constant argument can reduce size-dependent cost to `O(1)` when mathematically valid.
- AC14 — variable substitution is deterministic.
- AC15 — chain `A -> B -> C` propagates complexity.
- AC16 — linear call inside loop composes correctly.
- AC17 — same callee reused uses cache.
- AC18 — `MaximumCallDepth` is respected.
- AC19 — `MaximumMethodsPerRootAnalysis` is respected.
- AC20 — direct recursion does not cause stack overflow.
- AC21 — mutual cycle does not cause stack overflow or deadlock.
- AC22 — cycles are not solved in this phase.
- AC23 — `BIG0001` uses interprocedural result when known.
- AC24 — `BIG1004`, if approved by this spec, identifies input-dependent source calls inside iteration.
- AC25 — existing diagnostics from Phases 1-4 do not regress.
- AC26 — `Unknown` remains conservative.
- AC27 — analysis respects `CancellationToken`.
- AC28 — shared state is concurrency-safe.
- AC29 — no `ProjectReference` to upstream is created.
- AC30 — Workspaces is not introduced.
- AC31 — Release build passes.
- AC32 — Release tests pass.
- AC33 — package remains valid.
- AC34 — English and pt-BR documentation is updated.
- AC35 — no inherited project is modified.

## Definition of Done

Phase 5 is done when bounded demand-driven source-method complexity propagation is implemented inside `analyzer/`, semantically resolves safe source calls, preserves Phase 4 BCL/LINQ precedence, substitutes callee templates into caller dimensions without cache poisoning, detects but does not solve cycles, respects budgets and cancellation, remains concurrency-safe, updates documentation in English and pt-BR, passes Release build/test/pack, and leaves inherited projects untouched.

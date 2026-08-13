# Phase 4 — BCL, LINQ & Actionable Diagnostics

## Status

Complete.

## Objective

Extend the isolated analyzer with a small, semantically resolved known-operation layer for high-value BCL and LINQ operations, then expose complexity information and actionable diagnostics without changing the Phase 2 complexity model unnecessarily.

The phase must preserve the core policy from Phase 3:

```text
unsupported / unresolved invocation => Unknown
```

`Unknown` must never be converted automatically into `O(1)` or `O(n)`.

## Context

Phase 1 created the isolated analyzer package and the disabled-by-default infrastructure probe `BIG9000`.

Phase 2 created the immutable, Roslyn-free `ComplexityExpression` model.

Phase 3 added deterministic intraprocedural Roslyn extraction for methods, local input-size variables, safe basic operations, linear/logarithmic loops, branching, cancellation, and conservative `Unknown` results for unsupported constructs.

Phase 4 is the first phase allowed to classify selected method invocations. It must not introduce call graph traversal, recursion handling, interprocedural analysis, or a dependency on inherited projects.

## Architecture

Conceptual flow:

```text
C# invocation
      |
      v
SemanticModel / IMethodSymbol
      |
      v
KnownOperationResolver
      |
      v
KnownOperationMapping
      |-- complexity
      |-- execution semantics
      |-- provenance
      `-- metadata
      |
      v
MethodComplexityExtractor
      |
      v
ComplexityExpression
      |
      v
Diagnostic layer
```

Recommended structure:

```text
Analysis/
|-- KnownOperations/
|   |-- KnownOperationMapping.cs
|   |-- KnownOperationRegistry.cs
|   |-- KnownOperationResolver.cs
|   |-- BclMappings.cs
|   `-- LinqMappings.cs
```

The registry must be isolated inside `ComplexityAnalysis.Analyzers`; it must not reference or wrap `src/ComplexityAnalysis.Roslyn/BCL/BCLComplexityMappings.cs`.

Do not recreate a monolithic inherited-style mapping file. Prefer smaller grouped mapping sources and tests that document why each operation is present.

## Known Operation Model

Each known operation mapping must carry at least:

- Semantic identity: containing type, method name, arity/parameter shape when needed, and whether the operation is an instance, static, or reduced extension method.
- `ComplexityExpression Complexity`: expressed using the existing Phase 2 model.
- `ExecutionKind`: `Immediate` or `Deferred`.
- `Provenance`: `OfficialDocumentation`, `RuntimeSource`, or `Conservative`.
- Human-readable provenance note/source identifier suitable for test names or documentation.
- Metadata needed by diagnostics, such as operation family, whether it enumerates the receiver, whether it materializes, whether it orders, and whether it is a lookup operation.

Optional metadata:

- `ComplexityCase`: `WorstCase`, `Average`, or `Amortized`.

`ComplexityCase` is metadata only unless a later phase explicitly expands the model. Do not add average-case or amortized complexity expression types during Phase 4 if the existing model can represent the Big-O class and the case distinction can remain metadata.

## Mapping Provenance

Every mapping introduced in Phase 4 must have a verifiable justification.

Priority order:

1. Official Microsoft/.NET documentation.
2. `dotnet/runtime` source.
3. Explicit conservative estimate, documented as `Conservative`.

Do not copy assertions from the inherited implementation without verifying the source when a mapping is actually ported.

Tests should make provenance observable enough to prevent undocumented mappings from entering the registry.

## BCL Scope

Phase 4 must begin with a small high-value subset rather than full BCL parity.

Initial BCL candidates:

- `List<T>.Contains`
- `List<T>.IndexOf`
- `List<T>.Sort`
- `Dictionary<TKey,TValue>.ContainsKey`
- `Dictionary<TKey,TValue>.ContainsValue`
- `HashSet<T>.Contains`
- array `Length` and array indexing, only where still needed by invocation/pipeline substitution
- `string.Length`
- `string.Contains` and `string.IndexOf`, only for overloads whose semantics can be expressed correctly by the current model and verified provenance

Mappings that require unsupported math, unsupported memory modeling, probabilistic assumptions, comparer-specific behavior that changes asymptotic classification, or broad overload matrices may be deferred.

`List<T>.Contains` and `HashSet<T>.Contains` must be distinguished by semantic type. A custom `Contains` method must not receive either mapping.

## LINQ Scope

Phase 4 should prioritize common `System.Linq.Enumerable` operations with clear terminal/deferred behavior.

Immediate or terminal candidates:

- `Any`
- `All`
- `Contains`
- `Count`
- `LongCount`
- `First`
- `FirstOrDefault`
- `Single`
- `SingleOrDefault`
- `ToList`
- `ToArray`
- `ToDictionary`
- `ToHashSet`
- `Sum`
- `Min`
- `Max`
- `Aggregate`

Deferred candidates:

- `Where`
- `Select`
- `SelectMany`
- `OrderBy`
- `OrderByDescending`
- `ThenBy`
- `ThenByDescending`
- `Distinct`
- `GroupBy`

The implemented subset may be smaller than the candidate list if semantic resolution, provenance, or complexity substitution is not ready. The final implemented subset must be documented and covered by tests.

## Deferred Execution

Deferred LINQ operation creation must not be charged as a full enumeration at the creation site.

Example:

```csharp
var query = items.Where(predicate);
```

This must not automatically contribute `O(n)` to the method at assignment time. Creation normally contributes only known setup cost, typically `O(1)`, and must retain enough pipeline metadata for later consumption when feasible.

Enumeration cost is considered when the pipeline is consumed by a supported terminal operation or loop, such as:

- `ToList()`
- `ToArray()`
- `Count()`
- `Any()`
- `foreach`

Example:

```csharp
var list = items.Where(predicate).ToList();
```

The cost of enumerating `items` through the `Where` pipeline must be reflected.

Example:

```csharp
foreach (var item in items.Where(predicate))
{
}
```

The `foreach` must account for the effective enumeration of the deferred pipeline.

## Immediate Execution

Immediate operations contribute their invocation complexity at the invocation site when semantic resolution and input-size substitution succeed.

Terminal LINQ operations consume the receiver sequence. Their cost must include the effective cost of enumerating supported deferred pipeline stages before them.

Materializing operations include at least:

- `ToList`
- `ToArray`
- `ToDictionary`
- `ToHashSet`

Ordering operations such as `OrderBy` are deferred at creation, but the sort cost must be counted when the ordered sequence is enumerated or materialized.

## Invocation Resolution

Known operations must be resolved through Roslyn symbols, not text-only matching.

The resolver should use:

- `SemanticModel.GetSymbolInfo(invocation, cancellationToken)`
- `IMethodSymbol`
- `ContainingType`
- `OriginalDefinition`
- `ReducedFrom` for extension methods when needed
- `SymbolEqualityComparer.Default`

Avoid rules based only on names such as:

```csharp
method.Name == "Contains"
```

Custom methods with the same name as BCL/LINQ methods must remain unmapped unless their resolved symbol is a supported known operation.

## Complexity Substitution

Mappings describe operation complexity in terms of the receiver and relevant arguments. Substitution binds those abstract dimensions to the current method's canonical `ComplexityVariable` values.

Examples:

- A `List<T>.Contains` call on a parameter mapped to `n` may contribute `O(n)`.
- A `HashSet<T>.Contains` call on a parameter mapped to `m` may contribute the mapped known lookup complexity for `m`, with case metadata if needed.
- A terminal LINQ call on `items.Where(...)` must account for the receiver dimension and the deferred pipeline stages that are actually enumerated.

If the receiver dimension or required argument dimension cannot be resolved safely, the operation result is `Unknown`.

No fallback may infer `O(n)` for unknown invocations.

## Public Diagnostics

Phase 4 introduces product diagnostics while keeping `BIG9000` unchanged.

`BIG0001 — Estimated algorithmic complexity`

- Purpose: expose the estimated time complexity of a method.
- Default severity: `Info`.
- Enabled by default: `false`.
- Example message: `Estimated time complexity: O(n²)`.
- Do not emit when the method result is `Unknown`, unless a later explicit spec decision documents why unknown reporting is useful.

Actionable diagnostics:

`BIG1001 — Linear lookup inside iteration`

- Problem: a linear lookup operation is executed repeatedly inside an external iteration.
- Example: `List<T>.Contains` inside `foreach`.
- The diagnostic points at the lookup operation.
- The message explains that a linear lookup occurs inside iteration and includes known estimated complexity when available.
- It may suggest considering a lookup-oriented structure such as `HashSet<T>` or `Dictionary<TKey,TValue>`, but must not claim that replacement is always correct.

`BIG1002 — Materialization inside iteration`

- Problem: materialization such as `ToList`, `ToArray`, `ToDictionary`, or `ToHashSet` executes repeatedly inside a loop.
- The diagnostic points at the materializing operation and explains the repeated materialization pattern.

`BIG1003 — Ordering inside iteration`

- Problem: ordering work such as `OrderBy`/`ThenBy` is effectively consumed inside a loop.
- Do not report `BIG1003` merely for creating a deferred `OrderBy` pipeline without consumption.
- Report only when the ordering is enumerated or materialized inside an iteration.

## Diagnostic Severity Policy

`BIG0001`:

- `Info`
- disabled by default

`BIG100x` diagnostics:

- start as `Info` unless implementation evidence and this spec are amended to justify a stronger default.
- enabled-by-default may be true only if false-positive risk is acceptably low and documented by tests; otherwise prefer disabled-by-default.
- never use `Error`.

Consumers can promote diagnostics through `.editorconfig`.

## False Positive Policy

Phase 4 prioritizes precision over broad coverage.

Diagnostics must be emitted only when:

- the operation is semantically resolved to a known mapping;
- the containing iteration is identified by current intraprocedural analysis;
- the estimated operation complexity is known;
- deferred operations are known to be consumed when the diagnostic depends on consumption.

When in doubt, return `Unknown` or do not report a diagnostic.

## Unknown Policy

Unknown remains an explicit outcome of analysis.

Rules:

- Unsupported invocation => `Unknown`.
- Unresolved invocation => `Unknown`.
- Known method name on unsupported/custom type => `Unknown`.
- Missing required receiver/argument dimension => `Unknown`.
- Deferred pipeline whose consumption cannot be proven => no full enumeration cost is charged at creation.
- `Unknown` must not be silently converted to `O(1)`, `O(n)`, or any known operation mapping.

This deliberately rejects the inherited implementation's conservative fallback from unknown methods to `O(n)`.

## Performance Constraints

Analyzer performance remains a functional requirement.

Phase 4 must:

- stay intraprocedural;
- avoid project-wide scans;
- avoid call graph construction;
- avoid recursion solving;
- avoid `Microsoft.CodeAnalysis.Workspaces`;
- avoid I/O, network access, process execution, and reflection-heavy behavior in analyzer hot paths;
- keep registries immutable after construction;
- use targeted semantic lookups;
- respect `CancellationToken`;
- be deterministic under concurrent analyzer execution;
- avoid unbounded traversal of deferred pipeline chains.

## Out of Scope

Explicitly outside Phase 4:

- call graph
- interprocedural analysis
- recursion
- Master Theorem
- Akra-Bazzi
- automatic CodeFix
- Workspaces
- whole-solution indexing
- global complexity threshold
- complexity budget
- custom configuration beyond default severity
- memory complexity
- parallel complexity
- probabilistic complexity
- full BCL parity
- full LINQ parity
- modifying inherited projects
- changing `BIG9000`

## Testing Strategy

Tests must cover observable analyzer behavior and known-operation infrastructure.

Required categories:

- semantic resolution for known BCL instance methods;
- semantic resolution for LINQ extension methods, including reduced extension methods;
- same-name custom methods remaining unmapped;
- unknown operations remaining `Unknown`;
- each implemented BCL mapping has provenance;
- each implemented LINQ mapping has provenance;
- substitution of receiver dimensions into `ComplexityExpression`;
- deferred pipeline creation without full enumeration cost;
- terminal LINQ consumption with enumeration cost;
- `foreach` over supported deferred pipeline consumption;
- `List<T>.Contains` contributing linear lookup complexity;
- `HashSet<T>.Contains` not being confused with `List<T>.Contains`;
- `BIG0001` descriptor metadata and disabled-by-default behavior;
- `BIG0001` reported only for known method complexity when enabled;
- `BIG1001`, `BIG1002`, and `BIG1003` diagnostics on supported actionable patterns;
- no actionable diagnostics for semantic lookalikes;
- `BIG9000` descriptor and release metadata unchanged;
- Release build, Release tests, and pack validation.

## Documentation Requirements

Update English and pt-BR documentation when diagnostics are implemented.

Documentation must describe:

- `BIG0001`
- `BIG1001`
- `BIG1002`
- `BIG1003`
- default severities and enabled-by-default behavior;
- unknown policy;
- deferred versus immediate LINQ behavior;
- the implemented BCL/LINQ subset;
- how consumers can change severity through `.editorconfig`;
- that inherited projects remain reference-only.

## Acceptance Criteria

- AC01 — known operations use semantic resolution.
- AC02 — custom methods with BCL/LINQ-like names do not receive BCL/LINQ mappings.
- AC03 — unknown operation remains `Unknown`.
- AC04 — registry is isolated from the inherited project.
- AC05 — mappings have provenance.
- AC06 — documented BCL subset is covered by tests.
- AC07 — documented LINQ subset is covered by tests.
- AC08 — deferred execution is distinguished from immediate execution.
- AC09 — `Where` without consumption is not treated as full enumeration.
- AC10 — `Where(...).ToList()` accounts for enumeration.
- AC11 — `foreach` over supported deferred pipeline accounts for enumeration.
- AC12 — `List<T>.Contains` can contribute `O(n)`.
- AC13 — `HashSet<T>.Contains` is not confused with `List<T>.Contains`.
- AC14 — `BIG0001` exists and is disabled by default.
- AC15 — `BIG0001` displays known estimated complexity.
- AC16 — `BIG1001` detects linear lookup inside iteration.
- AC17 — `BIG1002` detects materialization inside iteration.
- AC18 — `BIG1003` detects effectively consumed ordering inside iteration.
- AC19 — diagnostics are not emitted for semantically different patterns only because the method name matches.
- AC20 — `BIG9000` remains unchanged.
- AC21 — previous phases do not regress.
- AC22 — Release build passes.
- AC23 — Release tests pass.
- AC24 — pack remains valid.
- AC25 — `AnalyzerReleases.Unshipped.md` corresponds to existing diagnostics.
- AC26 — English and pt-BR documentation describe the new diagnostics.
- AC27 — no inherited project is modified.

## Definition of Done

Phase 4 is done when the isolated analyzer resolves the documented subset of BCL and LINQ operations semantically, preserves `Unknown` for unresolved/unsupported invocations, distinguishes deferred from immediate execution, reports the specified diagnostics with conservative severities, documents the implemented behavior in English and pt-BR, validates Release build/test/pack, leaves `BIG9000` unchanged, and modifies no inherited projects.

# Analyzer Catalog

[English](analyzers.md) | [Português (Brasil)](../pt-BR/analyzers.md)

This page documents the public diagnostics exposed by `ComplexityAnalysis.Analyzers`
and the evidence required before each rule can report.

The analyzer is intentionally conservative. It reports only facts and known
estimates supported by the current syntax, Roslyn semantic model, known-operation
registry, bounded source-method analysis, and supported direct-recursion solver.
Unsupported, unsafe, cyclic, budget-limited, numerically inconclusive, or
unresolved behavior remains `Unknown` rather than being guessed.

## Summary

| ID | Title | Category | Default severity | Enabled by default |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | `false` |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | `true` |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | `true` |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | `true` |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | `true` |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | `true` |
| `BIG1006` | Method complexity exceeds configured threshold | `Complexity` | `Info` | `true` |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | `false` |

## Explainability Convention

Actionable diagnostics follow this convention when evidence is available:

| Dimension | Meaning |
| --- | --- |
| WHAT | What was detected. |
| WHERE | Which operation, invocation, method, or construct caused the diagnostic. |
| WHY | Why the pattern matters. |
| COST | The known operation or callee cost used by the analyzer. |
| CONTEXT | The enclosing execution context, such as an analyzable iteration. |
| THRESHOLD | The configured maximum exceeded by a known estimate. |
| GUIDANCE | Conditional improvement direction, not an unconditional fix. |
| LIMIT | What the analyzer is not claiming. |

Diagnostic messages stay short for IDE/build output. Detailed reasoning,
examples, guidance, and limitations live in this catalog. A missing diagnostic
does not prove that code is efficient; it can also mean the analyzer could not
prove the required facts safely.

## Diagnostic Properties

Diagnostics may include stable structured properties for future tooling. These
properties are deterministic strings and should not be treated as a complete
internal trace.

| Property | Meaning |
| --- | --- |
| `complexity` | Known method estimate emitted by `BIG0001`, `BIG1005`, or `BIG1006`. |
| `threshold` | Configured threshold expression emitted by `BIG1006`. |
| `operation` | Stable operation or method display name responsible for an actionable diagnostic. |
| `operationComplexity` | Known cost of the operation or callee at the diagnostic location. |
| `iterationComplexity` | Known enclosing iteration complexity. |
| `combinedComplexity` | Known nested contribution composed for the diagnostic. |
| `recurrenceClass` | Stable recurrence outcome class, currently `exponential`. |
| `diagnosticRole` | Stable infrastructure role for `BIG9000`, currently `execution-probe`. |

## BIG0001 - Estimated Algorithmic Complexity

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `false` |
| Location | Method identifier |
| Message | `Estimated algorithmic complexity for '{method}' is {complexity}` |
| Diagnostic properties | `complexity` |

### What It Detects

`BIG0001` reports the analyzer's known estimate for a supported method, such as
`O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`, `O(n^1.585)`, or
`O(1.618^n)`.

### Why It Matters

The diagnostic exposes the method-level result that other threshold and
actionable diagnostics are based on. It is disabled by default because it can be
chatty in normal builds.

### Example That Triggers

```csharp
public void M(int[] values)
{
    foreach (var value in values)
    {
        _ = value + 1;
    }
}
```

When enabled, `M` reports `Estimated algorithmic complexity for 'M' is O(n)`.

### Example That Does Not Trigger

```csharp
public void M(Service service)
{
    service.Process();
}
```

If the call cannot be resolved as a supported known operation or safe source
method, the method estimate remains `Unknown` and `BIG0001` does not report.

### Complexity Reasoning

The estimate may include supported loop bounds, known BCL/LINQ operations, safe
source-method callees in the same compilation, and selected solved direct
recursion.

### Guidance

Use `BIG0001` when you want visibility into known estimates while tuning
thresholds or reviewing analysis behavior.

### Limitations

`Unknown` is not converted into a guessed complexity class. Unsupported,
unresolved, unsafe, budget-limited, cancelled, or incomparable paths can suppress
the diagnostic.

Enable it with:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

## BIG1001 - Linear Lookup Inside Iteration

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Lookup invocation |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### What It Detects

`BIG1001` reports a supported lookup whose known cost is non-constant and whose
invocation is inside an analyzable iteration.

### Why It Matters

Repeated linear membership checks can multiply the cost of the enclosing loop.

### Example That Triggers

```csharp
foreach (var customer in customers)
{
    if (blockedCustomers.Contains(customer))
    {
    }
}
```

When `blockedCustomers` is a supported `List<T>`, the analyzer can report that
`List<T>.Contains` has known linear cost inside the loop.

### Example That Does Not Trigger

```csharp
foreach (var customer in customers)
{
    if (blockedCustomers.Contains(customer))
    {
    }
}
```

If `blockedCustomers` is a supported `HashSet<T>`, the lookup is recorded as
average `O(1)` and this rule does not report.

### Complexity Reasoning

The analyzer uses resolved symbol identity for the lookup operation, resolves the
receiver/input dimension, proves an enclosing iteration, and composes the known
operation cost with the iteration cost. It reports `combinedComplexity` only when
that composition is known.

### Guidance

Consider using an indexed lookup or set-like structure when repeated membership
lookup is required and its semantics, ordering, duplicate behavior, mutability,
and memory trade-offs are appropriate.

### Limitations

The analyzer is not claiming that `HashSet<T>` or any alternative collection is
always semantically correct. Custom methods named `Contains` are not classified
as framework lookups by name alone.

## BIG1002 - Materialization Inside Iteration

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Materializing invocation |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### What It Detects

`BIG1002` reports supported LINQ materializers such as `ToList`, `ToArray`,
`ToDictionary`, and `ToHashSet` when they execute inside an analyzable iteration.

### Why It Matters

Materialization can enumerate the source and allocate a result on each iteration.

### Example That Triggers

```csharp
foreach (var customer in customers)
{
    var copy = items.ToList();
}
```

### Example That Does Not Trigger

```csharp
var copy = items.ToList();

foreach (var customer in customers)
{
    _ = copy.Count;
}
```

### Complexity Reasoning

The analyzer reports only when the materializer is a supported known operation,
the source size is known, the enclosing iteration is analyzable, and the nested
contribution can be composed.

### Guidance

Consider moving the materialization outside the loop when the materialized result
does not depend on the current iteration and repeated allocation is not needed.

### Limitations

The analyzer does not prove that materialization is unnecessary. Repeated
materialization may be required when the source or desired snapshot depends on
the current iteration.

## BIG1003 - Ordering Inside Iteration

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Deferred ordering invocation |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### What It Detects

`BIG1003` reports supported ordering operations such as `OrderBy`,
`OrderByDescending`, `ThenBy`, and `ThenByDescending` when the analyzer can prove
that the deferred ordering is consumed inside an analyzable iteration.

### Why It Matters

Sorting on every iteration can dominate the loop body cost.

### Example That Triggers

```csharp
foreach (var customer in customers)
{
    var sorted = items.OrderBy(item => item).ToList();
}
```

### Example That Does Not Trigger

```csharp
var query = items.OrderBy(item => item);

foreach (var customer in customers)
{
    _ = customer;
}

var sorted = query.ToList();
```

### Complexity Reasoning

Deferred ordering creation alone is setup work. The diagnostic is emitted only
when a supported immediate consumer, such as `ToList`, consumes the ordered
sequence inside the loop. The reported operation cost is the known consumed
ordering cost.

### Guidance

Consider sorting once outside the loop when the ordering and source do not depend
on the current iteration.

### Limitations

The analyzer does not move sorting automatically and does not claim that sorting
outside the loop preserves semantics. It reports only consumed ordering whose
cost can be proven.

## BIG1004 - Input-Dependent Method Call Inside Iteration

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Source-method invocation |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### What It Detects

`BIG1004` reports a supported source-method call with known non-constant,
input-dependent complexity when that call executes inside an analyzable
iteration.

### Why It Matters

The callee's input-dependent work can be repeated once per iteration in the
caller.

### Example That Triggers

```csharp
foreach (var customer in customers)
{
    CheckAgainstBlacklist(customer, blocked);
}

private static void CheckAgainstBlacklist(int customer, int[] blocked)
{
    foreach (var value in blocked)
    {
        _ = value + customer;
    }
}
```

### Example That Does Not Trigger

```csharp
foreach (var customer in customers)
{
    Check(customer);
}

private static int Check(int value) => value + 1;
```

Constant callees do not report.

### Complexity Reasoning

The analyzer resolves a safe source-method target, derives or reuses a bounded
callee template, substitutes call-site arguments, and composes the known callee
cost with the enclosing iteration cost. It does not expose template or cache
details.

### Guidance

Consider precomputing, caching, memoization, or a different data shape when the
callee result is semantically reusable across iterations.

### Limitations

The analyzer does not claim that repeated calls are redundant. It avoids unsafe
virtual/interface dispatch, external metadata-only methods, unresolved calls,
cycles, and budget-boundary guesses.

## BIG1005 - Exponential Recursive Growth

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Recursive method identifier |
| Message | `Recursive method '{method}' exhibits exponential growth with estimated complexity {complexity}` |
| Diagnostic properties | `complexity`, `recurrenceClass` |

### What It Detects

`BIG1005` reports a supported direct-recursive method whose extracted recurrence
solves to exponential growth.

### Why It Matters

Exponential recursive growth can become impractical even for moderate input
sizes.

### Example That Triggers

```csharp
int Fibonacci(int n)
{
    if (n <= 1)
    {
        return n;
    }

    return Fibonacci(n - 1) + Fibonacci(n - 2);
}
```

### Example That Does Not Trigger

```csharp
int CountDown(int n)
{
    if (n <= 1)
    {
        return 1;
    }

    return CountDown(n - 1) + 1;
}
```

This supported recurrence is linear, not exponential, so `BIG1005` does not
report.

### Complexity Reasoning

The analyzer must prove semantic direct recursion, compatible base-case
evidence, reducing recursive arguments, known local work, and a supported solved
recurrence. Fibonacci-like `T(n)=T(n-1)+T(n-2)+O(1)` is documented as a
representative supported shape.

### Guidance

Consider memoization or an iterative approach when repeated recursive
subproblems are semantically equivalent.

### Limitations

The diagnostic does not include the full recurrence equation because the current
diagnostic pipeline carries the solved exponential estimate, not a stable public
recurrence-text contract. Unsupported recursion remains `Unknown`.

## BIG1006 - Method Complexity Exceeds Configured Threshold

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Method identifier |
| Message | `Method '{method}' has estimated complexity {actual}, exceeding configured maximum {threshold}` |
| Diagnostic properties | `complexity`, `threshold` |

### What It Detects

`BIG1006` reports a method whose known, comparable estimated complexity is
greater than `complexity_analyzers.maximum_complexity`.

### Why It Matters

The rule lets a project enforce an explicit maximum complexity policy.

### Example That Triggers

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

```csharp
void M(int[] values)
{
    foreach (var outer in values)
    {
        foreach (var inner in values)
        {
            _ = outer + inner;
        }
    }
}
```

A known `O(n^2)` estimate exceeds `O(n log n)`.

### Example That Does Not Trigger

```csharp
void M(int[] values)
{
    foreach (var value in values)
    {
        _ = value + 1;
    }
}
```

With `maximum_complexity = n`, equality does not report. Only strictly greater
known estimates report.

### Complexity Reasoning

The analyzer reports only when the method estimate is known, the configured
threshold is concrete, the estimate and threshold are comparable, and comparison
returns greater.

### Guidance

Consider reducing the method's proven dominant work, splitting responsibilities
where that improves clarity, or adjusting the configured threshold when the
project intentionally accepts the cost.

### Limitations

`Unknown` and incomparable multivariate expressions do not produce threshold
reports. `BIG1006` is a practical static-analysis signal, not a universal
mathematical proof.

## BIG9000 - Analyzer Execution Probe

| Property | Value |
| --- | --- |
| Category | `Infrastructure` |
| Default severity | `Info` |
| Enabled by default | `false` |
| Location | Start of a source file when available; otherwise no source location |
| Message | `ComplexityAnalysis.Analyzers execution probe is active` |
| Diagnostic properties | `diagnosticRole` |

### What It Detects

`BIG9000` proves that the analyzer package loaded, initialized, and executed.

### Why It Matters

It is useful for package-consumer smoke tests and compatibility validation.

### Example That Triggers

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

### Example That Does Not Trigger

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

### Complexity Reasoning

None. This is an infrastructure probe, not a complexity analysis rule.

### Guidance

Enable it temporarily only when validating analyzer loading, then disable it
again.

### Limitations

`BIG9000` is not a performance recommendation and does not indicate that any
source code is expensive. It reports at most once per compilation when enabled.

## Supported Known-Operation Subset

The analyzer documents a deliberately bounded known-operation set.

BCL examples include selected operations from:

- `List<T>`;
- `Dictionary<TKey,TValue>`;
- `HashSet<T>`;
- arrays;
- strings.

Supported LINQ immediate/terminal operations include:

- `Any`, `All`, `Contains`, `Count`, `LongCount`;
- `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`;
- `Sum`, `Min`, `Max`, `Aggregate`.

Supported deferred operations include:

- `Where`, `Select`, `SelectMany`;
- `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`;
- `Distinct`, `GroupBy`.

Mappings are based on resolved symbols. Same-name user methods are not mapped
automatically.

Deferred pipeline creation is treated as setup work. Enumeration or sorting cost
is charged only when supported consumption is proven.

## Supported Source-Method Scope

Interprocedural analysis is limited to ordinary source methods in the same
Roslyn `Compilation` when dispatch is safe.

Supported forms include:

- static methods;
- private methods;
- ordinary non-virtual methods;
- sealed dispatch when the runtime target is proven.

Traversal is demand-driven and bounded. The default maximum call depth is `5`,
configurable up to `16`. The default maximum source-method expansions per root is
`32`, configurable up to `128`.

Outside the supported scope are unsafe virtual/interface dispatch, dynamic
dispatch, external assemblies, constructors, properties, operators, local
functions, lambdas as independent targets, whole-compilation call graphs, and
whole-solution analysis.

Cycles are detected conservatively. Direct recursion can be delegated to the
recurrence pipeline; mutual recursion remains unsupported for solving.

## Supported Direct-Recursion Scope

A recurrence can be solved only when the analyzer can prove semantic direct
recursion, compatible base-case evidence, a reducing recursive argument, and
known local work.

Supported recurrence families include:

- summation/decrement forms such as `T(n)=T(n-c)+f(n)` for supported tolls;
- a bounded simple exponential subset, including Fibonacci-like shapes;
- Master Theorem forms;
- a restricted/bounded Akra-Bazzi subset with supported scale-only recursive
  terms.

Representative results include:

```text
T(n)=T(n-1)+1               => O(n)
T(n)=T(n-1)+n               => O(n^2)
T(n)=T(n-1)+log n           => O(n log n)
2T(n-1)+1                   => O(2^n)
T(n-1)+T(n-2)+1             => O(1.618^n)
T(n/2)+1                    => O(log n)
2T(n/2)+n                   => O(n log n)
3T(n/2)+n                   => O(n^1.585)
T(n/3)+T(2n/3)+n            => O(n log n)
```

The analyzer does not implement full Akra-Bazzi, arbitrary characteristic-
polynomial solving, general symbolic recurrence parsing, general numerical
integration, or external MathNet/SymPy solver integration.

Unsupported cases remain `Unknown`.

## Configuration

Use standard Roslyn severity configuration:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Behavioral options such as analysis budgets and the maximum-complexity threshold
are documented in [Configuration](configuration.md).

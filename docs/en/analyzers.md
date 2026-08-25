# Analyzer Catalog

[English](analyzers.md) | [Português (Brasil)](../pt-BR/analyzers.md)

This page documents the public diagnostics exposed by `ComplexityAnalysis.Analyzers` and the analysis boundaries that determine when each rule can report.

The analyzer resolves supported BCL and LINQ operations by Roslyn symbol identity, can propagate complexity from safe source methods in the same compilation, and can solve selected direct-recursion recurrence shapes. Unsupported, unsafe, cyclic, budget-limited, numerically inconclusive, or unresolved behavior remains `Unknown` rather than being guessed.

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

## How to read these diagnostics

The analyzer is intentionally conservative. A rule reports only when the syntax and semantic model provide enough evidence for the relevant complexity relationship. A missing diagnostic does not prove that an operation is efficient; it can also mean that the analyzer could not prove the required facts safely.

`Unknown` is therefore a first-class result and is not converted to `O(1)` or any other known complexity class.

## BIG0001 — Estimated algorithmic complexity

`BIG0001` exposes the analyzer's known estimate for a supported method, such as `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`, `O(n^1.585)`, or `O(1.618^n)`.

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `false` |
| Location | Method identifier |
| Message | `Estimated time complexity: {complexity}` |

The estimate can include supported loop bounds, known BCL/LINQ operations, safe source-method callees, and selected solved direct recursion.

Example:

```csharp
public void M(int[] values)
{
    foreach (var value in values)
    {
        _ = value + 1;
    }
}
```

When enabled, `M` reports `Estimated time complexity: O(n)`.

No diagnostic is reported when the rule is not enabled, the method result is `Unknown`, a required operation is unsupported or unresolved, an interprocedural boundary cannot be proven safely, or direct recursion falls outside the supported recurrence model.

Enable it with:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

## BIG1001 — Linear lookup inside iteration

`BIG1001` reports a supported linear lookup that executes inside an analyzable iteration.

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Lookup invocation |

Typical example:

```csharp
foreach (var customer in customers)
{
    if (blockedCustomers.Contains(customer))
    {
    }
}
```

When `blockedCustomers` is a supported `List<T>`, the lookup is linear in that collection size and can compound the containing loop cost.

The rule does not report the same lookup outside a loop, supported constant-average lookups such as `HashSet<T>.Contains`, custom same-name methods, or cases where the loop/receiver size cannot be resolved safely.

Configure it with:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

## BIG1002 — Materialization inside iteration

`BIG1002` reports repeated supported LINQ materialization inside an analyzable iteration. Supported materializers include `ToList`, `ToArray`, `ToDictionary`, and `ToHashSet`.

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Materializing invocation |

Example:

```csharp
foreach (var customer in customers)
{
    var copy = items.ToList();
}
```

The rule does not report materialization outside a loop, custom same-name methods, unresolved source sizes, or loops whose iteration count cannot be analyzed.

Configure it with:

```ini
[*.cs]

dotnet_diagnostic.BIG1002.severity = warning
```

## BIG1003 — Ordering inside iteration

`BIG1003` reports supported deferred ordering when the analyzer can prove that the ordering is consumed inside an analyzable iteration.

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Deferred ordering invocation |

Supported ordering includes `OrderBy`, `OrderByDescending`, `ThenBy`, and `ThenByDescending`.

Example:

```csharp
foreach (var customer in customers)
{
    var sorted = items.OrderBy(item => item).ToList();
}
```

The diagnostic points to the ordering operation. Creating an ordering pipeline without consuming it inside the loop does not report because the full sorting/enumeration cost has not been proven at that point.

Configure it with:

```ini
[*.cs]

dotnet_diagnostic.BIG1003.severity = warning
```

## BIG1004 — Input-dependent method call inside iteration

`BIG1004` reports a supported source-method call with known input-dependent complexity when that call executes inside an analyzable iteration.

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Source-method invocation |

Example:

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

The analyzer can combine the caller loop with the callee's substituted input-dependent cost, producing a pattern such as `O(n * m)`.

The rule does not report source calls outside loops, callees whose substituted cost is `O(1)`, unsafe dispatch, unknown argument bindings, cycle/budget boundaries, or known framework operations already handled by the BCL/LINQ rules.

Configure it with:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

## BIG1005 — Exponential recursive growth

`BIG1005` reports a supported direct-recursive method whose recurrence is solved as exponential growth.

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Recursive method identifier |
| Message | `Recursive method '{method}' has estimated exponential time complexity {complexity}` |

Example:

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

For the supported Fibonacci-like recurrence, the analyzer reports exponential growth such as `O(1.618^n)`.

The rule does not report polynomial/logarithmic solved recursion, unsupported or invalid recurrence shapes, missing base-case evidence, non-reducing recursive arguments, numerically inconclusive results, or mutual recursion.

Configure it with:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

## BIG1006 — Method complexity exceeds configured threshold

`BIG1006` reports a method whose known, comparable estimated complexity is greater than `complexity_analyzers.maximum_complexity`.

| Property | Value |
| --- | --- |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Method identifier |
| Message | `Method '{method}' has estimated complexity {actual}, which exceeds the configured maximum {threshold}` |

The descriptor is enabled by default, but the rule is functionally opt-in because the default threshold is `none`.

Example configuration:

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

A proven `O(n^2)` method exceeds `n_log_n` and can report. `O(n log n)`, `O(n)`, `Unknown`, and incomparable multivariate expressions do not report for that threshold.

`BIG1006` is a practical static-analysis signal, not a universal mathematical proof.

## BIG9000 — Analyzer execution probe

`BIG9000` is an infrastructure diagnostic used to prove that the package loaded, initialized, and executed.

| Property | Value |
| --- | --- |
| Category | `Infrastructure` |
| Default severity | `Info` |
| Enabled by default | `false` |
| Location | Start of a source file when available; otherwise no source location |
| Message | `ComplexityAnalysis.Analyzers execution probe is active` |

Enable it temporarily:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Disable it after the smoke test:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

It reports at most once per compilation when enabled.

## Supported known-operation subset

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

Mappings are based on resolved symbols. Same-name user methods are not mapped automatically.

Deferred pipeline creation is treated as setup work. Enumeration or sorting cost is charged when supported consumption is proven.

## Supported source-method scope

Interprocedural analysis is limited to ordinary source methods in the same Roslyn `Compilation` when dispatch is safe.

Supported forms include:

- static methods;
- private methods;
- ordinary non-virtual methods;
- sealed dispatch when the runtime target is proven.

Traversal is demand-driven and bounded. The default maximum call depth is `5`, configurable up to `16`. The default maximum source-method expansions per root is `32`, configurable up to `128`.

Outside the supported scope are unsafe virtual/interface dispatch, dynamic dispatch, external assemblies, constructors, properties, operators, local functions, lambdas as independent targets, whole-compilation call graphs, and whole-solution analysis.

Cycles are detected conservatively. Direct recursion can be delegated to the recurrence pipeline; mutual recursion remains unsupported for solving.

## Supported direct-recursion scope

A recurrence can be solved only when the analyzer can prove semantic direct recursion, compatible base-case evidence, a reducing recursive argument, and known local work.

Supported recurrence families include:

- summation/decrement forms such as `T(n)=T(n-c)+f(n)` for supported tolls;
- a bounded simple exponential subset, including Fibonacci-like shapes;
- Master Theorem forms;
- a restricted/bounded Akra-Bazzi subset with supported scale-only recursive terms.

Representative results include:

```text
T(n)=T(n-1)+1               => O(n)
T(n)=T(n-1)+n               => O(n^2)
T(n)=T(n-1)+log n           => O(n log n)
2T(n-1)+1                   => O(2^n)
T(n/2)+1                    => O(log n)
2T(n/2)+n                   => O(n log n)
3T(n/2)+n                   => O(n^1.585)
T(n/3)+T(2n/3)+n            => O(n log n)
```

The analyzer does not implement full Akra-Bazzi, arbitrary characteristic-polynomial solving, general symbolic recurrence parsing, general numerical integration, or external MathNet/SymPy solver integration.

Unsupported cases remain `Unknown`.

## Configuration

Use standard Roslyn severity configuration:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Behavioral options such as analysis budgets and the maximum-complexity threshold are documented in [Configuration](configuration.md).

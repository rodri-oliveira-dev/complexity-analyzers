# Analyzer Catalog

[English](analyzers.md) | [Portugues (Brasil)](../pt-BR/analyzers.md)

This page is the public catalog of diagnostics exposed by `ComplexityAnalysis.Analyzers` in Phase 6.

The analyzer resolves known BCL and LINQ operations through Roslyn symbols, can propagate complexity from safe source methods in the same compilation, and can solve selected direct recursive recurrence shapes. Same-name custom methods are not treated as known BCL/LINQ operations. Unsupported, unsafe, cyclic, budget-limited, numerically inconclusive, or unresolved operations remain `Unknown`.

## Summary

| ID | Title | Category | Default severity | Enabled by default |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | `false` |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | `true` |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | `true` |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | `true` |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | `true` |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | `true` |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | `false` |

## BIG0001 - Estimated Algorithmic Complexity

| Property | Value |
| --- | --- |
| ID | `BIG0001` |
| Title | `Estimated algorithmic complexity` |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `false` |
| Location | Method identifier |
| Message | `Estimated time complexity: {complexity}` |

### Problem Detected

`BIG0001` is informational. It exposes the analyzer's known estimate for a supported method, such as `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`, `O(n^1.585)`, or `O(1.618^n)`. In Phase 6, that estimate can include complexity propagated from safe source-method callees and selected solved direct recursion.

### Example

```csharp
public sealed class Sample
{
    public void M(int[] values)
    {
        foreach (var value in values)
        {
            var x = value + 1;
        }
    }
}
```

When enabled, the diagnostic is reported on `M` with `Estimated time complexity: O(n)`.

Interprocedural source-call example:

```csharp
public sealed class Sample
{
    public void M(int[] values)
    {
        Helper(values);
    }

    private void Helper(int[] items)
    {
        foreach (var item in items)
        {
            var x = item + 1;
        }
    }
}
```

When `BIG0001` is enabled, `M` reports `Estimated time complexity: O(n)`.

Direct-recursion example:

```csharp
public sealed class Sample
{
    public int BinarySearch(int n, bool left)
    {
        if (n <= 1)
        {
            return -1;
        }

        if (left)
        {
            return BinarySearch(n / 2, false);
        }

        return BinarySearch(n / 2, false);
    }
}
```

When `BIG0001` is enabled, `BinarySearch` reports `Estimated time complexity: O(log n)`. The two syntactic recursive calls are in exclusive branches and are not counted as multiplicity two.

### Non-Trigger Cases

No diagnostic is reported when:

- `BIG0001` is not enabled by consumer configuration;
- the method result is `Unknown`;
- the method depends on unsupported, unsafe, cyclic, budget-limited, numerically inconclusive, or unresolved operations;
- direct recursion lacks base-case evidence, has non-reducing arguments, has unknown local work, or exceeds the supported recurrence families;
- recursion is mutual rather than direct.

### Configuration

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Use `none` to keep it disabled:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = none
```

## BIG1001 - Linear Lookup Inside Iteration

| Property | Value |
| --- | --- |
| ID | `BIG1001` |
| Title | `Linear lookup inside iteration` |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Lookup invocation |
| Message | Linear lookup operation, containing iteration estimate, and combined estimate |

### Problem Detected

`BIG1001` reports a semantically known linear lookup that executes inside an analyzable loop. The main Phase 4 example is `List<T>.Contains` inside a loop over another input.

### Example

```csharp
using System.Collections.Generic;

public sealed class Sample
{
    void M(List<int> customers, List<int> blockedCustomers)
    {
        foreach (var customer in customers)
        {
            if (blockedCustomers.Contains(customer))
            {
            }
        }
    }
}
```

The diagnostic points at `blockedCustomers.Contains(customer)`.

### Non-Trigger Cases

No diagnostic is reported for:

- the same lookup outside a loop;
- `HashSet<T>.Contains`, because the supported mapping is average-case constant lookup;
- custom `Contains` methods with the same name;
- loops whose iteration count cannot be analyzed;
- lookups whose receiver size cannot be resolved safely.

### Configuration

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

Disable it:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = none
```

## BIG1002 - Materialization Inside Iteration

| Property | Value |
| --- | --- |
| ID | `BIG1002` |
| Title | `Materialization inside iteration` |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Materializing invocation |
| Message | Materialization operation, containing iteration estimate, and combined estimate |

### Problem Detected

`BIG1002` reports repeated supported LINQ materialization inside an analyzable loop. Supported materializers include `ToList`, `ToArray`, `ToDictionary`, and `ToHashSet`.

### Example

```csharp
using System.Collections.Generic;
using System.Linq;

public sealed class Sample
{
    void M(List<int> customers, IEnumerable<int> items)
    {
        foreach (var customer in customers)
        {
            var copy = items.ToList();
        }
    }
}
```

The diagnostic points at `items.ToList()`.

### Non-Trigger Cases

No diagnostic is reported for:

- materialization outside a loop;
- custom `ToList` or `ToArray` methods;
- loops whose iteration count cannot be analyzed;
- materializers whose source size cannot be resolved safely.

### Configuration

```ini
[*.cs]

dotnet_diagnostic.BIG1002.severity = warning
```

Disable it:

```ini
[*.cs]

dotnet_diagnostic.BIG1002.severity = none
```

## BIG1003 - Ordering Inside Iteration

| Property | Value |
| --- | --- |
| ID | `BIG1003` |
| Title | `Ordering inside iteration` |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Deferred ordering invocation |
| Message | Ordering operation, containing iteration estimate, and combined estimate |

### Problem Detected

`BIG1003` reports supported deferred ordering work when it is proven to be consumed inside an analyzable loop. Supported ordering operations include `OrderBy`, `OrderByDescending`, `ThenBy`, and `ThenByDescending`.

### Example

```csharp
using System.Collections.Generic;
using System.Linq;

public sealed class Sample
{
    void M(List<int> customers, IEnumerable<int> items)
    {
        foreach (var customer in customers)
        {
            var sorted = items.OrderBy(item => item).ToList();
        }
    }
}
```

The diagnostic points at `items.OrderBy(item => item)`, not at `ToList()`.

### Non-Trigger Cases

No diagnostic is reported for:

- creating an `OrderBy` pipeline inside a loop without consuming it;
- consuming the ordered pipeline outside the loop;
- custom `OrderBy` methods;
- loops whose iteration count cannot be analyzed;
- ordering chains whose source size cannot be resolved safely.

### Configuration

```ini
[*.cs]

dotnet_diagnostic.BIG1003.severity = warning
```

Disable it:

```ini
[*.cs]

dotnet_diagnostic.BIG1003.severity = none
```

## BIG1004 - Input-Dependent Method Call Inside Iteration

| Property | Value |
| --- | --- |
| ID | `BIG1004` |
| Title | `Input-dependent method call inside iteration` |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Source-method invocation |
| Message | Source method, callee estimate, containing iteration estimate, and combined estimate |

### Problem Detected

`BIG1004` reports a supported source-method call with known input-dependent complexity when that call executes inside an analyzable loop.

### Example

```csharp
public sealed class Sample
{
    void M(int[] customers, int[] blocked)
    {
        foreach (var customer in customers)
        {
            CheckAgainstBlacklist(customer, blocked);
        }
    }

    private void CheckAgainstBlacklist(int customer, int[] blocked)
    {
        foreach (var value in blocked)
        {
            var x = value + customer;
        }
    }
}
```

The diagnostic points at `CheckAgainstBlacklist(customer, blocked)` and reports the combined `O(n * m)` pattern.

### Non-Trigger Cases

No diagnostic is reported for:

- source calls outside loops;
- source calls whose substituted complexity is `O(1)`;
- unsafe virtual or interface dispatch;
- cyclic or budget-limited callees;
- unknown argument bindings;
- known BCL/LINQ operations already handled by `BIG1001`, `BIG1002`, or `BIG1003`.

### Configuration

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

Disable it:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = none
```

## BIG1005 - Exponential Recursive Growth

| Property | Value |
| --- | --- |
| ID | `BIG1005` |
| Title | `Exponential recursive growth` |
| Category | `Complexity` |
| Default severity | `Info` |
| Enabled by default | `true` |
| Location | Recursive method identifier |
| Message | `Recursive method '{method}' has estimated exponential time complexity {complexity}` |

### Problem Detected

`BIG1005` reports a supported direct recursive method whose solved recurrence is exponential. It is intentionally informational and does not prescribe memoization or a rewrite.

### Example

```csharp
public sealed class Sample
{
    int Fibonacci(int n)
    {
        if (n <= 1)
        {
            return n;
        }

        return Fibonacci(n - 1) + Fibonacci(n - 2);
    }
}
```

The diagnostic points at `Fibonacci` and reports `O(1.618^n)`.

### Non-Trigger Cases

No diagnostic is reported for:

- polynomial or logarithmic solved recursion;
- unknown, unsupported, invalid, or numerically inconclusive recurrence results;
- missing base-case evidence;
- non-reducing recursive arguments such as `n` or `n + 1`;
- mutual recursion, which is detected but not solved.

### Configuration

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

Disable it:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = none
```

## BIG9000 - Analyzer Execution Probe

| Property | Value |
| --- | --- |
| ID | `BIG9000` |
| Title | `Analyzer execution probe` |
| Category | `Infrastructure` |
| Default severity | `Info` |
| Enabled by default | `false` |
| Location | Start of a source file when one is available; otherwise no source location |
| Message | `ComplexityAnalysis.Analyzers execution probe is active` |

### Problem Detected

`BIG9000` does not detect a code problem. It is an infrastructure probe used to prove that the analyzer package was loaded, initialized, and able to report diagnostics.

### Example

Any C# source can produce the probe when explicitly enabled:

```csharp
public sealed class Sample
{
    public int M() => 42;
}
```

### Non-Trigger Cases

No diagnostic is reported when `BIG9000` is not enabled. It reports at most once per compilation when enabled.

### Configuration

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Disable it:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## Supported Known-Operation Subset

The analyzer includes a small documented subset:

- BCL: selected `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>`, array, and string operations.
- LINQ immediate or terminal operations: `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max`, and `Aggregate`.
- LINQ deferred operations: `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct`, and `GroupBy`.

Deferred LINQ pipeline creation is treated as setup work. Enumeration cost is counted only when a supported terminal operation or `foreach` consumes the pipeline.

## Supported Source-Method Scope

Source-method interprocedural analysis is limited to ordinary methods in the same Roslyn `Compilation` with safe dispatch:

- static methods;
- private methods;
- non-virtual ordinary methods;
- sealed dispatch when the runtime target is proven.

The traversal is demand-driven: a source callee is analyzed only when an analyzed caller reaches that invocation. The analyzer does not build a mandatory whole-compilation call graph and does not pre-analyze every syntax tree for interprocedural propagation.

Expansion is bounded by internal budgets: maximum call depth is `5`, and maximum uncached source-method expansions per root analysis is `32`. When a budget boundary is reached, the affected call remains `Unknown`; later independent roots can still analyze and cache the same source method when their own budget allows it.

Out of scope remains full virtual/interface dispatch, external assemblies, constructors, properties, operators, local functions, lambdas as independent targets, whole-compilation call graphs, and whole-solution analysis. Cycles are detected and remain safe; mutual recursion is detected but not solved.

## Supported Direct-Recursion Scope

Phase 6 solves only bounded direct recursion. A recursive call must resolve semantically to the same method definition, the method must provide compatible base-case evidence, the recursive argument must be provably reducing, and non-recursive local work must be known.

Supported recurrence families are:

- summation/decrement: `T(n)=T(n-c)+f(n)` for supported polylogarithmic `f(n)`;
- simple exponential direct recursion: `aT(n-c)+polylog` for the supported constant-coefficient subset, including Fibonacci-like `T(n-1)+T(n-2)+1`;
- Master Theorem: one scale term `aT(n/b)+f(n)` for supported polylogarithmic tolls;
- restricted/bounded Akra-Bazzi subset: scale-only recursive terms with no perturbation terms and supported polylogarithmic tolls.

Examples include `T(n)=T(n-1)+1 => O(n)`, `T(n)=T(n-1)+n => O(n^2)`, `T(n)=T(n-1)+log n => O(n log n)`, `2T(n-1)+1 => O(2^n)`, `T(n/2)+1 => O(log n)`, `2T(n/2)+n => O(n log n)`, `3T(n/2)+n => O(n^1.585)`, and `T(n/3)+T(2n/3)+n => O(n log n)`.

The analyzer does not implement full Akra-Bazzi, arbitrary characteristic polynomials, symbolic recurrence parsing, general numerical integration, MathNet, SymPy, Workspaces, or inherited solver project references. Unsupported cases remain `Unknown`.

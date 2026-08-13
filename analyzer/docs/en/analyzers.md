# Analyzer Catalog

[English](analyzers.md) | [Portugues (Brasil)](../pt-BR/analyzers.md)

This page is the public catalog of diagnostics exposed by `ComplexityAnalysis.Analyzers` in Phase 4.

The analyzer resolves known BCL and LINQ operations through Roslyn symbols. Same-name custom methods are not treated as known operations. Unsupported or unresolved operations remain `Unknown`.

## Summary

| ID | Title | Category | Default severity | Enabled by default |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | `false` |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | `true` |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | `true` |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | `true` |
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

`BIG0001` is informational. It exposes the analyzer's known estimate for a supported method, such as `O(1)`, `O(n)`, `O(n log n)`, or `O(n^2)`.

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

### Non-Trigger Cases

No diagnostic is reported when:

- `BIG0001` is not enabled by consumer configuration;
- the method result is `Unknown`;
- the method depends on unsupported project-local method calls or unresolved operations.

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

Phase 4 includes a small documented subset:

- BCL: selected `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>`, array, and string operations.
- LINQ immediate or terminal operations: `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max`, and `Aggregate`.
- LINQ deferred operations: `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct`, and `GroupBy`.

Deferred LINQ pipeline creation is treated as setup work. Enumeration cost is counted only when a supported terminal operation or `foreach` consumes the pipeline.

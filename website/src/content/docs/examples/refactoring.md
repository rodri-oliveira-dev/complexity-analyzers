---
title: Before and after a refactoring
description: Replace repeated linear lookup with an indexed set while preserving the analysis assumptions.
sidebar:
  order: 4
---

## Before

```csharp
static int CountKnown(List<string> values, List<string> known)
{
    var count = 0;
    foreach (var value in values)
        if (known.Contains(value))
            count++;
    return count;
}
```

`List<T>.Contains` under the loop supports `BIG1001` and a composed linear-by-linear estimate.

## After

```csharp
static int CountKnown(List<string> values, List<string> known)
{
    var index = new HashSet<string>(known, StringComparer.Ordinal);
    var count = 0;
    foreach (var value in values)
        if (index.Contains(value))
            count++;
    return count;
}
```

The lookup is now expected `O(1)` under the documented hash assumptions, so the linear-lookup-inside-iteration diagnostic no longer applies. Building the set is still linear in `known`, and the equality semantics are now explicit.

This is a useful refactoring when the method performs enough lookups to repay index construction and set semantics match the requirement. For tiny inputs or a single lookup, the original may remain the better engineering choice.

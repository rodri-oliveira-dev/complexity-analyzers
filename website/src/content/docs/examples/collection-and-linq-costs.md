---
title: Collection and LINQ costs
description: Interpret supported lookup, materialization, ordering, and deferred LINQ examples.
sidebar:
  order: 2
---

## Linear lookup inside a loop

```csharp
foreach (var id in requestedIds)
{
    if (existingIds.Contains(id))
        Process(id);
}
```

When `existingIds` resolves to `List<T>`, the analyzer knows `Contains` is linear and can report `BIG1001`. With independent input sizes, the composed estimate preserves both variables rather than silently treating them as equal.

## Materialization inside a loop

```csharp
foreach (var group in groups)
{
    var active = group.Where(IsActive).ToList();
    Consume(active);
}
```

The terminal `ToList` enumerates and allocates inside the outer iteration. Supported symbol resolution can report `BIG1002`. Moving materialization outside the loop is correct only when the data and semantics allow reuse.

## Ordering inside a loop

```csharp
foreach (var batch in batches)
{
    var first = batch.OrderBy(x => x.Score).First();
    Consume(first);
}
```

Supported ordering inside iteration can report `BIG1003`. The right redesign may be pre-ordering, using a selection algorithm, or keeping the original code when input sizes are bounded and clarity wins.

## Collection choice

Changing `List<T>` to `HashSet<T>` can replace linear lookup with expected `O(1)` lookup under hashing assumptions. It also changes ordering, duplicate behavior, memory use, and construction cost. The tool provides evidence; it does not choose the data structure for you.

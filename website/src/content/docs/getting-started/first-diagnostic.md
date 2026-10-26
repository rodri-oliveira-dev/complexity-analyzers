---
title: First diagnostic
description: Trigger, interpret, and configure a first BIG1001 analyzer diagnostic.
sidebar:
  order: 3
---

After adding the analyzer package, build a project containing a linear lookup inside an iteration:

```csharp
static List<int> FindMatches(List<int> items, List<int> candidates)
{
    var matches = new List<int>();
    foreach (var item in items)
    {
        if (candidates.Contains(item))
            matches.Add(item);
    }

    return matches;
}
```

`List<T>.Contains` is linear in the candidate count. Nested under a loop over `items`, the supported estimate composes both input variables and `BIG1001` identifies the costly lookup.

## Make the policy visible

Diagnostic severity uses standard analyzer configuration:

```ini
[*.cs]
dotnet_diagnostic.BIG1001.severity = warning
```

The diagnostic points to a pattern, not an automatic refactoring mandate. A `HashSet<T>` can offer expected constant lookup under documented hashing assumptions, but building that set also has cost and semantic implications. Interpret the evidence in the context of input sizes and call frequency.

See the [diagnostic catalog](/complexity-analyzers/analyzer/diagnostics/) and [configuration reference](/complexity-analyzers/analyzer/configuration/).

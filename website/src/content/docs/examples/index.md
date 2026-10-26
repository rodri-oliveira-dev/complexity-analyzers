---
title: Worked examples
description: C# scenarios that connect source patterns to supported diagnostics, metrics, and refactorings.
sidebar:
  order: 1
---

These examples are teaching aids grounded in supported behavior. Exact locations, variable names, and aggregate metrics depend on the analyzed source and configuration.

- [Collection and LINQ costs](./collection-and-linq-costs/) covers lookup, materialization, ordering, and collection choice.
- [Recursion and cognitive complexity](./recursion-and-cognition/) separates asymptotic growth from comprehension metrics.
- [Before and after a refactoring](./refactoring/) explains why removing a diagnostic is not the same as proving every performance property.
- [Snapshot comparison](./snapshot-comparison/) demonstrates deterministic regression review.

For runnable repository code, also see the [`samples/ComplexityAnalysis.Sample`](https://github.com/rodri-oliveira-dev/complexity-analyzers/tree/main/samples/ComplexityAnalysis.Sample) project.

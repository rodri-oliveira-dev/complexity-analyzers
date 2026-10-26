---
title: Complexity models
description: Distinguish Big-O, time and space, cyclomatic and cognitive complexity, nesting, size, and Halstead metrics.
sidebar:
  order: 2
---

## Big-O and time complexity

Big-O expresses how work grows relative to input size. `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, and `O(n²)` describe growth classes, not stopwatch durations. The analyzer composes supported loop bounds, known operations, safe source calls, and selected recurrences.

Estimates also carry guarantees and assumptions. A hash lookup may be expected `O(1)` under sufficiently distributed hash codes and constant-cost hashing/equality; that is not an unconditional worst-case promise.

## Space complexity

The current public diagnostics and project reports focus on algorithmic time estimates and structural/size metrics. They do not provide a general public auxiliary-space complexity estimate. Allocation-related operations such as materialization can be recognized as costly operations, but that is not equivalent to a complete space-complexity analysis.

## Structural metrics

Cyclomatic complexity is independent from Big-O: a constant-time method can contain many branches. Cognitive complexity adds penalties for structural breaks and nesting under the project's documented convention. Maximum nesting depth measures depth rather than path count.

## Size and Halstead

NLOC, statement count, token count, and parameter count are thresholdable size measures. Halstead measures are implemented internally for tooling and reporting foundations, but there is no public Halstead threshold diagnostic yet.

No single metric establishes maintainability. Use multiple signals and examine the code and evidence behind each result.

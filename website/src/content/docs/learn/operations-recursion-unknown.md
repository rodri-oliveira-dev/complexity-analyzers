---
title: Operations, recursion, and Unknown
description: Understand known collection and LINQ costs, bounded interprocedural analysis, recursion solving, and inconclusive results.
sidebar:
  order: 3
---

## Collections and LINQ

Known costs are resolved by Roslyn symbol identity, not by method name text. This prevents an unrelated custom `Contains` from being treated as `List<T>.Contains`. Supported mappings distinguish linear list lookup, expected hash lookup, ordering, materialization, deferred pipelines, and terminal enumeration.

Deferred LINQ operators do not pay a full enumeration cost at pipeline creation. Supported terminal operations and `foreach` consume the pipeline; proven re-enumeration is counted again.

## Interprocedural analysis

Safe source calls in the same compilation can be analyzed on demand. Dispatch must be proven safe, and traversal is bounded by call-depth and method budgets. The analyzer does not build an unbounded whole-program call graph.

## Direct recursion

Selected direct recurrences can be solved when the recursive call shape and a compatible base case are proven. Supported families include decrement/summation forms, simple exponential recursion, Master Theorem cases, and a restricted Akra–Bazzi subset.

## What `Unknown` means

`Unknown` means the bounded analysis could not prove a safe estimate. It does not mean `O(1)`, success, failure, or efficient code. Common causes include dynamic or unsafe dispatch, unresolved delegate targets, unsupported loop bounds, mutual recursion, missing base-case evidence, budget exhaustion, cancellation, or unsupported syntax.

The project prefers an explicit inconclusive result to a high-confidence-looking false positive.

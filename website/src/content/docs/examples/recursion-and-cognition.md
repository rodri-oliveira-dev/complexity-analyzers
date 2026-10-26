---
title: Recursion and cognitive complexity
description: Compare recurrence-based Big-O with independent structural comprehension metrics.
sidebar:
  order: 3
---

## Exponential direct recursion

```csharp
static int Fibonacci(int n)
{
    if (n <= 1) return n;
    return Fibonacci(n - 1) + Fibonacci(n - 2);
}
```

With compatible base-case evidence and supported recursive arguments, the recurrence is exponential and can produce `BIG1005`. The diagnostic is based on call shape and local work, not on the method name.

## A logarithmic recurrence

```csharp
static int Steps(int n)
{
    if (n <= 1) return 0;
    return 1 + Steps(n / 2);
}
```

This supported divide recurrence can resolve to `O(log n)`. Missing or incompatible base cases make the result `Unknown` rather than guessed.

## Cognitive complexity is separate

A method with nested `if`, loops, catches, switches, and logical sequences can have high cognitive complexity even if all inputs are bounded. Conversely, a short recursive method can have low cognitive score while its running time grows exponentially. Configure and interpret each metric independently.

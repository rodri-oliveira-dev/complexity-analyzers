---
title: Recursão e complexidade cognitiva
description: Compare Big-O baseado em recorrência com métricas estruturais independentes de compreensão.
sidebar:
  order: 3
---

## Recursão direta exponencial

```csharp
static int Fibonacci(int n)
{
    if (n <= 1) return n;
    return Fibonacci(n - 1) + Fibonacci(n - 2);
}
```

Com evidência compatível de caso base e argumentos recursivos suportados, a recorrência é exponencial e pode produzir `BIG1005`. O diagnóstico se baseia na forma da chamada e no trabalho local, não no nome do método.

## Uma recorrência logarítmica

```csharp
static int Steps(int n)
{
    if (n <= 1) return 0;
    return 1 + Steps(n / 2);
}
```

Essa recorrência de divisão suportada pode resultar em `O(log n)`. Casos base ausentes ou incompatíveis produzem `Unknown`, não um palpite.

## Complexidade cognitiva é separada

Um método com `if`, loops, catches, switches e sequências lógicas aninhadas pode ter alta complexidade cognitiva mesmo com entradas limitadas. Por outro lado, um método recursivo curto pode ter score cognitivo baixo enquanto o tempo cresce exponencialmente. Configure e interprete cada métrica de forma independente.

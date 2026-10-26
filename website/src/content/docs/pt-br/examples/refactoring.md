---
title: Antes e depois de uma refatoração
description: Substitua buscas lineares repetidas por um conjunto indexado, preservando as hipóteses da análise.
sidebar:
  order: 4
---

## Antes

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

`List<T>.Contains` sob o loop sustenta `BIG1001` e uma estimativa composta linear por linear.

## Depois

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

A busca agora é `O(1)` esperada sob as hipóteses documentadas de hash, então o diagnóstico de busca linear dentro de iteração deixa de se aplicar. Construir o conjunto continua linear em `known` e a semântica de igualdade passa a ser explícita.

Essa refatoração é útil quando existem buscas suficientes para compensar a construção do índice e a semântica de conjunto atende ao requisito. Para entradas pequenas ou uma única busca, o original pode continuar sendo a melhor decisão de engenharia.

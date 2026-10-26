---
title: Custos de coleções e LINQ
description: Interprete exemplos suportados de busca, materialização, ordenação e LINQ adiado.
sidebar:
  order: 2
---

## Busca linear dentro de loop

```csharp
foreach (var id in requestedIds)
{
    if (existingIds.Contains(id))
        Process(id);
}
```

Quando `existingIds` resolve para `List<T>`, o analyzer sabe que `Contains` é linear e pode reportar `BIG1001`. Com tamanhos de entrada independentes, a estimativa preserva as duas variáveis em vez de tratá-las silenciosamente como iguais.

## Materialização dentro de loop

```csharp
foreach (var group in groups)
{
    var active = group.Where(IsActive).ToList();
    Consume(active);
}
```

O `ToList` terminal enumera e aloca dentro da iteração externa. A resolução de símbolos suportada pode reportar `BIG1002`. Mover a materialização para fora só é correto quando dados e semântica permitem reutilização.

## Ordenação dentro de loop

```csharp
foreach (var batch in batches)
{
    var first = batch.OrderBy(x => x.Score).First();
    Consume(first);
}
```

Ordenação suportada dentro de iteração pode reportar `BIG1003`. O redesenho correto pode ser ordenar antes, usar seleção ou manter o código original quando as entradas são limitadas e clareza é prioritária.

## Escolha da coleção

Trocar `List<T>` por `HashSet<T>` pode substituir busca linear por busca `O(1)` esperada sob hipóteses de hashing. Também muda ordem, duplicatas, memória e custo de construção. A ferramenta fornece evidência; não escolhe a estrutura de dados por você.

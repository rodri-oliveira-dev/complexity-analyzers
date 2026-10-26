---
title: Operações, recursão e Unknown
description: Entenda custos conhecidos de coleções e LINQ, análise interprocedural limitada, recursão e resultados inconclusivos.
sidebar:
  order: 3
---

## Coleções e LINQ

Custos conhecidos são resolvidos pela identidade do símbolo Roslyn, não pelo texto do nome. Isso evita tratar um `Contains` customizado como `List<T>.Contains`. Os mapeamentos suportados distinguem busca linear em lista, busca esperada em hash, ordenação, materialização, pipelines adiados e enumeração terminal.

Operadores LINQ adiados não pagam uma enumeração completa ao criar o pipeline. Operações terminais suportadas e `foreach` consomem o pipeline; reenumeração comprovada é contada novamente.

## Análise interprocedural

Chamadas seguras de código-fonte na mesma compilação podem ser analisadas sob demanda. O dispatch precisa ser comprovadamente seguro e a travessia é limitada por profundidade e quantidade de métodos. O analyzer não constrói um call graph ilimitado de todo o programa.

## Recursão direta

Recorrências diretas selecionadas podem ser resolvidas quando a forma da chamada e um caso base compatível são comprovados. As famílias incluem decremento/somatório, recursão exponencial simples, casos do Teorema Mestre e um subconjunto restrito de Akra–Bazzi.

## O que `Unknown` significa

`Unknown` significa que a análise limitada não conseguiu provar uma estimativa segura. Não significa `O(1)`, sucesso, falha ou código eficiente. Causas comuns incluem dispatch dinâmico ou inseguro, delegates não resolvidos, limites de loop não suportados, recursão mútua, ausência de caso base, esgotamento de budget, cancelamento ou sintaxe não suportada.

O projeto prefere um resultado inconclusivo explícito a um falso positivo com aparência de alta confiança.

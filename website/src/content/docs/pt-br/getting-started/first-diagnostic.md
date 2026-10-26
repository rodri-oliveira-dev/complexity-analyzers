---
title: Primeiro diagnóstico
description: Produza, interprete e configure um primeiro diagnóstico BIG1001 do analyzer.
sidebar:
  order: 3
---

Depois de adicionar o pacote do analyzer, compile um projeto com uma busca linear dentro de uma iteração:

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

`List<T>.Contains` é linear no número de candidatos. Dentro de um loop sobre `items`, a estimativa suportada compõe as duas variáveis de entrada e `BIG1001` identifica a busca custosa.

## Torne a política visível

A severidade usa a configuração padrão de analyzers:

```ini
[*.cs]
dotnet_diagnostic.BIG1001.severity = warning
```

O diagnóstico aponta um padrão, não uma obrigação automática de refatoração. Um `HashSet<T>` pode oferecer busca constante esperada sob hipóteses documentadas de hashing, mas construir o conjunto também tem custo e implicações semânticas. Interprete a evidência considerando tamanho das entradas e frequência da chamada.

Consulte o [catálogo de diagnósticos](/complexity-analyzers/pt-br/analyzer/diagnostics/) e a [referência de configuração](/complexity-analyzers/pt-br/analyzer/configuration/).

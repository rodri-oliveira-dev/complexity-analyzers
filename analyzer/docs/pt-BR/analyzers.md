# Catalogo de Analyzers

[English](../en/analyzers.md) | Portugues (Brasil)

Esta pagina e o catalogo publico dos diagnostics expostos por `ComplexityAnalysis.Analyzers` na Phase 6.

O analyzer resolve operacoes conhecidas de BCL e LINQ por simbolos Roslyn, pode propagar complexidade de metodos fonte seguros na mesma compilation e pode resolver formatos selecionados de recorrencia diretamente recursiva. Metodos customizados com o mesmo nome nao sao tratados como operacoes BCL/LINQ conhecidas. Operacoes nao suportadas, inseguras, ciclicas, limitadas por budget, numericamente inconclusivas ou nao resolvidas continuam como `Unknown`.

## Resumo

| ID | Titulo | Categoria | Severidade padrao | Habilitado por padrao |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | `false` |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | `true` |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | `true` |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | `true` |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | `true` |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | `true` |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | `false` |

## BIG0001 - Estimated Algorithmic Complexity

| Propriedade | Valor |
| --- | --- |
| ID | `BIG0001` |
| Titulo | `Estimated algorithmic complexity` |
| Categoria | `Complexity` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `false` |
| Localizacao | Identificador do metodo |
| Mensagem | `Estimated time complexity: {complexity}` |

### Problema Detectado

`BIG0001` e informational. Ele expoe a estimativa conhecida do analyzer para um metodo suportado, como `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`, `O(n^1.585)` ou `O(1.618^n)`. Na Phase 6, essa estimativa pode incluir complexidade propagada de callees fonte seguros e recursao direta resolvida.

### Exemplo

```csharp
public sealed class Sample
{
    public void M(int[] values)
    {
        foreach (var value in values)
        {
            var x = value + 1;
        }
    }
}
```

Quando habilitado, o diagnostic e reportado em `M` com `Estimated time complexity: O(n)`.

Exemplo interprocedural com chamada fonte:

```csharp
public sealed class Sample
{
    public void M(int[] values)
    {
        Helper(values);
    }

    private void Helper(int[] items)
    {
        foreach (var item in items)
        {
            var x = item + 1;
        }
    }
}
```

Quando `BIG0001` esta habilitado, `M` reporta `Estimated time complexity: O(n)`.

Exemplo de recursao direta:

```csharp
public sealed class Sample
{
    public int BinarySearch(int n, bool left)
    {
        if (n <= 1)
        {
            return -1;
        }

        if (left)
        {
            return BinarySearch(n / 2, false);
        }

        return BinarySearch(n / 2, false);
    }
}
```

Quando `BIG0001` esta habilitado, `BinarySearch` reporta `Estimated time complexity: O(log n)`. As duas chamadas recursivas sintaticas estao em branches exclusivos e nao sao contadas como multiplicidade dois.

### Casos Que Nao Geram Diagnostic

Nenhum diagnostic e reportado quando:

- `BIG0001` nao esta habilitado pela configuracao do consumidor;
- o resultado do metodo e `Unknown`;
- o metodo depende de operacoes nao suportadas, inseguras, ciclicas, limitadas por budget, numericamente inconclusivas ou nao resolvidas;
- recursao direta sem evidencia de base case, com argumentos nao redutores, com trabalho local desconhecido ou fora das familias de recorrencia suportadas;
- recursao mutua em vez de recursao direta.

### Configuracao

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Use `none` para mante-lo desabilitado:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = none
```

## BIG1001 - Linear Lookup Inside Iteration

| Propriedade | Valor |
| --- | --- |
| ID | `BIG1001` |
| Titulo | `Linear lookup inside iteration` |
| Categoria | `Complexity` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `true` |
| Localizacao | Invocacao de lookup |
| Mensagem | Operacao de lookup linear, estimativa da iteracao externa e estimativa combinada |

### Problema Detectado

`BIG1001` reporta um lookup linear semanticamente conhecido executado dentro de um loop analisavel. O exemplo principal da Phase 4 e `List<T>.Contains` dentro de um loop sobre outra entrada.

### Exemplo

```csharp
using System.Collections.Generic;

public sealed class Sample
{
    void M(List<int> customers, List<int> blockedCustomers)
    {
        foreach (var customer in customers)
        {
            if (blockedCustomers.Contains(customer))
            {
            }
        }
    }
}
```

O diagnostic aponta para `blockedCustomers.Contains(customer)`.

### Casos Que Nao Geram Diagnostic

Nenhum diagnostic e reportado para:

- o mesmo lookup fora de um loop;
- `HashSet<T>.Contains`, porque o mapping suportado e lookup constante em caso medio;
- metodos customizados `Contains` com o mesmo nome;
- loops cuja contagem de iteracoes nao pode ser analisada;
- lookups cujo tamanho do receiver nao pode ser resolvido com seguranca.

### Configuracao

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

Desabilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = none
```

## BIG1002 - Materialization Inside Iteration

| Propriedade | Valor |
| --- | --- |
| ID | `BIG1002` |
| Titulo | `Materialization inside iteration` |
| Categoria | `Complexity` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `true` |
| Localizacao | Invocacao de materializacao |
| Mensagem | Operacao de materializacao, estimativa da iteracao externa e estimativa combinada |

### Problema Detectado

`BIG1002` reporta materializacao LINQ suportada e repetida dentro de um loop analisavel. Materializadores suportados incluem `ToList`, `ToArray`, `ToDictionary` e `ToHashSet`.

### Exemplo

```csharp
using System.Collections.Generic;
using System.Linq;

public sealed class Sample
{
    void M(List<int> customers, IEnumerable<int> items)
    {
        foreach (var customer in customers)
        {
            var copy = items.ToList();
        }
    }
}
```

O diagnostic aponta para `items.ToList()`.

### Casos Que Nao Geram Diagnostic

Nenhum diagnostic e reportado para:

- materializacao fora de um loop;
- metodos customizados `ToList` ou `ToArray`;
- loops cuja contagem de iteracoes nao pode ser analisada;
- materializadores cujo tamanho da fonte nao pode ser resolvido com seguranca.

### Configuracao

```ini
[*.cs]

dotnet_diagnostic.BIG1002.severity = warning
```

Desabilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG1002.severity = none
```

## BIG1003 - Ordering Inside Iteration

| Propriedade | Valor |
| --- | --- |
| ID | `BIG1003` |
| Titulo | `Ordering inside iteration` |
| Categoria | `Complexity` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `true` |
| Localizacao | Invocacao de ordenacao deferred |
| Mensagem | Operacao de ordenacao, estimativa da iteracao externa e estimativa combinada |

### Problema Detectado

`BIG1003` reporta trabalho de ordenacao deferred suportado quando o consumo e comprovado dentro de um loop analisavel. Operacoes suportadas incluem `OrderBy`, `OrderByDescending`, `ThenBy` e `ThenByDescending`.

### Exemplo

```csharp
using System.Collections.Generic;
using System.Linq;

public sealed class Sample
{
    void M(List<int> customers, IEnumerable<int> items)
    {
        foreach (var customer in customers)
        {
            var sorted = items.OrderBy(item => item).ToList();
        }
    }
}
```

O diagnostic aponta para `items.OrderBy(item => item)`, nao para `ToList()`.

### Casos Que Nao Geram Diagnostic

Nenhum diagnostic e reportado para:

- criar uma pipeline `OrderBy` dentro de um loop sem consumi-la;
- consumir a pipeline ordenada fora do loop;
- metodos customizados `OrderBy`;
- loops cuja contagem de iteracoes nao pode ser analisada;
- cadeias de ordenacao cuja fonte nao pode ser resolvida com seguranca.

### Configuracao

```ini
[*.cs]

dotnet_diagnostic.BIG1003.severity = warning
```

Desabilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG1003.severity = none
```

## BIG1004 - Input-Dependent Method Call Inside Iteration

| Propriedade | Valor |
| --- | --- |
| ID | `BIG1004` |
| Titulo | `Input-dependent method call inside iteration` |
| Categoria | `Complexity` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `true` |
| Localizacao | Invocacao de metodo fonte |
| Mensagem | Metodo fonte, estimativa do callee, estimativa da iteracao externa e estimativa combinada |

### Problema Detectado

`BIG1004` reporta uma chamada de metodo fonte suportada com complexidade conhecida dependente de entrada quando essa chamada executa dentro de um loop analisavel.

### Exemplo

```csharp
public sealed class Sample
{
    void M(int[] customers, int[] blocked)
    {
        foreach (var customer in customers)
        {
            CheckAgainstBlacklist(customer, blocked);
        }
    }

    private void CheckAgainstBlacklist(int customer, int[] blocked)
    {
        foreach (var value in blocked)
        {
            var x = value + customer;
        }
    }
}
```

O diagnostic aponta para `CheckAgainstBlacklist(customer, blocked)` e reporta o padrao combinado `O(n * m)`.

### Casos Que Nao Geram Diagnostic

Nenhum diagnostic e reportado para:

- chamadas fonte fora de loops;
- chamadas fonte cuja complexidade substituida e `O(1)`;
- dispatch virtual ou de interface inseguro;
- callees ciclicos ou limitados por budget;
- bindings de argumentos desconhecidos;
- operacoes BCL/LINQ conhecidas ja tratadas por `BIG1001`, `BIG1002` ou `BIG1003`.

### Configuracao

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

Desabilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = none
```

## BIG1005 - Exponential Recursive Growth

| Propriedade | Valor |
| --- | --- |
| ID | `BIG1005` |
| Titulo | `Exponential recursive growth` |
| Categoria | `Complexity` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `true` |
| Localizacao | Identificador do metodo recursivo |
| Mensagem | `Recursive method '{method}' has estimated exponential time complexity {complexity}` |

### Problema Detectado

`BIG1005` reporta um metodo diretamente recursivo suportado cuja recorrencia resolvida e exponencial. Ele e intencionalmente informational e nao prescreve memoization ou reescrita.

### Exemplo

```csharp
public sealed class Sample
{
    int Fibonacci(int n)
    {
        if (n <= 1)
        {
            return n;
        }

        return Fibonacci(n - 1) + Fibonacci(n - 2);
    }
}
```

O diagnostic aponta para `Fibonacci` e reporta `O(1.618^n)`.

### Casos Que Nao Geram Diagnostic

Nenhum diagnostic e reportado para:

- recursao resolvida polinomial ou logaritmica;
- resultados de recorrencia unknown, unsupported, invalid ou numericamente inconclusivos;
- evidencia de base case ausente;
- argumentos recursivos nao redutores como `n` ou `n + 1`;
- recursao mutua, que e detectada mas nao resolvida.

### Configuracao

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

Desabilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = none
```

## BIG9000 - Analyzer Execution Probe

| Propriedade | Valor |
| --- | --- |
| ID | `BIG9000` |
| Titulo | `Analyzer execution probe` |
| Categoria | `Infrastructure` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `false` |
| Localizacao | Inicio de um arquivo-fonte quando disponivel; caso contrario sem localizacao de fonte |
| Mensagem | `ComplexityAnalysis.Analyzers execution probe is active` |

### Problema Detectado

`BIG9000` nao detecta um problema de codigo. Ele e um probe de infraestrutura usado para provar que o pacote do analyzer foi carregado, inicializado e conseguiu reportar diagnostics.

### Exemplo

Qualquer codigo C# pode produzir o probe quando ele e habilitado explicitamente:

```csharp
public sealed class Sample
{
    public int M() => 42;
}
```

### Casos Que Nao Geram Diagnostic

Nenhum diagnostic e reportado quando `BIG9000` nao esta habilitado. Quando habilitado, ele reporta no maximo uma vez por compilation.

### Configuracao

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Desabilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## Subconjunto Suportado de Operacoes Conhecidas

O analyzer inclui um subconjunto pequeno e documentado:

- BCL: operacoes selecionadas de `List<T>`, `Dictionary<TKey,TValue>`, `HashSet<T>`, arrays e string.
- LINQ imediatas ou terminais: `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max` e `Aggregate`.
- LINQ deferred: `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct` e `GroupBy`.

A criacao de pipeline LINQ deferred e tratada como trabalho de setup. O custo de enumeracao so e contado quando uma operacao terminal suportada ou `foreach` consome a pipeline.

## Escopo Suportado de Metodos Fonte

A analise interprocedural de metodos fonte e limitada a metodos ordinarios na mesma Roslyn `Compilation` com dispatch seguro:

- metodos static;
- metodos private;
- metodos ordinarios nao virtuais;
- dispatch sealed quando o alvo de runtime e comprovado.

A travessia e demand-driven: um callee fonte e analisado somente quando um caller analisado alcanca aquela invocacao. O analyzer nao constroi um call graph obrigatorio da compilation inteira e nao pre-analisa todas as syntax trees para propagacao interprocedural.

A expansao e limitada por budgets internos: a profundidade maxima de chamadas e `5`, e o maximo de expansoes uncached de metodos fonte por analise raiz e `32`. Quando um boundary de budget e alcancado, a chamada afetada permanece `Unknown`; raizes independentes posteriores ainda podem analisar e cachear o mesmo metodo fonte quando o proprio budget permitir.

Continuam fora de escopo: dispatch virtual/interface completo, assemblies externos, construtores, propriedades, operadores, local functions, lambdas como alvos independentes, call graph de compilation inteira e analise de solution inteira. Ciclos sao detectados e continuam seguros; recursao mutua e detectada, mas nao resolvida.

## Escopo Suportado de Recursao Direta

A Phase 6 resolve apenas recursao direta limitada. Uma chamada recursiva precisa resolver semanticamente para a mesma definicao de metodo, o metodo precisa fornecer evidencia compativel de base case, o argumento recursivo precisa ser comprovadamente redutor e o trabalho local nao recursivo precisa ser conhecido.

Familias de recorrencia suportadas:

- soma/decremento: `T(n)=T(n-c)+f(n)` para `f(n)` polylogaritmico suportado;
- recursao direta exponencial simples: `aT(n-c)+polylog` para o subconjunto de coeficiente constante suportado, incluindo Fibonacci `T(n-1)+T(n-2)+1`;
- Master Theorem: um termo de escala `aT(n/b)+f(n)` para tolls polylogaritmicos suportados;
- subconjunto restrito/limitado de Akra-Bazzi: termos recursivos apenas por escala, sem perturbacoes, e tolls polylogaritmicos suportados.

Exemplos incluem `T(n)=T(n-1)+1 => O(n)`, `T(n)=T(n-1)+n => O(n^2)`, `T(n)=T(n-1)+log n => O(n log n)`, `2T(n-1)+1 => O(2^n)`, `T(n/2)+1 => O(log n)`, `2T(n/2)+n => O(n log n)`, `3T(n/2)+n => O(n^1.585)` e `T(n/3)+T(2n/3)+n => O(n log n)`.

O analyzer nao implementa Akra-Bazzi completo, polinomios caracteristicos arbitrarios, parsing simbolico de recorrencias, integracao numerica geral, MathNet, SymPy, Workspaces ou referencias a projetos solver herdados. Casos nao suportados permanecem `Unknown`.

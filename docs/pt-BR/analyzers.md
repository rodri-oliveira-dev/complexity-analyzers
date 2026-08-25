# Catálogo de Analyzers

[English](../en/analyzers.md) | Português (Brasil)

Esta página documenta os diagnósticos públicos expostos por `ComplexityAnalysis.Analyzers` e as fronteiras de análise que determinam quando cada regra pode reportar.

O analyzer resolve operações BCL e LINQ suportadas pela identidade de símbolo do Roslyn, pode propagar complexidade de métodos-fonte seguros dentro da mesma compilation e pode resolver formatos selecionados de recorrência de recursão direta. Comportamentos não suportados, inseguros, cíclicos, limitados por budget, numericamente inconclusivos ou não resolvidos permanecem `Unknown` em vez de serem estimados.

## Resumo

| ID | Título | Categoria | Severidade padrão | Habilitado por padrão |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | `false` |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | `true` |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | `true` |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | `true` |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | `true` |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | `true` |
| `BIG1006` | Method complexity exceeds configured threshold | `Complexity` | `Info` | `true` |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | `false` |

## Como interpretar os diagnósticos

O analyzer é intencionalmente conservador. Uma regra só reporta quando a sintaxe e o modelo semântico fornecem evidência suficiente para a relação de complexidade relevante. A ausência de um diagnóstico não prova que uma operação seja eficiente; também pode significar que o analyzer não conseguiu comprovar os fatos necessários com segurança.

`Unknown` é, portanto, um resultado de primeira classe e não é convertido para `O(1)` ou qualquer outra classe conhecida.

## BIG0001 — Complexidade algorítmica estimada

`BIG0001` expõe a estimativa conhecida do analyzer para um método suportado, como `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`, `O(n^1.585)` ou `O(1.618^n)`.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `false` |
| Localização | Identificador do método |
| Mensagem | `Estimated time complexity: {complexity}` |

A estimativa pode incluir limites de loop suportados, operações BCL/LINQ conhecidas, callees de métodos-fonte seguros e formatos selecionados de recursão direta resolvida.

Exemplo:

```csharp
public void M(int[] values)
{
    foreach (var value in values)
    {
        _ = value + 1;
    }
}
```

Quando habilitado, `M` reporta `Estimated time complexity: O(n)`.

Nenhum diagnóstico é reportado quando a regra não está habilitada, o resultado do método é `Unknown`, uma operação necessária é não suportada ou não resolvida, uma fronteira interprocedural não pode ser comprovada com segurança ou a recursão direta fica fora do modelo de recorrência suportado.

Habilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

## BIG1001 — Busca linear dentro de iteração

`BIG1001` reporta uma busca linear suportada executada dentro de uma iteração analisável.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação da busca |

Exemplo típico:

```csharp
foreach (var customer in customers)
{
    if (blockedCustomers.Contains(customer))
    {
    }
}
```

Quando `blockedCustomers` é um `List<T>` suportado, a busca é linear em relação ao tamanho da coleção e pode compor o custo do loop externo.

A regra não reporta a mesma busca fora de um loop, buscas de custo médio constante suportadas como `HashSet<T>.Contains`, métodos customizados com o mesmo nome ou casos em que o tamanho do loop/receptor não pode ser resolvido com segurança.

Configure com:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

## BIG1002 — Materialização dentro de iteração

`BIG1002` reporta materialização LINQ suportada e repetida dentro de uma iteração analisável. Os materializadores suportados incluem `ToList`, `ToArray`, `ToDictionary` e `ToHashSet`.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação de materialização |

Exemplo:

```csharp
foreach (var customer in customers)
{
    var copy = items.ToList();
}
```

A regra não reporta materialização fora de loop, métodos customizados com o mesmo nome, tamanhos de fonte não resolvidos ou loops cuja quantidade de iterações não pode ser analisada.

Configure com:

```ini
[*.cs]

dotnet_diagnostic.BIG1002.severity = warning
```

## BIG1003 — Ordenação dentro de iteração

`BIG1003` reporta ordenação deferred suportada quando o analyzer consegue comprovar que a ordenação é consumida dentro de uma iteração analisável.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação da ordenação deferred |

As operações suportadas incluem `OrderBy`, `OrderByDescending`, `ThenBy` e `ThenByDescending`.

Exemplo:

```csharp
foreach (var customer in customers)
{
    var sorted = items.OrderBy(item => item).ToList();
}
```

O diagnóstico aponta para a operação de ordenação. Criar uma pipeline de ordenação sem consumi-la dentro do loop não reporta, porque o custo completo de ordenação/enumeração ainda não foi comprovado naquele ponto.

Configure com:

```ini
[*.cs]

dotnet_diagnostic.BIG1003.severity = warning
```

## BIG1004 — Chamada dependente de entrada dentro de iteração

`BIG1004` reporta uma chamada a método-fonte suportado com complexidade conhecida dependente da entrada quando essa chamada é executada dentro de uma iteração analisável.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação do método-fonte |

Exemplo:

```csharp
foreach (var customer in customers)
{
    CheckAgainstBlacklist(customer, blocked);
}

private static void CheckAgainstBlacklist(int customer, int[] blocked)
{
    foreach (var value in blocked)
    {
        _ = value + customer;
    }
}
```

O analyzer pode combinar o loop do caller com o custo dependente de entrada substituído do callee, produzindo um padrão como `O(n * m)`.

A regra não reporta chamadas-fonte fora de loops, callees cujo custo substituído é `O(1)`, dispatch inseguro, binding de argumentos desconhecido, fronteiras de ciclo/budget ou operações de framework já tratadas pelas regras BCL/LINQ.

Configure com:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

## BIG1005 — Crescimento recursivo exponencial

`BIG1005` reporta um método de recursão direta suportado cuja recorrência é resolvida como crescimento exponencial.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Identificador do método recursivo |
| Mensagem | `Recursive method '{method}' has estimated exponential time complexity {complexity}` |

Exemplo:

```csharp
int Fibonacci(int n)
{
    if (n <= 1)
    {
        return n;
    }

    return Fibonacci(n - 1) + Fibonacci(n - 2);
}
```

Para a recorrência suportada no estilo Fibonacci, o analyzer reporta crescimento exponencial como `O(1.618^n)`.

A regra não reporta recursão resolvida como polinomial/logarítmica, formatos não suportados ou inválidos, ausência de base case, argumentos recursivos não redutores, resultados numericamente inconclusivos ou recursão mútua.

Configure com:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

## BIG1006 — Complexidade acima do threshold configurado

`BIG1006` reporta um método cuja complexidade estimada conhecida e comparável é maior que `complexity_analyzers.maximum_complexity`.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Identificador do método |
| Mensagem | `Method '{method}' has estimated complexity {actual}, which exceeds the configured maximum {threshold}` |

O descriptor é habilitado por padrão, mas a regra é funcionalmente opt-in porque o threshold padrão é `none`.

Exemplo de configuração:

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

Um método comprovado como `O(n^2)` excede `n_log_n` e pode reportar. `O(n log n)`, `O(n)`, `Unknown` e expressões multivariadas incomparáveis não reportam para esse threshold.

`BIG1006` é um sinal prático de análise estática, não uma prova matemática universal.

## BIG9000 — Probe de execução do analyzer

`BIG9000` é um diagnóstico de infraestrutura usado para comprovar que o pacote foi carregado, inicializado e executado.

| Propriedade | Valor |
| --- | --- |
| Categoria | `Infrastructure` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `false` |
| Localização | Início de um arquivo-fonte quando disponível; caso contrário, sem localização |
| Mensagem | `ComplexityAnalysis.Analyzers execution probe is active` |

Habilite temporariamente:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Depois do smoke test, desabilite:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Quando habilitado, reporta no máximo uma vez por compilation.

## Subconjunto de operações conhecidas

O analyzer documenta deliberadamente um conjunto limitado de operações conhecidas.

Exemplos BCL incluem operações selecionadas de:

- `List<T>`;
- `Dictionary<TKey,TValue>`;
- `HashSet<T>`;
- arrays;
- strings.

Operações LINQ imediatas/terminais suportadas incluem:

- `Any`, `All`, `Contains`, `Count`, `LongCount`;
- `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`;
- `Sum`, `Min`, `Max`, `Aggregate`.

Operações deferred suportadas incluem:

- `Where`, `Select`, `SelectMany`;
- `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`;
- `Distinct`, `GroupBy`.

Os mappings são baseados nos símbolos resolvidos. Métodos do usuário com o mesmo nome não são mapeados automaticamente.

A criação de uma pipeline deferred é tratada como trabalho de setup. O custo de enumeração ou ordenação é cobrado quando o consumo suportado é comprovado.

## Escopo de métodos-fonte suportados

A análise interprocedural é limitada a métodos-fonte ordinários na mesma Roslyn `Compilation` quando o dispatch é seguro.

Formas suportadas incluem:

- métodos static;
- métodos private;
- métodos ordinários não virtuais;
- dispatch sealed quando o alvo de runtime pode ser comprovado.

O traversal é sob demanda e limitado. A profundidade máxima padrão é `5`, configurável até `16`. O máximo padrão de expansões de métodos-fonte por raiz é `32`, configurável até `128`.

Ficam fora do escopo suportado dispatch virtual/interface inseguro, dynamic dispatch, assemblies externos, construtores, propriedades, operadores, local functions, lambdas como alvos independentes, call graphs de compilation inteira e análise de solution inteira.

Ciclos são detectados de forma conservadora. Recursão direta pode ser delegada ao pipeline de recorrências; recursão mútua continua sem suporte para resolução.

## Escopo de recursão direta suportada

Uma recorrência só pode ser resolvida quando o analyzer consegue comprovar recursão direta semântica, evidência compatível de base case, argumento recursivo redutor e trabalho local conhecido.

As famílias suportadas incluem:

- formas de soma/decremento como `T(n)=T(n-c)+f(n)` para tolls suportados;
- um subconjunto exponencial simples e limitado, incluindo formatos no estilo Fibonacci;
- formas do Master Theorem;
- um subconjunto restrito/limitado de Akra-Bazzi com termos recursivos por escala suportados.

Resultados representativos:

```text
T(n)=T(n-1)+1               => O(n)
T(n)=T(n-1)+n               => O(n^2)
T(n)=T(n-1)+log n           => O(n log n)
2T(n-1)+1                   => O(2^n)
T(n/2)+1                    => O(log n)
2T(n/2)+n                   => O(n log n)
3T(n/2)+n                   => O(n^1.585)
T(n/3)+T(2n/3)+n            => O(n log n)
```

O analyzer não implementa Akra-Bazzi completo, resolução arbitrária por polinômio característico, parsing simbólico geral de recorrências, integração numérica geral ou integração com solvers externos MathNet/SymPy.

Casos não suportados permanecem `Unknown`.

## Configuração

Use a configuração padrão de severidade do Roslyn:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Opções comportamentais, como budgets de análise e threshold máximo de complexidade, estão documentadas em [Configuração](configuration.md).

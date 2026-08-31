# ComplexityAnalysis.Analyzers

[English](README.md) | Português (Brasil)

[![Build & Tests](https://github.com/rodri-oliveira-dev/complexity-analyzers/actions/workflows/complexity-analyzers-ci.yml/badge.svg)](https://github.com/rodri-oliveira-dev/complexity-analyzers/actions/workflows/complexity-analyzers-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_complexity-analyzers&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_complexity-analyzers)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/ComplexityAnalysis.Analyzers?logo=nuget&label=NuGet)](https://www.nuget.org/packages/ComplexityAnalysis.Analyzers)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ComplexityAnalysis.Analyzers?logo=nuget&label=Downloads)](https://www.nuget.org/packages/ComplexityAnalysis.Analyzers)
[![GitHub Release](https://img.shields.io/github/v/release/rodri-oliveira-dev/complexity-analyzers?logo=github&label=Release)](https://github.com/rodri-oliveira-dev/complexity-analyzers/releases/latest)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_complexity-analyzers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_complexity-analyzers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/license/mit)

`ComplexityAnalysis.Analyzers` é um Roslyn Analyzer de tempo de compilação para C# que estima complexidade algorítmica e reporta diagnósticos para padrões potencialmente custosos, sem adicionar dependências de runtime ou instrumentação às aplicações consumidoras.

O analyzer é deliberadamente conservador: quando não consegue comprovar a complexidade com segurança a partir das informações sintáticas e semânticas disponíveis, retorna `Unknown` em vez de fazer uma estimativa insegura.

## Instalação rápida

Ao consumir uma versão publicada do pacote no NuGet.org:

```bash
dotnet add package ComplexityAnalysis.Analyzers
```

O pacote funciona como um analyzer de tempo de compilação. Depois da instalação, o Roslyn o carrega durante builds e análise na IDE; o código da aplicação não chama o analyzer e não assume uma dependência de runtime nele. Para criação local do pacote e consumo a partir do repositório, veja [Primeiros Passos](docs/pt-BR/getting-started.md).

## Exemplo rápido

```csharp
foreach (var item in items)
{
    if (otherItems.Contains(item))
    {
        // ...
    }
}
```

Quando `otherItems` é uma coleção com busca linear, como `List<T>`, isso pode reportar:

```text
BIG1001 - Linear lookup inside iteration
```

Quer ver o analyzer em ação? Consulte o [sample executável](samples/ComplexityAnalysis.Sample/README.md). Para a referência completa de regras e configuração, comece por [Primeiros Passos](docs/pt-BR/getting-started.md) e pelo [Catálogo de Analyzers](docs/pt-BR/analyzers.md).

## O que ele faz

- Estima complexidade Big-O de métodos C# suportados usando sintaxe, símbolos e informações semânticas do Roslyn.
- Detecta operações custosas dentro de iterações, incluindo buscas lineares, materialização de coleções e ordenação.
- Entende um subconjunto documentado de operações BCL e LINQ por identidade do símbolo resolvido, e não apenas pelo nome do método.
- Executa análise interprocedural limitada e sob demanda para chamadas seguras a métodos-fonte dentro da mesma compilation.
- Resolve famílias selecionadas de recorrências de recursão direta, incluindo decremento, recursão exponencial simples, formas do Master Theorem e um subconjunto restrito de Akra-Bazzi.
- Mede Cyclomatic Complexity estrutural independentemente do Big-O, com contabilização de `switch` em modo standard ou Modified McCabe.
- Mede Maximum Control-Flow Nesting Depth independentemente de Big-O e Cyclomatic Complexity.
- Mede NLOC, statement count e token count como métricas independentes de tamanho de executable member.
- Mede Parameter Count source-declared independentemente das métricas de complexidade e tamanho.
- Mede Cognitive Complexity como métrica C# documentada e independente de compreensão de fluxo de controle.
- Define métricas Halstead C# internas para reporting/tooling futuros sem emitir ainda um diagnóstico público de threshold Halstead.
- Permite configurar budgets de análise e um limite máximo de complexidade via `.editorconfig`/analyzer config.
- Executa como um Roslyn Analyzer normal durante build e análise na IDE; o código consumidor não chama o analyzer em runtime.

## Diagnósticos

| ID | Título | Categoria | Severidade padrão | Habilitado por padrão |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | Não |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | Sim |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | Sim |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | Sim |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | Sim |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | Sim |
| `BIG1006` | Method complexity exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG2001` | Cyclomatic complexity exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG2002` | Maximum nesting depth exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG2003` | Method NLOC exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG2004` | Statement count exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG2005` | Token count exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG2006` | Parameter count exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG2007` | Cognitive complexity exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | Não |

`BIG0001` é um diagnóstico informativo opt-in que reporta uma estimativa conhecida de complexidade no identificador do método.

`BIG1005` reporta métodos de recursão direta suportados cuja recorrência resolvida apresenta crescimento exponencial, como em implementações no estilo Fibonacci.

`BIG1006` reporta quando `complexity_analyzers.maximum_complexity` está configurado e uma estimativa conhecida e comparável ultrapassa o limite definido. Estimativas `Unknown` e incomparáveis não são reportadas.

`BIG2001` reporta quando `complexity_analyzers.maximum_cyclomatic_complexity` está configurado e a Cyclomatic Complexity estrutural de um executable member suportado excede esse máximo. Ela é independente do Big-O e pode usar contabilização de `switch` standard ou Modified McCabe.

`BIG2002` reporta quando `complexity_analyzers.maximum_nesting_depth` está configurado e a Maximum Control-Flow Nesting Depth de um executable member suportado excede esse máximo. Código straight-line tem depth `0`; branches irmãos não acumulam; local functions, lambdas e anonymous methods aninhados são analisados independentemente.

`BIG2003`, `BIG2004` e `BIG2005` reportam quando seus thresholds de tamanho correspondentes estão configurados e um executable member suportado excede a política de NLOC, statement count ou token count. Essas são métricas de tamanho, não métricas de Big-O ou fluxo de controle.

`BIG2006` reporta quando `complexity_analyzers.maximum_parameters` está configurado e um executable member suportado declara mais parâmetros de fonte do que o permitido. Ele conta parâmetros declarados, incluindo o receiver `this` de extension methods, mas não type parameters, variáveis capturadas, `this` implícito de instância ou `value` implícito de accessors.

`BIG2007` reporta quando `complexity_analyzers.maximum_cognitive_complexity` está configurado e um executable member suportado excede a convenção C# documentada de Cognitive Complexity deste projeto. Código straight-line possui score `0`; quebras estruturais de fluxo de controle e nesting local aumentam o score.

`BIG9000` é um probe de infraestrutura usado para comprovar que o pacote do analyzer foi carregado e executado. Ele não representa uma recomendação de performance.

Veja o [Catálogo de Analyzers](docs/pt-BR/analyzers.md) para detalhes das regras.

## Modelo de análise

### Operações BCL e LINQ conhecidas

As operações conhecidas são mapeadas por identidade de símbolo do Roslyn. Métodos customizados chamados `Contains`, `Where`, `ToList` ou similares não são tratados como operações BCL/LINQ, a menos que o símbolo resolvido pertença ao subconjunto suportado.

Exemplos implementados incluem:

- `List<T>.Contains`, `List<T>.IndexOf`, `List<T>.Sort`, `List<T>.Count` e o indexer de `List<T>`.
- `Dictionary<TKey,TValue>.ContainsKey` e `Dictionary<TKey,TValue>.ContainsValue`.
- `HashSet<T>.Contains`.
- `Length` de arrays e strings.
- LINQ `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max` e `Aggregate`.
- Operações LINQ deferred como `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct` e `GroupBy`.

A criação de uma pipeline LINQ deferred não é contabilizada como uma enumeração completa. O custo de enumeração é considerado quando uma operação terminal suportada ou um `foreach` consome a pipeline.

### Análise interprocedural

Quando um caller invoca um método-fonte suportado declarado na mesma Roslyn `Compilation`, o analyzer pode derivar um template do callee independente do caller e substituir nesse template os argumentos usados na chamada.

Os métodos-fonte suportados precisam possuir dispatch seguro, como métodos static, private, não virtuais ou dispatch sealed quando o alvo de runtime pode ser comprovado. Operações BCL/LINQ conhecidas têm precedência sobre a análise de métodos-fonte.

O traversal é sob demanda e limitado. Um callee só é analisado quando é alcançado a partir do método raiz atual. O analyzer não pré-varre todas as syntax trees e não constrói um call graph da compilation inteira.

Exemplos:

```text
A -> B O(n)           => A O(n)
loop n -> B O(n)     => O(n^2)
loop n -> B O(m)     => O(n * m)
B(left) + B(right)   => O(n + m)
B(constant)          => O(1)
A -> B -> C O(log n) => O(log n)
```

Chamadas não suportadas, não resolvidas, inseguras, limitadas por budget, canceladas ou cíclicas permanecem `Unknown`.

### Recursão direta e resolução de recorrências

O analyzer reconhece chamadas diretamente recursivas por identidade de símbolo do Roslyn e exige evidência compatível de base case antes de resolver uma recorrência. Chamadas recursivas em branches mutuamente exclusivos são contabilizadas por caminho, de modo que código no estilo binary search permanece `O(log n)` em vez de ser superestimado como linear.

As famílias de recorrência suportadas incluem:

- formas de decremento/somatório como `T(n)=T(n-1)+1`, `T(n)=T(n-1)+n` e `T(n)=T(n-1)+log n`;
- recursão exponencial simples como `2T(n-1)+1` e Fibonacci `T(n-1)+T(n-2)+1`;
- formas do Master Theorem como `T(n)=T(n/2)+1`, `2T(n/2)+n`, `2T(n/2)+n^2` e `3T(n/2)+n`;
- um subconjunto restrito e limitado de Akra-Bazzi com termos recursivos apenas por escala e tolls polilogarítmicos, por exemplo `T(n)=T(n/3)+T(2n/3)+n`.

Potências polinomiais fracionárias são representadas de forma determinística; por exemplo, `3T(n/2)+n` reporta `O(n^1.585)`.

Base case ausente, argumentos não redutores, formatos de recorrência não suportados, trabalho local desconhecido, inconclusão numérica, cancelamento e recursão mútua permanecem `Unknown`.

## Build local

Pré-requisitos:

- .NET SDK `10.0.400` ou um SDK compatível selecionado por `global.json`.
- Um shell capaz de executar comandos `dotnet`.

A partir da raiz do repositório:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

O repositório também documenta criação e consumo local do pacote. Se você estiver trabalhando antes de uma versão estável estar disponível no NuGet.org, use o fluxo de pacote local em [Primeiros Passos](docs/pt-BR/getting-started.md).

Veja [Primeiros Passos](docs/pt-BR/getting-started.md).

## Configuração

O comportamento do analyzer pode ser configurado pelo Roslyn analyzer config. As severidades dos diagnósticos continuam usando as entradas padrão `dotnet_diagnostic.<RULE_ID>.severity`.

```ini
[*.cs]

complexity_analyzers.interprocedural_analysis = true
complexity_analyzers.recursion_analysis = true
complexity_analyzers.max_call_depth = 5
complexity_analyzers.max_methods_per_root = 32
complexity_analyzers.maximum_complexity = n_log_n
complexity_analyzers.maximum_cyclomatic_complexity = 10
complexity_analyzers.cyclomatic_complexity_mode = standard
complexity_analyzers.maximum_nesting_depth = 3
complexity_analyzers.maximum_method_nloc = 40
complexity_analyzers.maximum_statement_count = 25
complexity_analyzers.maximum_token_count = 300
complexity_analyzers.maximum_parameters = 5
complexity_analyzers.maximum_cognitive_complexity = 15

dotnet_diagnostic.BIG0001.severity = suggestion
dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
dotnet_diagnostic.BIG1005.severity = warning
dotnet_diagnostic.BIG1006.severity = warning
dotnet_diagnostic.BIG2001.severity = warning
dotnet_diagnostic.BIG2002.severity = warning
dotnet_diagnostic.BIG2003.severity = warning
dotnet_diagnostic.BIG2004.severity = warning
dotnet_diagnostic.BIG2005.severity = warning
dotnet_diagnostic.BIG2006.severity = warning
dotnet_diagnostic.BIG2007.severity = warning
dotnet_diagnostic.BIG9000.severity = none
```

Os valores padrão mantêm análise interprocedural e recursiva habilitadas, `max_call_depth` em `5`, `max_methods_per_root` em `32`, `maximum_complexity` como `none`, `maximum_cyclomatic_complexity` sem valor, `cyclomatic_complexity_mode` como `standard` e todos os thresholds de nesting/tamanho/parâmetros/cognitive sem valor. O threshold só é aplicado à métrica configurada.

Veja [Configuração](docs/pt-BR/configuration.md).

## Performance e compatibilidade

O analyzer foi projetado para permanecer limitado e adequado à execução dentro do compilador/IDE: sem acesso de rede, sem I/O de filesystem nos hot paths do analyzer, sem execução de processos, sem telemetria, sem varredura obrigatória da solution inteira, traversal limitado de métodos-fonte, resolução limitada de recorrências, execução concorrente, exclusão de código gerado e checks de cancellation.

O harness de performance reproduzível está documentado em [performance/README.md](performance/README.md). Ele valida comportamento estrutural e o reporting de execução de analyzers do compilador com `ReportAnalyzer=true`; o tempo decorrido é apenas informativo porque hardware e runners de CI variam.

O CI valida o consumo local do pacote em hosts com SDKs .NET 8, .NET 9 e .NET 10 para detectar regressões de carregamento e compatibilidade do analyzer.

## Arquitetura

O pacote é um analyzer de tempo de compilação, não uma biblioteca de runtime:

```text
código-fonte da aplicação
        |
        | compilado por
        v
compilador Roslyn / host da IDE
        |
        | carrega
        v
ComplexityAnalysis.Analyzers
```

O assembly do analyzer é empacotado em:

```text
analyzers/dotnet/cs/
```

Veja [Arquitetura](docs/pt-BR/architecture.md).

## Documentação

- [Primeiros Passos](docs/pt-BR/getting-started.md)
- [Catálogo de Analyzers](docs/pt-BR/analyzers.md)
- [Arquitetura](docs/pt-BR/architecture.md)
- [Configuração](docs/pt-BR/configuration.md)
- [Métricas Halstead para C#](docs/pt-BR/halstead-metrics.md)
- [Sample executável](samples/ComplexityAnalysis.Sample/README.md)
- [Governança de Qualidade de Release](docs/pt-BR/development/quality-gates.md)
- [Documentation in English](README.md)

## Limitações

- A análise de métodos-fonte é limitada a métodos ordinários com dispatch seguro dentro da mesma compilation.
- Não há call graph da compilation inteira nem da solution inteira.
- A resolução de recorrências é limitada às formas suportadas de recursão direta com evidência de base case.
- Recursão mútua é detectada, mas não resolvida.
- O suporte a Akra-Bazzi é um subconjunto restrito/limitado, não o teorema completo.
- Polinômios característicos gerais, integração numérica geral, MathNet, SymPy e projetos solver herdados não são utilizados.
- Não há `CodeFixProvider`.
- `Microsoft.CodeAnalysis.Workspaces` não é utilizado.
- Comportamento não suportado ou não comprovado resulta em `Unknown` em vez de estimativas inseguras.

## Licença

MIT, conforme a declaração de licença do pacote.

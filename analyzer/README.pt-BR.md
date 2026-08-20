# ComplexityAnalysis.Analyzers

[English](README.md) | Portugues (Brasil)

ComplexityAnalysis.Analyzers e um pacote Roslyn analyzer isolado para expor informacoes de complexidade algoritmica em builds e IDEs C#.

O analyzer e desenvolvido em `analyzer/` como uma fronteira de pacote separada dos projetos herdados de `complexity-hints`. Os projetos herdados podem servir como referencia, mas o pacote do analyzer nao possui `ProjectReference`, dependencia binaria ou dependencia de pacote local para eles.

## Estado Atual

Phase 1 ate Phase 7 estao implementadas.

| Phase | Estado | Entrega |
| --- | --- | --- |
| Phase 1 - Analyzer Foundation | Completa | Projeto analyzer isolado `netstandard2.0`, layout de pacote e probe de infraestrutura `BIG9000`. |
| Phase 2 - Complexity Model | Completa | Modelo Big-O sem Roslyn, formatacao deterministica, comparacao de crescimento, composicao, variaveis independentes e `Unknown`. |
| Phase 3 - Roslyn Extraction | Completa | Extracao intraprocedural de metodos a partir de sintaxe e semantica Roslyn. |
| Phase 4 - BCL, LINQ and Actionable Diagnostics | Completa | Mapeamentos semanticos de um subconjunto documentado de BCL/LINQ, `BIG0001` e diagnostics acionaveis `BIG100x`. |
| Phase 5 - Interprocedural Analysis | Completa | Propagacao limitada e sob demanda a partir de metodos fonte seguros na mesma compilation, diagnostic de chamada fonte em loop `BIG1004`, deteccao de ciclos, cache e limites internos. |
| Phase 6 - Recursion & Recurrence Solving | Completa | Extracao limitada de recursao direta, recorrencias de soma, recursao exponencial simples, Master Theorem, subconjunto restrito/limitado de Akra-Bazzi, potencias fracionarias e `BIG1005`. |
| Phase 7 - Configuration, Performance & NuGet Readiness | Completa | Opcoes de analyzer config, budgets publicos limitados, diagnostic de threshold configuravel `BIG1006`, harness de performance, testes de contrato do pacote, validacao local de consumidor e checks de compatibilidade em CI. |

O analyzer pode seguir metodos fonte suportados na mesma compilation quando o dispatch e seguro e a chamada e alcancada a partir do metodo raiz atual. Ele tambem pode resolver metodos diretamente recursivos selecionados quando evidencia de base case, reducao de argumento, trabalho local e formato da recorrencia sao comprovados. Ele nao cria call graph da compilation inteira, nao resolve recursao mutua e nao usa `Microsoft.CodeAnalysis.Workspaces`.

## Diagnostics

| ID | Titulo | Categoria | Severidade padrao | Habilitado por padrao |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | Nao |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | Sim |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | Sim |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | Sim |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | Sim |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | Sim |
| `BIG1006` | Method complexity exceeds configured threshold | `Complexity` | `Info` | Sim |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | Nao |

`BIG0001` e informational e desabilitado por padrao. Ele reporta uma estimativa conhecida de complexidade do metodo no identificador do metodo quando habilitado explicitamente.

`BIG1005` reporta metodos diretamente recursivos suportados cuja recorrencia resolvida e exponencial, como recursao estilo Fibonacci.

`BIG1006` reporta quando `complexity_analyzers.maximum_complexity` esta configurado e a estimativa conhecida e comparavel de um metodo excede esse threshold. Estimativas `Unknown` e incomparaveis nao reportam.

`BIG9000` e um probe de infraestrutura. Ele prova que o pacote do analyzer foi carregado e executado quando habilitado explicitamente; ele nao e uma recomendacao de performance.

Veja o [Catalogo de Analyzers](docs/pt-BR/analyzers.md).

## Analise Interprocedural

A Phase 5 adiciona analise interprocedural de metodos fonte: quando um caller invoca um metodo suportado declarado na mesma Roslyn `Compilation`, o analyzer pode analisar o callee uma vez como template independente do caller e substituir os argumentos do caller nesse template.

Metodos fonte suportados sao metodos C# ordinarios com dispatch seguro, incluindo metodos static, private, nao virtuais e dispatch sealed quando o alvo de runtime e comprovado. Operacoes conhecidas de BCL e LINQ mantem precedencia sobre analise de metodo fonte.

O traversal e sob demanda. Um callee e analisado apenas quando o metodo raiz atual alcanca aquela invocacao. O analyzer nao pre-varre todas as syntax trees e nao cria um call graph completo. Opcoes publicas expoem limites controlados para profundidade de chamada e metodos expandidos por analise raiz.

Chamadas nao suportadas, nao resolvidas, inseguras, limitadas por budget, canceladas ou ciclicas continuam `Unknown`. Recursao direta pode ser resolvida apenas pelo pipeline limitado de recorrencias. Recursao mutua e detectada, mas nao resolvida.

Exemplos:

```text
A -> B O(n)           => A O(n)
loop n -> B O(n)     => O(n^2)
loop n -> B O(m)     => O(n * m)
B(left) + B(right)   => O(n + m)
B(constant)          => O(1)
A -> B -> C O(log n) => O(log n)
```

## Recursao Direta e Recorrencias

O analyzer reconhece chamadas diretamente recursivas por identidade de simbolos Roslyn e exige evidencia compativel de base case antes de resolver. Chamadas recursivas em branches mutuamente exclusivos sao contadas por caminho, entao codigo estilo binary search com duas chamadas sintaticas em branches exclusivos continua `O(log n)`, nao `O(n)`.

Familias de recorrencia suportadas incluem:

- recorrencias de soma/decremento como `T(n)=T(n-1)+1`, `T(n)=T(n-1)+n` e `T(n)=T(n-1)+log n`;
- recursao direta exponencial simples como `2T(n-1)+1` e Fibonacci `T(n-1)+T(n-2)+1`;
- formas de Master Theorem como `T(n)=T(n/2)+1`, `2T(n/2)+n`, `2T(n/2)+n^2` e `3T(n/2)+n`;
- um subconjunto restrito/limitado de Akra-Bazzi com termos recursivos apenas por escala e toll polylogaritmico, por exemplo `T(n)=T(n/3)+T(2n/3)+n`.

Potencias polinomiais fracionarias sao representadas de forma deterministica, entao `3T(n/2)+n` reporta `O(n^1.585)`. Trabalho local desconhecido, base case ausente, argumentos nao redutores, formatos de recorrencia nao suportados, solucao numericamente inconclusiva, cancellation e recursao mutua continuam `Unknown`. O analyzer nao afirma suporte completo a Akra-Bazzi, solucao simbolica geral de recorrencias, deteccao de memoization ou prova geral de terminacao.

## Escopo de Operacoes Conhecidas

A Phase 4 mapeia operacoes selecionadas por simbolos Roslyn, nao apenas por nomes de metodos. Metodos customizados chamados `Contains`, `Where`, `ToList` ou similares continuam sem mapping, a menos que o simbolo resolvido faca parte do subconjunto suportado.

Exemplos implementados incluem:

- `List<T>.Contains`, `List<T>.IndexOf`, `List<T>.Sort`, `List<T>.Count` e indexer de `List<T>`.
- `Dictionary<TKey,TValue>.ContainsKey` e `Dictionary<TKey,TValue>.ContainsValue`.
- `HashSet<T>.Contains`.
- `Length` de array e string.
- LINQ `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max`, `Aggregate`.
- Operacoes LINQ deferred incluindo `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct` e `GroupBy`.

A criacao de uma pipeline LINQ deferred nao e cobrada como enumeracao completa. O custo de enumeracao e contado quando uma operacao terminal suportada ou um `foreach` consome a pipeline.

Operacoes nao suportadas ou nao resolvidas produzem `Unknown`. `Unknown` nao e tratado como `O(1)` ou `O(n)`, e nao e reportado por `BIG0001`.

## Quick Start

Pre-requisitos:

- .NET SDK `10.0.100` ou um SDK compativel selecionado por `analyzer/global.json`.
- Um shell capaz de executar comandos `dotnet`.

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

O pacote e documentado como build/fonte de pacote local. Nao assuma publicacao no NuGet.org sem evidencia independente.

Veja [Primeiros Passos](docs/pt-BR/getting-started.md).

## Configuracao

A Phase 7 adiciona opcoes customizadas de comportamento via Roslyn analyzer config. Severidades de diagnostics continuam usando `dotnet_diagnostic.<RULE_ID>.severity`.

```ini
[*.cs]

complexity_analyzers.interprocedural_analysis = true
complexity_analyzers.recursion_analysis = true
complexity_analyzers.max_call_depth = 5
complexity_analyzers.max_methods_per_root = 32
complexity_analyzers.maximum_complexity = n_log_n

dotnet_diagnostic.BIG0001.severity = suggestion
dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
dotnet_diagnostic.BIG1005.severity = warning
dotnet_diagnostic.BIG1006.severity = warning
dotnet_diagnostic.BIG9000.severity = none
```

Os defaults preservam o comportamento existente: analise interprocedural e analise de recursao ficam habilitadas, `max_call_depth` e `5`, `max_methods_per_root` e `32`, e `maximum_complexity` e `none`. O diagnostic de threshold so se aplica a estimativas conhecidas e comparaveis.

Veja [Configuracao](docs/pt-BR/configuration.md).

## Performance e Compatibilidade

O analyzer foi desenhado para ser limitado: sem acesso de rede, sem I/O de filesystem em hot paths do analyzer, sem execucao de processos, sem telemetria, sem varredura obrigatoria de solution inteira, traversal limitado de metodos fonte, solucao limitada de recorrencias, execucao concorrente, exclusao de codigo gerado e checks de cancellation.

O harness de performance repetivel esta documentado em [performance/README.md](performance/README.md). Ele valida comportamento estrutural e reporting oficial do compilador com `ReportAnalyzer=true`; tempo decorrido e informativo porque hardware e runners de CI variam.

O CI valida consumo local do pacote nos hosts SDK suportados pela matriz do projeto: .NET 8 LTS, .NET 9 STS e .NET 10 LTS. O pacote nao e publicado no NuGet.org por este workflow do repositorio.

## Arquitetura

O pacote e um analyzer de tempo de compilacao, nao uma biblioteca de runtime. Aplicacoes consumidoras nao chamam classes do analyzer em tempo de execucao.

```text
codigo-fonte da aplicacao
        |
        | compilado por
        v
compilador Roslyn / host de IDE
        |
        | carrega
        v
ComplexityAnalysis.Analyzers
```

O assembly do analyzer e empacotado em:

```text
analyzers/dotnet/cs/
```

Veja [Arquitetura](docs/pt-BR/architecture.md).

## Documentacao

- [Primeiros Passos](docs/pt-BR/getting-started.md)
- [Catalogo de Analyzers](docs/pt-BR/analyzers.md)
- [Arquitetura](docs/pt-BR/architecture.md)
- [Configuracao](docs/pt-BR/configuration.md)
- [Documentation in English](README.md)

## Limitacoes

- A analise de metodos fonte e limitada a metodos ordinarios com dispatch seguro na mesma compilation.
- Nao ha call graph de compilation inteira ou solution inteira.
- Solucao de recorrencias e limitada a formatos de recursao direta suportados com evidencia de base case.
- Recursao mutua e detectada, mas nao resolvida.
- Akra-Bazzi e apenas um subconjunto restrito/limitado de Akra-Bazzi, nao o teorema completo.
- Polinomios caracteristicos gerais, integracao numerica geral, MathNet, SymPy e projetos solver herdados nao sao usados.
- Nao ha `CodeFixProvider`.
- `Microsoft.CodeAnalysis.Workspaces` nao e usado.
- Comportamento nao suportado ou nao comprovado prefere `Unknown` em vez de palpites inseguros.

## Licenca

MIT, conforme a declaracao de licenca do repositorio.

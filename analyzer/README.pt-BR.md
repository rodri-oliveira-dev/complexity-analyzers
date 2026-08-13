# ComplexityAnalysis.Analyzers

[English](README.md) | Portugues (Brasil)

ComplexityAnalysis.Analyzers e um pacote Roslyn analyzer isolado para expor informacoes de complexidade algoritmica em builds e IDEs C#.

O analyzer e desenvolvido em `analyzer/` como uma fronteira de pacote separada dos projetos herdados de `complexity-hints`. Os projetos herdados podem servir como referencia, mas o pacote do analyzer nao possui `ProjectReference`, dependencia binaria ou dependencia de pacote local para eles.

## Estado Atual

Phase 1 ate Phase 4 estao implementadas.

| Phase | Estado | Entrega |
| --- | --- | --- |
| Phase 1 - Analyzer Foundation | Completa | Projeto analyzer isolado `netstandard2.0`, layout de pacote e probe de infraestrutura `BIG9000`. |
| Phase 2 - Complexity Model | Completa | Modelo Big-O sem Roslyn, formatacao deterministica, comparacao de crescimento, composicao, variaveis independentes e `Unknown`. |
| Phase 3 - Roslyn Extraction | Completa | Extracao intraprocedural de metodos a partir de sintaxe e semantica Roslyn. |
| Phase 4 - BCL, LINQ and Actionable Diagnostics | Em hardening | Mapeamentos semanticos de um subconjunto documentado de BCL/LINQ, `BIG0001` e diagnostics acionaveis `BIG100x`. |

O analyzer continua intraprocedural. Ele nao cria call graph, nao segue metodos locais do projeto, nao resolve recursao e nao usa `Microsoft.CodeAnalysis.Workspaces`.

## Diagnostics

| ID | Titulo | Categoria | Severidade padrao | Habilitado por padrao |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | Nao |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | Sim |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | Sim |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | Sim |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | Nao |

`BIG0001` e informational e desabilitado por padrao. Ele reporta uma estimativa conhecida de complexidade do metodo no identificador do metodo quando habilitado explicitamente.

`BIG9000` e um probe de infraestrutura. Ele prova que o pacote do analyzer foi carregado e executado quando habilitado explicitamente; ele nao e uma recomendacao de performance.

Veja o [Catalogo de Analyzers](docs/pt-BR/analyzers.md).

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
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.4.0-phase4-local
```

O pacote e documentado como build/fonte de pacote local. Nao assuma publicacao no NuGet.org sem evidencia independente.

Veja [Primeiros Passos](docs/pt-BR/getting-started.md).

## Configuracao

Diagnostics usam a configuracao padrao de severidade Roslyn via `.editorconfig`. Nao ha opcoes customizadas do analyzer na Phase 4.

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG9000.severity = none
```

Veja [Configuracao](docs/pt-BR/configuration.md).

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

- A analise e intraprocedural; nao ha call graph.
- Chamadas de metodos locais do projeto nao sao seguidas.
- Recursao e solucao de recorrencias nao sao suportadas.
- Master Theorem e Akra-Bazzi nao estao implementados no analyzer isolado.
- Nao ha `CodeFixProvider`.
- `Microsoft.CodeAnalysis.Workspaces` nao e usado.
- Comportamento nao suportado ou nao comprovado prefere `Unknown` em vez de palpites inseguros.

## Licenca

Use a licenca do repositorio que se aplica a este projeto.

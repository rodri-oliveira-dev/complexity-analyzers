# ComplexityAnalysis.Analyzers

[English](README.md) | Português (Brasil)

ComplexityAnalysis.Analyzers e um workspace isolado de Roslyn analyzer para transformar analise de complexidade algoritmica em informacao consumivel pelo compilador e pelo ecossistema de IDEs do .NET.

O projeto atualmente contem a fundacao do analyzer, um modelo interno de complexidade e extracao Roslyn intraprocedural ate a Phase 3. Ele ainda nao expoe diagnostics de produto que avisem sobre complexidade Big-O.

## Por Que Este Projeto Existe

O objetivo e construir um Roslyn analyzer de C# que, futuramente, consiga apresentar feedback util de complexidade algoritmica durante o desenvolvimento .NET normal: saida de build, diagnostics no editor, CI e consumo por pacote NuGet analyzer.

Este workspace nasceu tomando a implementacao herdada de `complexity-hints` como referencia conceitual:

```text
implementacao herdada
        |
        | fonte conceitual/de referencia
        v
ComplexityAnalysis.Analyzers
```

O novo analyzer e isolado. Ele nao possui `ProjectReference`, dependencia binaria ou dependencia de pacote local para os projetos herdados.

## Estado Atual

Phase 1, Phase 2 e Phase 3 estao implementadas.

| Phase | Estado | Entrega |
| --- | --- | --- |
| Phase 1 - Analyzer Foundation | Concluida | Workspace isolado do analyzer, projeto analyzer `netstandard2.0`, empacotamento NuGet analyzer e probe de infraestrutura `BIG9000`. |
| Phase 2 - Complexity Model | Concluida | Modelo interno Big-O sem Roslyn, comparacao de crescimento, composicao, variaveis independentes e `Unknown`. |
| Phase 3 - Roslyn Extraction | Concluida | Extracao intraprocedural de metodos a partir de sintaxe e semantica Roslyn para o modelo interno de complexidade. |

A fronteira importante e:

```text
Complexity Model / Roslyn Extraction
        |
        | implementados internamente
        X diagnostics de produto Big-O ainda nao estao conectados
        |
Camada de diagnostics
        `-- probe de infraestrutura BIG9000
```

## O Que Esta Implementado

A capacidade interna de analise inclui:

- Formas do modelo Big-O como `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`, `O(n^k)`, `O(b^n)`, `O(n!)` e `Unknown`.
- Composicao sequencial, aninhada e por ramificacao.
- Comparacao de crescimento para expressoes da mesma variavel e tratamento conservador de variaveis independentes.
- Extracao intraprocedural para corpos de metodos, metodos expression-bodied, operacoes basicas comprovadas, loops suportados e estruturas de ramificacao.
- Resultados conservadores `Unknown` para comportamento nao suportado ou nao comprovado.

Essa capacidade interna nao e a mesma coisa que um diagnostic visivel ao usuario. Os testes da Phase 3 validam valores `ComplexityExpression` extraidos; eles nao validam diagnostics publicos de Big-O.

## Diagnostics Atuais

Apenas um diagnostic esta exposto atualmente:

| ID | Titulo | Categoria | Severidade padrao | Habilitado por padrao | Finalidade |
| --- | --- | --- | --- | --- | --- |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | Nao | Prova que o pacote do analyzer foi carregado e executado. |

`BIG9000` nao e uma recomendacao de performance. Ele nao identifica codigo ineficiente, nao calcula Big-O e nao representa bug no projeto consumidor.

Diagnostics de produto baseados na complexidade Big-O extraida estao planejados para uma fase posterior.

Veja o [Catalogo de Analyzers](docs/pt-BR/analyzers.md).

## Quick Start

Os pre-requisitos sao derivados deste workspace:

- .NET SDK `10.0.100` ou um SDK compativel selecionado por `analyzer/global.json`.
- Um shell capaz de executar comandos `dotnet`.

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.3.0-docs-local
```

O pacote atualmente esta documentado como consumido a partir de build/fonte de pacote local. Nao assuma uma publicacao no NuGet.org sem evidencia independente.

Veja [Primeiros Passos](docs/pt-BR/getting-started.md).

## Configuracao

Diagnostics usam a configuracao padrao de severidade Roslyn via `.editorconfig`:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Para um smoke test, habilite explicitamente o probe de execucao:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Usar `warning` aqui altera a severidade configurada pelo consumidor para aumentar a visibilidade. `BIG9000` continua definido pelo analyzer como `Info` e desabilitado por padrao.

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

## Desenvolvimento

A solution isolada do analyzer e:

```text
analyzer/ComplexityAnalysis.Analyzers.slnx
```

Comandos comuns de validacao:

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

Arquivos fora de `analyzer/` pertencem a implementacao herdada e sao tratados como referencia apenas para este workspace.

## Documentacao

- [Primeiros Passos](docs/pt-BR/getting-started.md)
- [Catalogo de Analyzers](docs/pt-BR/analyzers.md)
- [Arquitetura](docs/pt-BR/architecture.md)
- [Configuracao](docs/pt-BR/configuration.md)
- [Documentation in English](README.md)

## Limitacoes Atuais

- Nenhum diagnostic de produto relata resultados Big-O ao usuario atualmente.
- Mapeamentos de complexidade de BCL e LINQ nao fazem parte da Phase 3.
- Chamadas de metodos nao sao resolvidas e geralmente produzem `Unknown`.
- A analise e intraprocedural; nao ha call graph.
- Recursao e solucao de recorrencias nao sao suportadas.
- Master Theorem e Akra-Bazzi nao estao implementados no analyzer isolado.
- Nao ha `CodeFixProvider`.
- `Microsoft.CodeAnalysis.Workspaces` nao e usado.
- Comportamento nao suportado ou nao comprovado prefere `Unknown` em vez de palpites inseguros.

`Unknown` significa que o analyzer nao conseguiu provar uma complexidade assintotica segura para a construcao. Ele nao significa `O(1)` e nao deve ser interpretado, por si so, como problema de performance.

## Roadmap / Proximo Passo

O handoff identifica o proximo passo como Phase 4: BCL, LINQ e diagnostics acionaveis. Esse trabalho nao foi implementado pela Phase 3.

## Relacao Com complexity-hints

O codigo herdado de `complexity-hints` permanece valioso como implementacao de referencia. Este workspace de analyzer mantem intencionalmente uma fronteira de produto separada para poder se tornar um pacote Roslyn analyzer focado, sem herdar dependencias de runtime ou uma arquitetura mais ampla antes da hora.

## Licenca

Use a licenca do repositorio que se aplica a este projeto.

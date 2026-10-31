---
title: GitHub Action
description: Analise a complexidade C# no CI do GitHub, compare regressões em PRs e aplique quality gates opcionais.
sidebar:
  order: 1
---

A **Complexity Analysis Action** disponibiliza a [CLI de projetos](/complexity-analyzers/pt-br/cli/) em workflows reutilizáveis do GitHub Actions. É indicada para obter evidências de complexidade em pull requests sem manter scripts próprios de instalação e relatórios da CLI.

A Action está em um [repositório separado](https://github.com/rodri-oliveira-dev/complexity-analysis-action) e reutiliza o mesmo motor de análise da CLI, do Roslyn Analyzer e do MCP. Ela **não** implementa outro modelo de complexidade.

:::note[Versão estável disponível]
A [Complexity Analysis Action `v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0) foi publicada em 30 de outubro de 2026. Para reproduzir o mesmo comportamento, utilize o SHA aprovado `2d807ef29ca03c685d224543a135d8c30dfe2c67` ou a tag publicada `@v1.0.0`. A referência móvel `@v1` **ainda não foi criada**.

A listagem pública no GitHub Marketplace não foi verificada de forma independente. Publicar uma GitHub Release não comprova, por si só, a presença no Marketplace.
:::

## Quando escolher a Action

| Seu fluxo de trabalho | Prefira |
| --- | --- |
| Feedback durante edição ou compilação | [Roslyn Analyzer](/complexity-analyzers/pt-br/analyzer/) |
| Scripts locais ou integração própria em CI | [CLI](/complexity-analyzers/pt-br/cli/) |
| Checks reutilizáveis no GitHub e regressões em PRs | **GitHub Action** |
| Análise somente leitura por agentes de IA compatíveis | [Servidor MCP](/complexity-analyzers/pt-br/mcp/) |

As quatro integrações atendem a necessidades distintas. A Action executa uma versão publicada e fixa da `ComplexityAnalysis.Tool`. Action, CLI, Analyzer e MCP têm ciclos de releases independentes.

## O que pode ser configurado

- **Quality gates:** limites opcionais de Big-O, complexidade ciclomática e cognitiva, aninhamento, NLOC por método e duplicação.
- **Regressões em PRs:** comparação entre snapshots da base e da revisão atual, com falha apenas para regressões comprovadas pela CLI.
- **Relatórios:** JSON, SARIF 2.1.0, GitHub Job Summary, upload opcional de Code Scanning e feedback resumido no PR.
- **Políticas distintas:** a violação de um limite absoluto é diferente de uma piora em relação à branch-base.

Evidências desconhecidas ou incomparáveis não são tratadas automaticamente como regressões. As estimativas de complexidade **não são benchmarks de desempenho em runtime**.

## Exemplo: análise de PR somente leitura

Crie `.github/workflows/complexity.yml` no seu repositório C#. O workflow já utiliza o SHA da versão publicada; substitua `MySolution.slnx` pelo caminho real do arquivo `.csproj`, `.sln` ou `.slnx`:

```yaml
name: Complexity review
on:
  pull_request:

permissions:
  contents: read

jobs:
  complexity:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
        with:
          persist-credentials: false
      - id: analysis
        uses: rodri-oliveira-dev/complexity-analysis-action@2d807ef29ca03c685d224543a135d8c30dfe2c67
        with:
          path: MySolution.slnx
          max-cyclomatic-complexity: '15'
          max-cognitive-complexity: '20'
          compare-with-base: 'true'
          fail-on-regression: 'true'
```

A referência da Action já é executável; é necessário apenas informar um caminho existente para o projeto ou solução. O exemplo usa **permissões somente de leitura**, sem publicar comentários ou enviar SARIF ao Code Scanning. Se o baseline estiver indisponível, isso não equivale a uma análise concluída sem regressões. Para preservar os relatórios, utilize `actions/upload-artifact` fixado em um SHA revisado e os [outputs documentados](/complexity-analyzers/pt-br/action/reference/).

Consulte também os [workflows mantidos na branch principal](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples). A tag `v1.0.0` preserva o snapshot original, cujos exemplos ainda contêm SHAs provisórios; os arquivos na `main` da Action já foram corrigidos.

## Requisitos e segurança

A `v1.0.0` publicada suporta **runners Ubuntu**, exige Bash, Python 3 e acesso ao NuGet.org e instala o SDK .NET `10.0.403`. Sua dependência CLI padrão é a versão exata publicada `1.0.1` de `ComplexityAnalysis.Tool`. Não há suporte anunciado para Windows ou macOS sem validação end-to-end equivalente.

Análises e comparações simples exigem somente `contents: read`. O upload de SARIF exige também `security-events: write`. Comentários diretos exigem `pull-requests: write`, que **não** deve ser exposto a jobs que analisam PRs não confiáveis. Para contribuições de forks, utilize análise isolada e somente leitura, com um workflow de publicação confiável separado.

O carregamento de projetos MSBuild pode executar lógica controlada pelo repositório. A Action não é um sandbox de processos; nunca execute código de PR não confiável com `pull_request_target`.

## Continue aprendendo

A [referência completa da Action](/complexity-analyzers/pt-br/action/reference/) descreve os inputs, outputs, estados de comparação e soluções de problemas. Para instalar e executar a CLI manualmente em um workflow, consulte o [guia GitHub Actions da CLI](/complexity-analyzers/pt-br/cli/github-actions/).

Comece pela [release publicada `v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0), consulte os [exemplos de workflow atualizados](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples) ou acesse o [repositório Complexity Analysis Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action).

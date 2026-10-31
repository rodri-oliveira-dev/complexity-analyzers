---
title: Integração com GitHub Actions
description: Execute a CLI manualmente no GitHub Actions, com versões fixas e quality gates explícitos.
sidebar:
  order: 2
---

Esta página demonstra o uso **manual da CLI**. Para verificações reutilizáveis
no CI do GitHub, a [Complexity Analysis Action](/complexity-analyzers/pt-br/action/)
já possui a [release `v1.0.0` publicada](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0).
Ela invoca a mesma CLI, mas tem versionamento independente. Para começar, consulte
as [instruções de instalação](https://github.com/rodri-oliveira-dev/complexity-analysis-action#quick-start) e os
[exemplos da versão](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples).

Use um manifesto local de ferramentas para que o workflow restaure uma versão revisada.

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.1
```

Versione `.config/dotnet-tools.json` e invoque a ferramenta na CI:

```yaml
name: Complexity report

on:
  pull_request:

permissions:
  contents: read

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
        with:
          persist-credentials: false
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68
        with:
          dotnet-version: '10.0.403'
      - run: dotnet tool restore
      - run: mkdir -p ./artifacts
      - run: >-
          dotnet complexity-analysis analyze ./App.slnx
          --format sarif
          --output ./artifacts/complexity.sarif
          --max-cyclomatic-complexity 10
```

O workflow utiliza commits revisados para fixar `actions/checkout` e
`actions/setup-dotnet`. Atualize essas referências de forma controlada quando
as dependências evoluírem. Adicione upload de artefatos se os relatórios precisarem
ser retidos ou enviados a um consumidor SARIF.

## CLI manual ou Action publicada?

| Integração | Quando usar | Configuração |
| --- | --- | --- |
| CLI manual | Execução local, orquestração própria ou controle explícito dos comandos. | Gerencie manifesto .NET, SDK, comandos e armazenamento dos relatórios. |
| [GitHub Action v1.0.0](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0) | Análise reutilizável no CI do GitHub, quality gates, relatórios e regressões em PRs. | Referencie a Action publicada com o caminho do projeto ou solução; ela instala a CLI fixada. |

Para utilizar a Action diretamente:

```yaml
name: Complexity Action
on:
  pull_request:

permissions:
  contents: read

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
        with:
          persist-credentials: false
      - name: Analyze complexity
        uses: rodri-oliveira-dev/complexity-analysis-action@2d807ef29ca03c685d224543a135d8c30dfe2c67
        with:
          path: App.slnx
```

Substitua `App.slnx` pelo caminho do projeto ou solução. A Action está fixada no
commit revisado da `v1.0.0`. A tag `@v1.0.0` também está disponível, mas a
referência móvel `@v1` ainda não foi criada. A versão padrão da CLI instalada
pela Action é `1.0.1`, independente da versão `v1.0.0` da própria Action.
A disponibilidade no Marketplace exige verificação independente da GitHub Release.

## Semântica dos gates

Gates são opt-in. Uma falha retorna código `2`, que naturalmente falha a etapa do shell. Evidências `Unknown` ou incompatíveis permanecem incomparáveis em vez de se tornarem regressões comprovadas.

Para comparar baselines, armazene um snapshot JSON revisado conforme a política do repositório e execute `complexity-analysis compare baseline.json current.json --fail-on-regression`.

---
title: Integração com GitHub Actions
description: Execute a CLI manualmente no GitHub Actions, com versões fixas e quality gates explícitos.
sidebar:
  order: 2
---

Esta página demonstra o uso **manual da CLI**. Para integrar relatórios e regressões em PRs por meio de uma Action reutilizável, consulte a [Complexity Analysis GitHub Action](/complexity-analyzers/pt-br/action/). A primeira release estável está planejada, mas ainda não foi publicada.

Use um manifesto local de ferramentas para que o workflow restaure uma versão revisada.

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.0
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
      - uses: actions/checkout@<immutable-commit-sha>
        with:
          persist-credentials: false
      - uses: actions/setup-dotnet@<immutable-commit-sha>
        with:
          dotnet-version: '10.0.x'
      - run: dotnet tool restore
      - run: mkdir -p ./artifacts
      - run: >-
          dotnet complexity-analysis analyze ./App.slnx
          --format sarif
          --output ./artifacts/complexity.sarif
          --max-cyclomatic-complexity 10
```

No workflow real, fixe ações de terceiros em SHAs imutáveis revisados. Adicione upload de artefato quando o relatório precisar ser retido ou enviado a um consumidor SARIF compatível.

## Semântica dos gates

Gates são opt-in. Uma falha retorna código `2`, que naturalmente falha a etapa do shell. Evidências `Unknown` ou incompatíveis permanecem incomparáveis em vez de se tornarem regressões comprovadas.

Para comparar baselines, armazene um snapshot JSON revisado conforme a política do repositório e execute `complexity-analysis compare baseline.json current.json --fail-on-regression`.

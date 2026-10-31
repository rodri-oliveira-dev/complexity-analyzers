---
title: Instalação rápida
description: Instale Roslyn Analyzer, CLI e MCP ou integre a GitHub Action separada.
sidebar:
  order: 2
---

Instale somente a superfície necessária ao seu fluxo. Os pacotes são independentes.

## Roslyn Analyzer

```bash
dotnet add package ComplexityAnalysis.Analyzers
```

O Roslyn carrega o analyzer durante builds e análise na IDE. A aplicação não o chama e não recebe dependência dele em runtime.

## CLI

A versão publicada de `ComplexityAnalysis.Tool` utilizada pela Action `v1.0.0` é `1.0.1`. Instale a CLI diretamente para scripts locais ou orquestração personalizada no CI:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.1
complexity-analysis --version
```

Para repositórios reproduzíveis, prefira um manifesto local e versione `.config/dotnet-tools.json`.

## GitHub Action

No GitHub CI, a [Complexity Analysis Action](/complexity-analyzers/pt-br/action/)
executa a CLI publicada sem exigir scripts próprios de instalação. Sua
[release estável `v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0) foi
publicada em 30 de outubro de 2026. Ela suporta runners Ubuntu, instala o SDK
.NET `10.0.403` e utiliza `ComplexityAnalysis.Tool` `1.0.1` por padrão.

Crie `.github/workflows/complexity.yml` no repositório que deseja analisar:

```yaml
name: Complexity Analysis
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
      - name: Analyze C# complexity
        uses: rodri-oliveira-dev/complexity-analysis-action@2d807ef29ca03c685d224543a135d8c30dfe2c67
        with:
          path: MySolution.slnx
```

Substitua `MySolution.slnx` pelo caminho real do arquivo `.csproj`, `.sln`
ou `.slnx`. O exemplo fixa o commit revisado da `v1.0.0` em um SHA completo
para maior reprodutibilidade. A tag publicada `@v1.0.0` também está
disponível; a referência móvel `@v1` **ainda não foi criada**. A análise
utiliza apenas a permissão `contents: read`; uploads ao Code Scanning e
comentários em PRs exigem permissões adicionais e os cuidados descritos na
referência técnica.

Consulte a [referência da Action](/complexity-analyzers/pt-br/action/reference/)
e os [exemplos de workflow mantidos](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples).
A presença no Marketplace não foi verificada de maneira independente da
publicação da GitHub Release. Action, CLI, Analyzer e MCP mantêm ciclos
de releases independentes.

## Servidor MCP

O comando documentado para `1.0.0` se aplica depois que essa versão estiver publicada no NuGet.org:

```bash
dotnet tool install --global ComplexityAnalysis.Mcp --version 1.0.0
complexity-mcp --version
```

Clientes MCP iniciam `complexity-mcp` por `stdio` com um limite de leitura explícito:

```bash
complexity-mcp --root /caminho/absoluto/para/o/projeto
```

:::caution
Gerar um pacote local comprova que ele pode ser produzido; não comprova que uma versão está publicada no NuGet.org. Use uma fonte explícita e uma versão somente local para testes do repositório.
:::

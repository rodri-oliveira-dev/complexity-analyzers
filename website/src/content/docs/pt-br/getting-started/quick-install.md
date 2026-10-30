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

O comando documentado para `1.0.0` se aplica depois que essa versão estiver publicada no NuGet.org:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.0
complexity-analysis --version
```

Para repositórios reproduzíveis, prefira um manifesto local e versione `.config/dotnet-tools.json`.

## GitHub Action

No GitHub CI, a [Complexity Analysis Action](/complexity-analyzers/pt-br/action/) executa a CLI publicada por você. A primeira `v1.0.0` e a referência `v1` **ainda não foram publicadas**; durante a avaliação, use um SHA completo de commit revisado, não uma tag inexistente.

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

---
title: Instalação rápida
description: Instale o Roslyn Analyzer, a CLI ou o servidor MCP por NuGet e ferramentas .NET.
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

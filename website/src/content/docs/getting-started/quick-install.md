---
title: Quick installation
description: Install the Roslyn Analyzer, CLI, or MCP server through NuGet and .NET tools.
sidebar:
  order: 2
---

Install only the surface your workflow needs. The packages are independent.

## Roslyn Analyzer

```bash
dotnet add package ComplexityAnalysis.Analyzers
```

Roslyn loads the analyzer during builds and IDE analysis. Your application does not call it and receives no analyzer runtime dependency.

## CLI

The documented `1.0.0` command applies after that version is published on NuGet.org:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.0
complexity-analysis --version
```

For reproducible repositories, prefer a local tool manifest and commit `.config/dotnet-tools.json`.

## MCP server

The documented `1.0.0` command applies after that version is published on NuGet.org:

```bash
dotnet tool install --global ComplexityAnalysis.Mcp --version 1.0.0
complexity-mcp --version
```

MCP clients start `complexity-mcp` over `stdio` with an explicit read boundary:

```bash
complexity-mcp --root /absolute/path/to/project
```

:::caution
Building a local package proves the package can be produced; it does not prove that a version is published on NuGet.org. Use an explicit local source and a local-only version for repository testing.
:::

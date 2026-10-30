---
title: Quick installation
description: Install the Roslyn Analyzer, CLI, and MCP, or integrate the separate GitHub Action.
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

## GitHub Action

For GitHub CI, the separately maintained [Complexity Analysis Action](/complexity-analyzers/action/) runs the published CLI for you. The first stable `v1.0.0` and `v1` references are **not yet published**; evaluate with a reviewed full commit SHA after checking the Action repository, rather than copying a nonexistent release tag.

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

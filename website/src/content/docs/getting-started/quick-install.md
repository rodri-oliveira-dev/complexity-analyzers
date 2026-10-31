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

The published `ComplexityAnalysis.Tool` version used by Action `v1.0.0` is `1.0.1`. Install the CLI directly for local scripts or custom CI orchestration:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.1
complexity-analysis --version
```

For reproducible repositories, prefer a local tool manifest and commit `.config/dotnet-tools.json`.

## GitHub Action

The separately maintained [Complexity Analysis Action](/complexity-analyzers/action/)
runs the published CLI for you in GitHub CI. Its
[stable `v1.0.0` release](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0) was
published on October 30, 2026. It supports Ubuntu runners, installs .NET SDK
`10.0.403`, and uses `ComplexityAnalysis.Tool` `1.0.1` by default.

Create `.github/workflows/complexity.yml` in the repository to be analyzed:

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

Replace `MySolution.slnx` with the actual `.csproj`, `.sln`, or `.slnx`
path. The example pins the reviewed `v1.0.0` commit to a full SHA for
reproducibility. The published `@v1.0.0` tag is also available; the moving
`@v1` alias has **not** been created yet. Analysis uses read-only
`contents: read` permissions; optional Code Scanning uploads and PR comments
require additional permissions and the security guidance in the full reference.

See the [Action reference](/complexity-analyzers/action/reference/) and
[maintained workflow examples](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples).
Marketplace listing visibility has not been independently verified by the
GitHub Release publication. The Action, CLI, Analyzer, and MCP retain
independent release versions.

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

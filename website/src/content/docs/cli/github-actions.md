---
title: GitHub Actions integration
description: Pin the CLI, generate artifacts, and enforce explicit complexity gates in CI.
sidebar:
  order: 2
---

Use a repository-local tool manifest so the workflow restores a reviewed version.

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.0
```

Commit `.config/dotnet-tools.json`, then invoke the tool in CI:

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

Pin third-party actions to reviewed immutable commit SHAs in the real workflow. Add an artifact upload step when the report must be retained or sent to a compatible SARIF consumer.

## Gate semantics

Gates are opt-in. A gate failure returns exit code `2`, which naturally fails the shell step. `Unknown` and incompatible snapshot evidence remain incomparable rather than being promoted to a proven regression.

For baseline comparison, store a reviewed JSON snapshot using your repository's artifact policy and run `complexity-analysis compare baseline.json current.json --fail-on-regression`.

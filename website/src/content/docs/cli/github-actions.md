---
title: GitHub Actions integration
description: Run the Complexity Analysis CLI manually in GitHub Actions, with pinned tools and explicit quality gates.
sidebar:
  order: 2
---

This page shows a **manual CLI workflow**. For reusable GitHub CI checks,
the [Complexity Analysis Action](/complexity-analyzers/action/)
is available in its [published `v1.0.0` release](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0).
It invokes the same CLI, but its release versions are independent. For adoption,
see the [installation guide](https://github.com/rodri-oliveira-dev/complexity-analysis-action#quick-start) and the
[versioned examples](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples).

Use a repository-local tool manifest so the workflow restores a reviewed version.

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.1
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

The workflow uses reviewed immutable commit pins for `actions/checkout` and
`actions/setup-dotnet`. Update these pins deliberately as dependencies evolve.
Add an artifact upload step when reports must be retained or sent to a SARIF
consumer.

## Manual CLI or published Action?

| Integration | Prefer when | Setup |
| --- | --- | --- |
| Manual CLI | You need local runs, custom orchestration, or explicit control over CLI steps. | Manage a .NET tool manifest, SDK setup, invocation, and report retention. |
| [GitHub Action v1.0.0](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0) | You want reusable GitHub CI analysis, quality gates, reports, and PR regression checks. | Use the published Action with a project or solution path; it installs the pinned CLI. |

For a minimal Action-based workflow, use:

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

Replace `App.slnx` with your project's actual path. The Action is pinned to
the reviewed `v1.0.0` commit; `@v1.0.0` is also available, but the moving
`@v1` alias has not yet been created. The Action defaults to CLI version
`1.0.1`, independently of its own `v1.0.0` version. Marketplace listing
visibility must be verified separately from GitHub Release publication.

## Gate semantics

Gates are opt-in. A gate failure returns exit code `2`, which naturally fails the shell step. `Unknown` and incompatible snapshot evidence remain incomparable rather than being promoted to a proven regression.

For baseline comparison, store a reviewed JSON snapshot using your repository's artifact policy and run `complexity-analysis compare baseline.json current.json --fail-on-regression`.

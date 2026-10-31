---
title: GitHub Action
description: Analyze C# complexity in GitHub CI, compare pull-request regressions, and apply opt-in quality gates.
sidebar:
  order: 1
---

The **Complexity Analysis Action** brings the published [project-level CLI](/complexity-analyzers/cli/) into reusable GitHub Actions workflows. It is useful when you want complexity evidence during a pull request without maintaining your own CLI setup and reporting scripts.

The Action lives in a [separate repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action) and uses the same analysis engine as the CLI, Roslyn Analyzer, and MCP server. It does **not** implement a second complexity model.

:::note[Stable release available]
[Complexity Analysis Action `v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0) was published on October 30, 2026. Use the reviewed release commit SHA `2d807ef29ca03c685d224543a135d8c30dfe2c67` for reproducible workflows, or the published `@v1.0.0` tag. The moving `@v1` alias has **not** been created yet.

The public GitHub Marketplace listing has not been independently verified. A GitHub Release does not itself confirm Marketplace visibility.
:::

## When to choose the Action

| Your workflow | Prefer |
| --- | --- |
| Live feedback in the editor or compiler | [Roslyn Analyzer](/complexity-analyzers/analyzer/) |
| Local scripts or custom CI orchestration | [CLI](/complexity-analyzers/cli/) |
| Reusable GitHub CI checks and PR regression feedback | **GitHub Action** |
| Read-only analysis from compatible AI agents | [MCP Server](/complexity-analyzers/mcp/) |

All four integrations serve different workflows. The Action invokes a pinned published version of `ComplexityAnalysis.Tool`; the Action, CLI, Analyzer, and MCP have independent release versions.

## What you can configure

- **Quality gates:** optional Big-O, cyclomatic and cognitive complexity, nesting depth, method NLOC, and duplication thresholds.
- **PR regressions:** compare baseline and current snapshots, and fail only on regressions that the CLI can prove.
- **Reporting:** JSON, SARIF 2.1.0, GitHub Job Summary, optional Code Scanning upload and bounded PR feedback.
- **Failure policy:** keep an absolute threshold violation separate from a change relative to the PR base.

Unknown or incomparable evidence is not automatically treated as a regression, and complexity estimates are **not runtime performance benchmarks**.

## Example: read-only PR analysis

Create `.github/workflows/complexity.yml` in your C# repository. The workflow pins the published release commit; replace `MySolution.slnx` with your `.csproj`, `.sln`, or `.slnx` path:

```yaml
name: Complexity review
on:
  pull_request:

permissions:
  contents: read

jobs:
  complexity:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
        with:
          persist-credentials: false
      - id: analysis
        uses: rodri-oliveira-dev/complexity-analysis-action@2d807ef29ca03c685d224543a135d8c30dfe2c67
        with:
          path: MySolution.slnx
          max-cyclomatic-complexity: '15'
          max-cognitive-complexity: '20'
          compare-with-base: 'true'
          fail-on-regression: 'true'
```

The Action reference is executable as shown once you supply an existing project or solution path. The sample keeps permissions **read-only** and does not post comments or upload SARIF to Code Scanning. If the baseline is unavailable, the comparison is not equivalent to a successful "no regressions" result. To retain reports, add a SHA-pinned `actions/upload-artifact` step using the [documented outputs](/complexity-analyzers/action/reference/).

For additional configurations, see the [maintained workflow examples](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples). The release tag `v1.0.0` preserves the original snapshot, whose example files contain pre-publication SHA placeholders; the examples on the Action's `main` branch have been corrected.

## Requirements and security

The published `v1.0.0` supports **Ubuntu runners**, requires Bash, Python 3, and access to NuGet.org, and installs .NET SDK `10.0.403`. Its default exact CLI dependency is `ComplexityAnalysis.Tool` `1.0.1`. Windows and macOS support are not claimed without equivalent end-to-end evidence.

Simple analysis and comparisons need only `contents: read`. SARIF upload additionally requires `security-events: write`. Direct comments require `pull-requests: write`, which should **not** be exposed to untrusted PR analysis. For fork contributions, use isolated read-only analysis and a separate trusted publishing workflow.

Loading MSBuild projects can execute project-controlled logic. The Action is not a process sandbox; never run untrusted PR code using `pull_request_target`.

## Continue learning

For every supported input, output, comparison status, and troubleshooting scenario, see the [full Action reference](/complexity-analyzers/action/reference/). For a workflow that installs the CLI manually instead, see the [CLI GitHub Actions guide](/complexity-analyzers/cli/github-actions/).

Start from the [published `v1.0.0` release](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0), explore the [up-to-date workflow examples](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples), or consult the [Complexity Analysis Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action).

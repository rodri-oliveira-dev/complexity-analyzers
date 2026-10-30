---
title: GitHub Action
description: Analyze C# complexity in GitHub CI, compare pull-request regressions, and apply opt-in quality gates.
sidebar:
  order: 1
---

The **Complexity Analysis Action** brings the published [project-level CLI](/complexity-analyzers/cli/) into reusable GitHub Actions workflows. It is useful when you want complexity evidence during a pull request without maintaining your own CLI setup and reporting scripts.

The Action lives in a [separate repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action) and uses the same analysis engine as the CLI, Roslyn Analyzer, and MCP server. It does **not** implement a second complexity model.

:::caution[Release status]
The first stable Action release, `v1.0.0`, is **planned but not yet published** at the time of this update. Do not use `@v1` or `@v1.0.0` until those tags exist. The GitHub Marketplace listing is not yet available. For evaluation, pin a **reviewed full commit SHA** from the [Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action) and check its current status.
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

Create `.github/workflows/complexity.yml` in your C# repository. Replace the placeholder with a **real reviewed 40-character commit SHA**, and the solution name with your own path:

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
      - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd
        with:
          persist-credentials: false
      - id: analysis
        uses: rodri-oliveira-dev/complexity-analysis-action@REVIEWED_FULL_40_CHARACTER_SHA
        with:
          path: MySolution.slnx
          max-cyclomatic-complexity: '15'
          max-cognitive-complexity: '20'
          compare-with-base: 'true'
          fail-on-regression: 'true'
```

This is an **illustrative template, not directly executable** while the SHA remains a placeholder. The sample intentionally uses read-only permissions and does not post comments. To retain reports, add an artifact-upload step after analysis using the documented output paths.

## Requirements and security

The current release candidate supports **Ubuntu runners**, requires access to NuGet.org, and installs .NET SDK 10. Its default published CLI dependency is `ComplexityAnalysis.Tool` `1.0.1`. Windows and macOS support are not claimed without equivalent end-to-end evidence.

Simple analysis and comparisons need only `contents: read`. SARIF upload additionally requires `security-events: write`. Direct comments require `pull-requests: write`, which should **not** be exposed to untrusted PR analysis. For fork contributions, use isolated read-only analysis and a separate trusted publishing workflow.

Loading MSBuild projects can execute project-controlled logic. The Action is not a process sandbox; never run untrusted PR code using `pull_request_target`.

## Continue learning

For every supported input, output, comparison status, and troubleshooting scenario, see the [full Action reference](/complexity-analyzers/action/reference/). For a workflow that installs the CLI manually instead, see the [CLI GitHub Actions guide](/complexity-analyzers/cli/github-actions/).

Source and detailed examples: [Complexity Analysis Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action).

---
title: Choose Analyzer, CLI, MCP, or Action
description: Choose the right C# complexity integration for IDE, automation, GitHub CI, or AI agents.
sidebar:
  order: 4
---

## Choose the Analyzer when

- developers need feedback while editing or compiling;
- policy belongs in `.editorconfig` and standard Roslyn diagnostics;
- zero runtime integration is important.

## Choose the CLI when

- you need whole-project or solution reports from local scripts, terminals, or custom CI;
- you want to manage CLI installation, snapshot comparison, and artifact handling yourself;
- you need JSON/SARIF output, duplicate detection, regression analysis, or opt-in quality gates outside a reusable GitHub Action.

## Choose the GitHub Action when

- your repository uses GitHub Actions and needs reusable checks on supported Ubuntu runners;
- you want opt-in quality gates, JSON/SARIF reports, and pull-request regression comparison without maintaining CLI setup scripts;
- you prefer the Action to install a fixed, published CLI version and integrate results into GitHub workflow summaries.

The separately versioned [Complexity Analysis Action `v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0)
was published on October 30, 2026. It uses `ComplexityAnalysis.Tool` `1.0.1` by default;
the Analyzer, CLI, MCP, and Action retain independent version numbers.

For reproducibility, use the verified release commit in your workflow:
`rodri-oliveira-dev/complexity-analysis-action@2d807ef29ca03c685d224543a135d8c30dfe2c67`.
The published `@v1.0.0` tag is also available; the moving `@v1` alias
has **not** been created yet. See the [Action guide](/complexity-analyzers/action/)
and [published workflow examples](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples). GitHub Release publication
does not by itself confirm Marketplace listing visibility.

## Choose MCP when

- a compatible local agent should discover analysis tools;
- the agent needs exact evidence for a stable member identity;
- project reads must stay inside a configured root with bounded time, file, and byte limits.

## Combine them deliberately

A common adoption path uses the Analyzer for immediate feedback, the CLI for local reports, the GitHub Action for reusable CI checks, and MCP for investigation. The Action invokes the published CLI rather than reimplementing analysis. Keep each product's version pin and workflow permissions explicit.

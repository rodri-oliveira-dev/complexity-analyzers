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

- you need whole-project or solution reports;
- CI must consume JSON or SARIF;
- snapshots, regression comparison, duplicate detection, or opt-in quality gates are required.

## Choose the GitHub Action when

- your repository runs on GitHub Actions and needs reusable CI checks;
- you want configured quality gates, SARIF, and PR regression comparisons without maintaining custom scripts;
- you can use the currently supported Ubuntu runner and follow the Action's pre-release SHA guidance.

[Explore the Action](/complexity-analyzers/action/). The first stable `v1.0.0` is planned, not yet published.

## Choose MCP when

- a compatible local agent should discover analysis tools;
- the agent needs exact evidence for a stable member identity;
- project reads must stay inside a configured root with bounded time, file, and byte limits.

## Combine them deliberately

A common adoption path uses the Analyzer for immediate feedback, the CLI for local reports, the GitHub Action for reusable CI checks, and MCP for investigation. The Action invokes the CLI rather than reimplementing analysis. Keep all version pins and permissions explicit.

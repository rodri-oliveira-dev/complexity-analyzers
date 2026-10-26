---
title: Choose Analyzer, CLI, or MCP
description: Select the Complexity Analyzers product that fits IDE, CI, reporting, or agent workflows.
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

## Choose MCP when

- a compatible local agent should discover analysis tools;
- the agent needs exact evidence for a stable member identity;
- project reads must stay inside a configured root with bounded time, file, and byte limits.

## Combine them deliberately

A common adoption path is Analyzer for immediate feedback, CLI for aggregate governance, and MCP for investigation. Installing one product does not install the others. Keep each configuration explicit and pin CLI/MCP versions in automation.

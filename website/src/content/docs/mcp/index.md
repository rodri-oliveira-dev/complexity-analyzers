---
title: Local MCP server
description: Expose read-only complexity analysis tools to compatible local AI agents over stdio.
sidebar:
  order: 1
---

`ComplexityAnalysis.Mcp` is a separately packaged .NET 10 tool. It exposes analysis capabilities to compatible clients over local `stdio`; it is not an autonomous agent and never edits source code.

## Tools

- `analyze_project` analyzes a `.csproj`, `.sln`, or `.slnx` below the configured root and returns a full or bounded summary response.
- `query_member_evidence` retrieves exact evidence for a stable member identity, optionally reusing an immutable cached analysis by `analysisId`.
- `compare_snapshots` compares deterministic JSON snapshots and classifies regressions, improvements, equivalent results, and incomparable evidence.

## Operational boundary

The host is local, stdio-only, and read-only. It has no HTTP transport, shell execution, write tools, telemetry, or analysis-time network calls. It rejects paths outside `--root`, traversal through links/reparse points, unsupported inputs, and work beyond configured time, byte, and file limits.

See the [client and agent workflow guide](./agent-workflows/) or the synchronized [complete MCP reference](./reference/).

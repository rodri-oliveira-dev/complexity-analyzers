---
title: Ecosystem overview
description: Understand the Analyzer, CLI, and MCP surfaces and the evidence they share.
sidebar:
  order: 1
---

Complexity Analyzers is a C# analysis ecosystem with three independently installed products. They share an analysis model, but meet you at different points in the development workflow.

| Product | Best fit | Runs when | Primary output |
| --- | --- | --- | --- |
| `ComplexityAnalysis.Analyzers` | IDE and build feedback | Roslyn compilation | `BIG*` diagnostics |
| `ComplexityAnalysis.Tool` | Review, automation, and CI | Explicit CLI invocation | Console, JSON, or SARIF reports |
| `ComplexityAnalysis.Mcp` | Local AI-agent workflows | MCP client invokes a tool | Structured analysis and evidence |

## What the ecosystem analyzes

The shared engine estimates Big-O for a documented subset of C#, recognizes selected BCL and LINQ operations, follows safe source calls within bounded budgets, and solves selected direct-recursion families. It also measures structural metrics such as cyclomatic and cognitive complexity, nesting depth, method size, and parameter count.

The analysis is static and conservative. It does not execute the program, profile production traffic, or promise a result for every valid C# method. When the available syntax and semantic evidence are insufficient, the result is `Unknown`.

## Requirements

- The analyzer package targets `netstandard2.0` and is consumed by compatible Roslyn hosts.
- The CLI and MCP server are .NET 10 tools and require the .NET 10 runtime.
- Project and solution analysis supports `.csproj`, `.sln`, and `.slnx` inputs where documented.

Continue with [quick installation](/complexity-analyzers/getting-started/quick-install/) or [choose the right product](/complexity-analyzers/getting-started/choose-product/).

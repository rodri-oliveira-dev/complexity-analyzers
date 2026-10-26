---
title: Roslyn Analyzer
description: Add conservative complexity diagnostics to C# builds and IDE analysis.
sidebar:
  order: 1
---

`ComplexityAnalysis.Analyzers` runs inside Roslyn during compilation and IDE analysis. It targets `netstandard2.0`, adds no runtime dependency to consumer applications, and reports only within its documented static-analysis boundaries.

## Integration model

Install the package as a development-time analyzer. Supported hosts load its assembly from the package's `analyzers/dotnet/cs/` path. Application code never calls the analyzer.

The analyzer enables concurrent execution where safe, explicitly skips generated code, respects cancellation, and avoids network or hot-path file I/O.

## Diagnostic families

- `BIG0001` is an opt-in known complexity estimate.
- `BIG1001`–`BIG1006` cover costly patterns, exponential recursion, and a configured Big-O threshold.
- `BIG2001`–`BIG2007` cover cyclomatic complexity, nesting, method size, parameter count, and cognitive complexity thresholds.
- `BIG9000` is a disabled infrastructure probe, not a performance recommendation.

Continue with [installation and compatibility](./installation/), the [diagnostic catalog](./diagnostics/), or [.editorconfig configuration](./configuration/).

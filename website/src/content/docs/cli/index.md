---
title: Project-level CLI
description: Generate deterministic console, JSON, and SARIF reports for C# projects and solutions.
sidebar:
  order: 1
---

`ComplexityAnalysis.Tool` is a separately versioned .NET 10 tool. It analyzes `.csproj`, `.sln`, and `.slnx` inputs on explicit invocation and can produce aggregate reports without changing analyzer behavior inside builds.

```bash
complexity-analysis analyze MySolution.slnx
complexity-analysis analyze MyProject.csproj --format json --output report.json
complexity-analysis analyze MyProject.csproj --format sarif --output report.sarif
```

## Capabilities

- deterministic console, JSON, and SARIF 2.1.0 reports;
- snapshots and comparison without requiring Git;
- opt-in gates for Big-O, structural, size, parameter, and duplication measures;
- normalized C# duplicate-code detection;
- explicit exclusions and stable exit codes.

No threshold fails a build by default. Exit code `0` means success and configured gates passed, `1` means invalid input/loading/report failure, `2` means an enabled gate failed, and `130` means cancellation.

Use the complete [CLI reference](./reference/) for exact arguments and contracts, and the [GitHub Actions guide](./github-actions/) for CI integration.

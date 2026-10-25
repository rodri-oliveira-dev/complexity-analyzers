# ComplexityAnalysis.Tool

[Português (Brasil)](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/src/ComplexityAnalysis.Tool/README.pt-BR.md)

`ComplexityAnalysis.Tool` is a deterministic .NET tool for analyzing algorithmic
and structural complexity across C# projects and solutions. Use it for local
review, machine-readable reports, snapshot regression checks, duplicate-code
detection, and CI quality gates.

The products in this repository serve different integration points:

- `ComplexityAnalysis.Analyzers` runs inside Roslyn during builds and IDE analysis.
- `ComplexityAnalysis.Tool` is the `complexity-analysis` command for project- and
  solution-level reports and gates.
- `ComplexityAnalysis.Mcp` is a separate local, read-only MCP host. It is not a
  dependency of the tool or analyzer packages.

## Requirements

.NET 10 runtime is required. The .NET 10 SDK is recommended for local-tool
manifests and CI usage.

## Install From NuGet.org

After version `1.0.0` is published, install it globally:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.0
complexity-analysis --version
```

Or pin it in a repository-local tool manifest:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.0
dotnet tool restore
dotnet tool run complexity-analysis --version
```

Update or remove a global installation with:

```bash
dotnet tool update --global ComplexityAnalysis.Tool --version <version>
dotnet tool uninstall --global ComplexityAnalysis.Tool
```

For a local manifest, omit `--global` and run the command with either
`dotnet tool run complexity-analysis` or `dotnet complexity-analysis`.

Published-package commands use NuGet.org. A package built from source is not a
published release: install it only with an explicit local source and a local
version, for example `--add-source ./artifacts/local-packages --version
0.0.0-local`.

## Quick Start

```bash
complexity-analysis analyze MySolution.slnx
complexity-analysis analyze MyProject.csproj --format json
complexity-analysis analyze MyProject.csproj --format sarif
complexity-analysis analyze MyProject.csproj --max-cyclomatic-complexity 10
```

Console is the default format. JSON is intended for automation and snapshots;
SARIF 2.1.0 integrates with compatible code-scanning systems. Use `--output
<path>` to write any report to a file.

## Quality Gates And Exit Codes

Gates are opt-in; no default threshold fails a build. Available gates cover
Big-O, cyclomatic and cognitive complexity, nesting depth, NLOC, statement and
token counts, parameters, duplicate rate, and duplicate tokens.

- `0`: command succeeded and configured gates passed.
- `1`: invalid command, input/loading failure, or report-writing failure.
- `2`: an explicitly configured quality gate failed.
- `130`: operation canceled.

## Snapshots And Regression Comparison

Create deterministic JSON snapshots and compare them without Git or an external
service:

```bash
complexity-analysis analyze MyProject.csproj --format json --snapshot --output baseline.json
complexity-analysis analyze MyProject.csproj --format json --snapshot --output current.json
complexity-analysis compare baseline.json current.json --format json --fail-on-regression
```

The regression gate returns `2` only for proven regressions. Unknown or
incompatible evidence remains explicitly incomparable.

## Duplicate Detection

```bash
complexity-analysis analyze MyProject.csproj \
  --detect-duplicates \
  --min-duplicate-tokens 40 \
  --max-duplicate-rate 5
```

Detection is opt-in and C#-aware. Token and line minimums select candidates;
maximum duplicate rate or token count enables gates.

## Command Reference

Use `complexity-analysis --help`, `complexity-analysis analyze --help`, and
`complexity-analysis compare --help` for the installed version's exact options.
The complete reference, defaults, restrictions, report contracts, snapshot
semantics, duplicate normalization, and CI examples are in the
[project tooling guide](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/docs/en/project-tooling.md).

## Project

- [Repository](https://github.com/rodri-oliveira-dev/complexity-analyzers)
- [Contributing](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CONTRIBUTING.md)
- [MIT License](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/LICENSE)

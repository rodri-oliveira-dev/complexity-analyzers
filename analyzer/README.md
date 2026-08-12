# ComplexityAnalysis.Analyzers

English | [Português (Brasil)](README.pt-BR.md)

ComplexityAnalysis.Analyzers is an isolated Roslyn analyzer workspace for turning algorithmic-complexity analysis into information that can be consumed by the .NET compiler and IDE ecosystem.

The project currently contains the analyzer foundation, an internal complexity model, and intraprocedural Roslyn extraction through Phase 3. It does not yet expose product diagnostics that warn about Big-O complexity.

## Why this project exists

The goal is to build a C# Roslyn analyzer that can eventually surface useful algorithmic-complexity feedback during normal .NET development: build output, editor diagnostics, CI, and NuGet analyzer consumption.

This workspace was created with the inherited `complexity-hints` implementation as a conceptual reference:

```text
inherited implementation
        |
        | conceptual/reference source
        v
ComplexityAnalysis.Analyzers
```

The new analyzer is isolated. It has no `ProjectReference`, binary dependency, or local package dependency on the inherited projects.

## Current Status

Phase 1, Phase 2, and Phase 3 are implemented.

| Phase | Status | Delivered |
| --- | --- | --- |
| Phase 1 - Analyzer Foundation | Complete | Isolated analyzer workspace, `netstandard2.0` analyzer project, NuGet analyzer packaging, and `BIG9000` infrastructure probe. |
| Phase 2 - Complexity Model | Complete | Roslyn-free internal Big-O expression model, growth comparison, composition, independent variables, and `Unknown`. |
| Phase 3 - Roslyn Extraction | Complete | Intraprocedural method extraction from Roslyn syntax and semantics into the internal complexity model. |

The important boundary is:

```text
Complexity Model / Roslyn Extraction
        |
        | implemented internally
        X product Big-O diagnostics are not wired yet
        |
Diagnostic layer
        `-- BIG9000 infrastructure probe
```

## What Is Implemented

Internal analysis capability includes:

- Big-O model forms such as `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`, `O(n^k)`, `O(b^n)`, `O(n!)`, and `Unknown`.
- Sequential, nested, and branching composition.
- Growth comparison for same-variable expressions and conservative handling of independent variables.
- Intraprocedural extraction for method bodies, expression-bodied methods, proven basic operations, supported loops, and branching constructs.
- Conservative `Unknown` results for unsupported or unproven behavior.

This internal capability is not the same as a user-facing diagnostic. Phase 3 tests assert extracted `ComplexityExpression` values; they do not assert public Big-O diagnostics.

## Current Diagnostics

Only one diagnostic is currently exposed:

| ID | Title | Category | Default severity | Enabled by default | Purpose |
| --- | --- | --- | --- | --- | --- |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | No | Proves the analyzer package loaded and ran. |

`BIG9000` is not a performance recommendation. It does not identify inefficient code, does not calculate Big-O, and does not represent a bug in the consumer project.

Product diagnostics based on extracted Big-O complexity are planned for a later phase.

See [Analyzer Catalog](docs/en/analyzers.md).

## Quick Start

Prerequisites are derived from this workspace:

- .NET SDK `10.0.100` or a compatible SDK selected by `analyzer/global.json`.
- A shell capable of running `dotnet` commands.

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.3.0-docs-local
```

The package is currently documented as a local build/package source. Do not assume a NuGet.org release unless one exists independently.

See [Getting Started](docs/en/getting-started.md).

## Configuration

Diagnostics use standard Roslyn `.editorconfig` severity configuration:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

For a smoke test, enable the execution probe explicitly:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Using `warning` here changes the consumer-configured severity for visibility. `BIG9000` is still defined by the analyzer as `Info` and disabled by default.

See [Configuration](docs/en/configuration.md).

## Architecture

The package is a compile-time analyzer package, not a runtime library. Consumer applications do not call analyzer classes at runtime.

```text
application source
        |
        | compiled by
        v
Roslyn compiler / IDE host
        |
        | loads
        v
ComplexityAnalysis.Analyzers
```

The analyzer assembly is packed under:

```text
analyzers/dotnet/cs/
```

See [Architecture](docs/en/architecture.md).

## Development

The isolated analyzer solution is:

```text
analyzer/ComplexityAnalysis.Analyzers.slnx
```

Common validation commands:

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

Files outside `analyzer/` belong to the inherited implementation and are treated as reference-only for this workspace.

## Documentation

- [Getting Started](docs/en/getting-started.md)
- [Analyzer Catalog](docs/en/analyzers.md)
- [Architecture](docs/en/architecture.md)
- [Configuration](docs/en/configuration.md)
- [Documentação em português](README.pt-BR.md)

## Current Limitations

- No product diagnostic currently reports Big-O results to users.
- BCL and LINQ complexity mappings are not part of Phase 3.
- Method calls are not resolved and generally produce `Unknown`.
- Analysis is intraprocedural; there is no call graph.
- Recursion and recurrence solving are not supported.
- Master Theorem and Akra-Bazzi are not implemented in the isolated analyzer.
- No `CodeFixProvider` is included.
- `Microsoft.CodeAnalysis.Workspaces` is not used.
- Unsupported or unproven behavior prefers `Unknown` over unsafe guesses.

`Unknown` means the analyzer could not prove a safe asymptotic complexity for the construct. It does not mean `O(1)` and it should not be interpreted as a performance problem by itself.

## Roadmap / Next Step

The handoff identifies the next step as Phase 4: BCL, LINQ, and actionable diagnostics. That work has not been implemented by Phase 3.

## Relationship With complexity-hints

The inherited `complexity-hints` code remains valuable as a reference implementation. This analyzer workspace intentionally keeps a separate product boundary so it can become a focused Roslyn analyzer package without inheriting runtime dependencies or broader architecture prematurely.

## License

Use the repository license that applies to this project.

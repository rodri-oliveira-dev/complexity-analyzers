# ComplexityAnalysis.Analyzers

English | [Portugues (Brasil)](README.pt-BR.md)

ComplexityAnalysis.Analyzers is an isolated Roslyn analyzer package for surfacing algorithmic-complexity information in C# builds and IDEs.

The analyzer is developed under `analyzer/` as a package boundary separate from the inherited `complexity-hints` projects. Inherited projects may be used as reference material, but the analyzer package has no `ProjectReference`, binary dependency, or local package dependency on them.

## Current Status

Phase 1 through Phase 4 are implemented.

| Phase | Status | Delivered |
| --- | --- | --- |
| Phase 1 - Analyzer Foundation | Complete | Isolated `netstandard2.0` analyzer project, package layout, and `BIG9000` infrastructure probe. |
| Phase 2 - Complexity Model | Complete | Roslyn-free Big-O expression model, deterministic formatting, growth comparison, composition, independent variables, and `Unknown`. |
| Phase 3 - Roslyn Extraction | Complete | Intraprocedural method extraction from Roslyn syntax and semantics. |
| Phase 4 - BCL, LINQ, and Actionable Diagnostics | In hardening | Semantic known-operation mappings for a documented BCL/LINQ subset, `BIG0001`, and actionable `BIG100x` diagnostics. |

The analyzer remains intraprocedural. It does not build a call graph, follow project-local methods, solve recursion, or use `Microsoft.CodeAnalysis.Workspaces`.

## Diagnostics

| ID | Title | Category | Default severity | Enabled by default |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | No |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | Yes |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | Yes |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | Yes |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | No |

`BIG0001` is informational and disabled by default. It reports a known method complexity estimate at the method identifier when explicitly enabled.

`BIG9000` is an infrastructure probe. It proves the analyzer package loaded and ran when explicitly enabled; it is not a performance recommendation.

See [Analyzer Catalog](docs/en/analyzers.md).

## Known Operation Scope

Phase 4 maps selected operations by Roslyn symbols, not by method names alone. Custom methods named `Contains`, `Where`, `ToList`, or similar remain unmapped unless their resolved symbol is part of the supported subset.

Implemented examples include:

- `List<T>.Contains`, `List<T>.IndexOf`, `List<T>.Sort`, `List<T>.Count`, and `List<T>` indexer.
- `Dictionary<TKey,TValue>.ContainsKey` and `Dictionary<TKey,TValue>.ContainsValue`.
- `HashSet<T>.Contains`.
- Array and string `Length`.
- LINQ `Any`, `All`, `Contains`, `Count`, `LongCount`, `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`, `Sum`, `Min`, `Max`, `Aggregate`.
- Deferred LINQ pipeline operations including `Where`, `Select`, `SelectMany`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Distinct`, and `GroupBy`.

Deferred LINQ creation is not charged as a full enumeration. Enumeration cost is counted when a supported terminal operation or `foreach` consumes the pipeline.

Unsupported or unresolved operations produce `Unknown`. `Unknown` is not treated as `O(1)` or `O(n)`, and it is not reported by `BIG0001`.

## Quick Start

Prerequisites:

- .NET SDK `10.0.100` or a compatible SDK selected by `analyzer/global.json`.
- A shell capable of running `dotnet` commands.

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.4.0-phase4-local
```

The package is documented as a local build/package source. Do not assume a NuGet.org release unless one exists independently.

See [Getting Started](docs/en/getting-started.md).

## Configuration

Diagnostics use standard Roslyn `.editorconfig` severity configuration. There are no custom analyzer options in Phase 4.

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG9000.severity = none
```

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

## Documentation

- [Getting Started](docs/en/getting-started.md)
- [Analyzer Catalog](docs/en/analyzers.md)
- [Architecture](docs/en/architecture.md)
- [Configuration](docs/en/configuration.md)
- [Documentacao em portugues](README.pt-BR.md)

## Limitations

- Analysis is intraprocedural; there is no call graph.
- Project-local method calls are not followed.
- Recursion and recurrence solving are not supported.
- Master Theorem and Akra-Bazzi are not implemented in the isolated analyzer.
- No `CodeFixProvider` is included.
- `Microsoft.CodeAnalysis.Workspaces` is not used.
- Unsupported or unproven behavior prefers `Unknown` over unsafe guesses.

## License

Use the repository license that applies to this project.

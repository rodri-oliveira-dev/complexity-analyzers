# Configuration

[English](configuration.md) | [Portugues (Brasil)](../pt-BR/configuration.md)

`ComplexityAnalysis.Analyzers` uses standard Roslyn diagnostic severity configuration. Phase 6 does not define custom analyzer options.

## .editorconfig Format

Use the standard rule-specific format:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Common values include:

```text
none
silent
suggestion
warning
error
default
```

The compiler and SDK determine the exact build behavior for each configured severity.

## Defaults

| ID | Default severity | Enabled by default |
| --- | --- | --- |
| `BIG0001` | `Info` | `false` |
| `BIG1001` | `Info` | `true` |
| `BIG1002` | `Info` | `true` |
| `BIG1003` | `Info` | `true` |
| `BIG1004` | `Info` | `true` |
| `BIG1005` | `Info` | `true` |
| `BIG9000` | `Info` | `false` |

## Recommended Local Visibility

`BIG0001` is informational and disabled by default. Enable it when you want the analyzer to display known method estimates:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Promote actionable diagnostics when you want them visible in builds:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
dotnet_diagnostic.BIG1005.severity = warning
```

Keep the infrastructure probe disabled in normal projects:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Enable the probe only for package-consumption or CI smoke tests:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

## Disable Rules

Use `none` for any rule you do not want reported:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = none
dotnet_diagnostic.BIG1001.severity = none
dotnet_diagnostic.BIG1002.severity = none
dotnet_diagnostic.BIG1003.severity = none
dotnet_diagnostic.BIG1004.severity = none
dotnet_diagnostic.BIG1005.severity = none
dotnet_diagnostic.BIG9000.severity = none
```

## What Is Not Configurable

Phase 6 does not expose options for:

- Big-O thresholds;
- custom operation mappings;
- changing BCL or LINQ mapping behavior;
- recurrence-family selection, theorem thresholds, or numerical tolerances;
- call graph analysis;
- interprocedural call-depth or method-budget limits;
- full Akra-Bazzi, mutual recursion solving, or general symbolic recurrence solving;
- code fixes;
- memory, parallel, or probabilistic complexity.

Unsupported or unresolved operations remain `Unknown`; there is no option that converts them into a known complexity class.

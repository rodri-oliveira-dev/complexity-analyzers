# Configuration

[English](configuration.md) | [Português (Brasil)](../pt-BR/configuration.md)

`ComplexityAnalysis.Analyzers` uses two configuration layers:

- `complexity_analyzers.*` options control analyzer behavior.
- `dotnet_diagnostic.<RULE_ID>.severity` controls Roslyn diagnostic severity.

Behavior options are read through Roslyn analyzer config APIs. The analyzer does not parse `.editorconfig` files manually. Options can be set globally or per file; when both are present, the file-specific value wins for that syntax tree.

Invalid values are safe: they do not fail the build or report an analyzer failure. The analyzer falls back to the documented default for that option.

## Analyzer options

| Option | Type | Default | Allowed values | Purpose |
| --- | --- | --- | --- | --- |
| `complexity_analyzers.interprocedural_analysis` | Boolean | `true` | `true`, `false` | Enables supported source-method expansion in the same compilation. |
| `complexity_analyzers.recursion_analysis` | Boolean | `true` | `true`, `false` | Enables supported direct-recursion extraction and recurrence solving. |
| `complexity_analyzers.max_call_depth` | Integer | `5` | `0` through `16` | Caps source-method expansion depth. |
| `complexity_analyzers.max_methods_per_root` | Integer | `32` | `0` through `128` | Caps uncached source-method expansions per root method analysis. |
| `complexity_analyzers.maximum_complexity` | String | `none` | `none`, `constant`, `log_n`, `n`, `n_log_n`, `n2`, `n3`, `exponential`, `factorial` | Enables `BIG1006` when a known comparable method estimate exceeds this threshold. |
| `complexity_analyzers.maximum_cyclomatic_complexity` | Integer | unset | Positive base-10 integer | Enables `BIG2001` when a supported executable member's Cyclomatic Complexity exceeds this threshold. |
| `complexity_analyzers.cyclomatic_complexity_mode` | String | `standard` | `standard`, `modified_mccabe` | Selects switch accounting for Cyclomatic Complexity. |

Boolean values are case-insensitive after trimming surrounding whitespace. Integer values must be base-10 non-negative integers with no sign, decimal point, separators, or embedded whitespace. Threshold values are case-sensitive.

Values outside the public budget limits fall back to the default: `max_call_depth = 5` and `max_methods_per_root = 32`. Invalid cyclomatic thresholds fall back to unset. Invalid cyclomatic modes fall back to `standard`.

## Example

```ini
[*.cs]

complexity_analyzers.interprocedural_analysis = true
complexity_analyzers.recursion_analysis = true
complexity_analyzers.max_call_depth = 5
complexity_analyzers.max_methods_per_root = 32
complexity_analyzers.maximum_complexity = n_log_n
complexity_analyzers.maximum_cyclomatic_complexity = 10
complexity_analyzers.cyclomatic_complexity_mode = standard

dotnet_diagnostic.BIG1006.severity = warning
dotnet_diagnostic.BIG2001.severity = warning
```

## Threshold behavior

`complexity_analyzers.maximum_complexity` is opt-in. The default `none` means `BIG1006` does not report.

`BIG1006` reports only when all of these are true:

- the method complexity is known;
- the configured threshold is not `none`;
- the estimate and threshold are comparable by the analyzer's current model;
- the estimate is greater than the threshold.

`Unknown` complexity does not produce a threshold report. Incomparable multivariate complexity such as an expression over independent variables may also produce no threshold report. `BIG1006` is a practical analyzer signal, not a universal mathematical proof.

Examples:

| Actual estimate | Threshold | Result |
| --- | --- | --- |
| `O(n^2)` | `n_log_n` | Reports `BIG1006`. |
| `O(n log n)` | `n_log_n` | No report. |
| `O(n)` | `n_log_n` | No report. |
| `Unknown` | `n` | No report. |
| Incomparable multivariate expression | `n2` | No report. |

## Cyclomatic complexity behavior

`complexity_analyzers.maximum_cyclomatic_complexity` is opt-in. When it is unset
or invalid, `BIG2001` does not report. Valid values are positive base-10
integers. Equality does not report; only a strictly greater actual value reports.

Cyclomatic Complexity is structural path complexity, not algorithmic time
complexity. A member can be `O(n)` and also have Cyclomatic Complexity `12`.
`BIG2001` does not affect `BIG0001`, `BIG1006`, interprocedural analysis,
recursion analysis, or `maximum_complexity`.

The standard convention uses baseline `1 + decision points` and counts
documented decision constructs such as `if`, loops, `catch`, `?:`, short-circuit
`&&`/`||`, switch cases/arms, guarded discard switch expression arms, `when`
guards, and `or` patterns. In `modified_mccabe` mode, each switch statement or
switch expression contributes one decision for the switch family instead of one
per non-default case/arm; guards and `or` patterns are still counted separately.

Examples:

| Actual value | Threshold | Result |
| --- | --- | --- |
| `9` | `10` | No report. |
| `10` | `10` | No report. |
| `11` | `10` | Reports `BIG2001`. |

## Feature flags

`complexity_analyzers.interprocedural_analysis = false` prevents expansion into supported source callees. Intraprocedural analysis and supported BCL/LINQ operation analysis remain active.

`complexity_analyzers.recursion_analysis = false` prevents direct-recursion recurrence extraction and solving, including `BIG1005`. Non-recursive intraprocedural analysis and supported non-recursive source-method expansion can still run when interprocedural analysis is enabled.

## Analysis budgets

The analyzer is bounded by public options and hard limits:

- default call depth: `5`;
- maximum configurable call depth: `16`;
- default source methods per root: `32`;
- maximum configurable source methods per root: `128`.

At a budget boundary, affected calls remain conservative, usually `Unknown`. A small budget is useful for smoke tests or very cautious consumers, but it can reduce `BIG0001`, `BIG1004`, and `BIG1006` coverage for source-call-heavy code.

## Diagnostic severity

Use the standard Roslyn rule-specific format:

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

The compiler and SDK determine the exact build behavior for each severity.

## Diagnostic defaults

| ID | Default severity | Enabled by default |
| --- | --- | --- |
| `BIG0001` | `Info` | `false` |
| `BIG1001` | `Info` | `true` |
| `BIG1002` | `Info` | `true` |
| `BIG1003` | `Info` | `true` |
| `BIG1004` | `Info` | `true` |
| `BIG1005` | `Info` | `true` |
| `BIG1006` | `Info` | `true` |
| `BIG2001` | `Info` | `true` |
| `BIG9000` | `Info` | `false` |

`BIG1006` is enabled by default as a descriptor, but it is functionally inactive until `complexity_analyzers.maximum_complexity` is set to a concrete threshold.

`BIG2001` is enabled by default as a descriptor, but it is functionally inactive until `complexity_analyzers.maximum_cyclomatic_complexity` is set to a concrete threshold.

## Common settings

Enable method estimates:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Promote actionable diagnostics and threshold checks:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
dotnet_diagnostic.BIG1005.severity = warning
complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
complexity_analyzers.maximum_cyclomatic_complexity = 10
complexity_analyzers.cyclomatic_complexity_mode = modified_mccabe
dotnet_diagnostic.BIG2001.severity = warning
```

Temporarily prove package loading:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Keep the infrastructure probe disabled in normal projects:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## What is not configurable

The analyzer does not expose options for custom operation mappings, custom cyclomatic decision-point rules beyond the documented switch mode, BCL/LINQ mapping behavior, recurrence-family selection, theorem tolerances, whole-solution analysis, code fixes, memory complexity, parallel complexity, or probabilistic complexity.

Unsupported or unresolved operations remain `Unknown`; there is no option that converts them into a known complexity class.

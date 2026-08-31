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
| `complexity_analyzers.maximum_nesting_depth` | Integer | unset | Non-negative base-10 integer | Enables `BIG2002` when a supported executable member's Maximum Control-Flow Nesting Depth exceeds this threshold. |
| `complexity_analyzers.maximum_method_nloc` | Integer | unset | Non-negative base-10 integer | Enables `BIG2003` when a supported executable member's NLOC exceeds this threshold. |
| `complexity_analyzers.maximum_statement_count` | Integer | unset | Non-negative base-10 integer | Enables `BIG2004` when a supported executable member's statement count exceeds this threshold. |
| `complexity_analyzers.maximum_token_count` | Integer | unset | Non-negative base-10 integer | Enables `BIG2005` when a supported executable member's token count exceeds this threshold. |
| `complexity_analyzers.maximum_parameters` | Integer | unset | Non-negative base-10 integer | Enables `BIG2006` when a supported executable member's source-declared parameter count exceeds this threshold. |
| `complexity_analyzers.maximum_cognitive_complexity` | Integer | unset | Non-negative base-10 integer | Enables `BIG2007` when a supported executable member's Cognitive Complexity exceeds this threshold. |

Boolean values are case-insensitive after trimming surrounding whitespace. Integer values must be base-10 non-negative integers with no sign, decimal point, separators, or embedded whitespace. Threshold values are case-sensitive.

Values outside the public budget limits fall back to the default: `max_call_depth = 5` and `max_methods_per_root = 32`. Invalid cyclomatic, nesting, NLOC, statement-count, token-count, parameter-count, and cognitive-complexity thresholds fall back to unset. Invalid cyclomatic modes fall back to `standard`.

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
complexity_analyzers.maximum_nesting_depth = 3
complexity_analyzers.maximum_method_nloc = 40
complexity_analyzers.maximum_statement_count = 25
complexity_analyzers.maximum_token_count = 300
complexity_analyzers.maximum_parameters = 5
complexity_analyzers.maximum_cognitive_complexity = 15

dotnet_diagnostic.BIG1006.severity = warning
dotnet_diagnostic.BIG2001.severity = warning
dotnet_diagnostic.BIG2002.severity = warning
dotnet_diagnostic.BIG2003.severity = warning
dotnet_diagnostic.BIG2004.severity = warning
dotnet_diagnostic.BIG2005.severity = warning
dotnet_diagnostic.BIG2006.severity = warning
dotnet_diagnostic.BIG2007.severity = warning
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

## Maximum nesting depth behavior

`complexity_analyzers.maximum_nesting_depth` is opt-in. When it is unset or
invalid, `BIG2002` does not report. Valid values are non-negative base-10
integers. Equality does not report; only a strictly greater actual value reports.

Maximum Control-Flow Nesting Depth measures the deepest nested control-flow
structure inside one executable member. Straight-line code has depth `0`, a
single `if` or loop has depth `1`, and truly nested constructs increase the
depth. Sibling branches do not accumulate.

The nesting convention counts `if`, loops, `switch` statements, switch
expressions, `try`, and conditional `?:` expressions as one nesting level.
`else`, `else if`, cases, switch arms, catches, `finally`, boolean chains,
patterns, guards, plain blocks, initializers, `lock`, `using`, `fixed`,
`checked`, and `unchecked` do not add a level by themselves. Nested local
functions, lambdas, and anonymous methods are analyzed as independent executable
members rather than inflating their lexical parent.

Examples:

| Actual value | Threshold | Result |
| --- | --- | --- |
| `2` | `3` | No report. |
| `3` | `3` | No report. |
| `4` | `3` | Reports `BIG2002`. |

## Method-size behavior

The method-size thresholds are opt-in and independent:

- `complexity_analyzers.maximum_method_nloc` enables `BIG2003`;
- `complexity_analyzers.maximum_statement_count` enables `BIG2004`;
- `complexity_analyzers.maximum_token_count` enables `BIG2005`.

When a threshold is unset or invalid, its diagnostic does not report. Valid
values are non-negative base-10 integers. Equality does not report; only a
strictly greater actual value reports.

NLOC counts source lines inside the executable body that contain at least one
owned code token, excluding blank lines, comment-only lines, brace-only lines,
and delimiter-only lines. Declaration/header lines outside the executable body
do not participate. For expression-bodied members, only the expression body
participates.

Statement count counts structural C# statements in the executable body. Blocks
are containers, not counted statements. Expression-bodied members count as one
synthetic expression statement. Nested local functions, lambdas, and anonymous
methods are measured independently; a local-function declaration counts as one
parent statement, but its body does not inflate the parent.

Token count counts non-missing C# syntax tokens owned by the executable body,
including punctuation and delimiters, while excluding trivia, whitespace, and
comments. For expression-bodied members, only expression tokens participate.

Examples:

| Metric | Actual value | Threshold | Result |
| --- | --- | --- | --- |
| NLOC | `9` | `10` | No report. |
| NLOC | `10` | `10` | No report. |
| NLOC | `11` | `10` | Reports `BIG2003`. |
| Statement count | `26` | `25` | Reports `BIG2004`. |
| Token count | `301` | `300` | Reports `BIG2005`. |

## Parameter count behavior

`complexity_analyzers.maximum_parameters` is opt-in. When it is unset or
invalid, `BIG2006` does not report. Valid values are non-negative base-10
integers. Equality does not report; only a strictly greater actual value reports.

Parameter Count counts source-declared parameters on supported executable
members. Extension-method `this` receivers count because they are explicit
source parameters. Optional/defaulted, `params`, `ref`, `in`, and `out`
parameters each count once.

Generic type parameters, generic constraints, captured variables, implicit
instance `this`, compiler-generated parameters, and implicit accessor `value`
parameters do not count. Supported indexer accessors count explicit indexer
parameters and still exclude implicit setter/init `value`. Primary constructors
are deferred by the current executable-member support matrix.

Examples:

| Actual value | Threshold | Result |
| --- | --- | --- |
| `4` | `5` | No report. |
| `5` | `5` | No report. |
| `6` | `5` | Reports `BIG2006`. |
| `1` | `0` | Reports `BIG2006`. |

## Cognitive complexity behavior

`complexity_analyzers.maximum_cognitive_complexity` is opt-in. When it is unset
or invalid, `BIG2007` does not report. Valid values are non-negative base-10
integers. Equality does not report; only a strictly greater actual value reports.

Cognitive Complexity uses this project's documented C# convention. Straight-line
code starts at `0`. Structural control-flow breaks add `1 + current nesting`;
`else` adds `1`; boolean and pattern logical sequences add sequence/change cost;
direct self-recursion adds one point once per member when proven by symbol
identity; and `break`, `continue`, and `goto` add one point each. Nested local
functions, lambdas, and anonymous methods are scored independently.

See [Cognitive Complexity Convention](cognitive-complexity.md) for the complete
scoring table and worked examples.

Examples:

| Actual value | Threshold | Result |
| --- | --- | --- |
| `14` | `15` | No report. |
| `15` | `15` | No report. |
| `16` | `15` | Reports `BIG2007`. |
| `1` | `0` | Reports `BIG2007`. |

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
| `BIG2002` | `Info` | `true` |
| `BIG2003` | `Info` | `true` |
| `BIG2004` | `Info` | `true` |
| `BIG2005` | `Info` | `true` |
| `BIG2006` | `Info` | `true` |
| `BIG2007` | `Info` | `true` |
| `BIG9000` | `Info` | `false` |

`BIG1006` is enabled by default as a descriptor, but it is functionally inactive until `complexity_analyzers.maximum_complexity` is set to a concrete threshold.

`BIG2001` is enabled by default as a descriptor, but it is functionally inactive until `complexity_analyzers.maximum_cyclomatic_complexity` is set to a concrete threshold.

`BIG2002` is enabled by default as a descriptor, but it is functionally inactive until `complexity_analyzers.maximum_nesting_depth` is set to a concrete threshold.

`BIG2003`, `BIG2004`, and `BIG2005` are enabled by default as descriptors, but each remains functionally inactive until its matching method-size threshold is set.

`BIG2006` is enabled by default as a descriptor, but it remains functionally inactive until `complexity_analyzers.maximum_parameters` is set.

`BIG2007` is enabled by default as a descriptor, but it remains functionally inactive until `complexity_analyzers.maximum_cognitive_complexity` is set.

There is no public Halstead threshold option. Halstead metrics are currently an
internal capability for future reporting/tooling, and no single derived
Halstead value has been adopted as an evidence-backed maintainability threshold.

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
complexity_analyzers.maximum_nesting_depth = 3
dotnet_diagnostic.BIG2002.severity = warning
complexity_analyzers.maximum_method_nloc = 40
dotnet_diagnostic.BIG2003.severity = warning
complexity_analyzers.maximum_statement_count = 25
dotnet_diagnostic.BIG2004.severity = warning
complexity_analyzers.maximum_token_count = 300
dotnet_diagnostic.BIG2005.severity = warning
complexity_analyzers.maximum_parameters = 5
dotnet_diagnostic.BIG2006.severity = warning
complexity_analyzers.maximum_cognitive_complexity = 15
dotnet_diagnostic.BIG2007.severity = warning
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

The analyzer does not expose options for custom operation mappings, custom cyclomatic decision-point rules beyond the documented switch mode, custom method-size counting conventions, custom parameter-count conventions, custom Cognitive Complexity scoring conventions, custom Halstead classification or Halstead thresholds, BCL/LINQ mapping behavior, recurrence-family selection, theorem tolerances, whole-solution analysis, code fixes, memory complexity, parallel complexity, or probabilistic complexity.

Unsupported or unresolved operations remain `Unknown`; there is no option that converts them into a known complexity class.

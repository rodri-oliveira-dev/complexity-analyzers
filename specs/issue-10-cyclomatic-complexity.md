# Issue #10 SDD - Cyclomatic Complexity And Modified McCabe

## Specification

Issue #10 adds a deterministic structural control-flow metric for supported C#
executable members. The metric is independent from Big-O, interprocedural
analysis, recursion, recurrence solving, and `maximum_complexity`.

Cyclomatic Complexity is defined as:

```text
1 + decision points
```

The baseline value `1` applies to a supported executable member with an explicit
block or expression body. Declarations without executable bodies do not produce a
cyclomatic threshold diagnostic.

Decision points are counted inside the member's own executable body only.
Nested local functions, lambdas, and anonymous methods are analyzed as
independent executable roots through the shared executable-member abstraction.

The adopted standard convention counts these C# decision points:

- `if`: `+1` for each `if`; `else if` contributes through its nested `if`;
- loops: `for`, `foreach`, deconstruction `foreach`, `while`, and `do`: `+1`;
- `catch`: `+1` per catch clause;
- catch filters: `+1` for a `catch (...) when (...)` filter;
- conditional expressions: `+1` for each `?:`;
- boolean short-circuit expressions: `+1` per `&&` or `||` binary expression;
- switch statements in standard mode: `+1` per non-default `case` label,
  including pattern case labels;
- switch expressions in standard mode: `+1` per non-discard arm;
- switch/case/switch-arm guards: `+1` per `when` guard;
- pattern alternatives: `+1` per `or` pattern.

Constructs not listed above are not decision points for this issue. In
particular, plain `else`, `try`, `finally`, lexical blocks, object/collection
initializers, `??`, `?.`, `is` without an `or` pattern, pattern `and`, and
pattern `not` do not add points by themselves.

Modified McCabe mode changes only the switch-family convention:

- a switch statement contributes `+1` when it has at least one non-default
  `case` label;
- a switch expression contributes `+1` when it has at least one non-discard arm;
- `when` guards and `or` patterns are still counted separately because they add
  control-flow alternatives beyond the switch dispatch convention.

Examples:

```text
straight-line body                  => 1
if                                  => 2
if with a && b                      => 3
if / else if / else                 => 3
for containing if                   => 3
switch with two cases and default   => 3 standard, 2 modified
switch arm with when guard          => arm decision + guard decision
```

Threshold behavior is opt-in through:

```ini
complexity_analyzers.maximum_cyclomatic_complexity = 10
```

The default is unset. Missing or invalid values produce no threshold diagnostic.
Valid values are positive base-10 integers. The comparison is strictly greater:
actual values below or equal to the configured maximum do not report.

Switch accounting is configurable through:

```ini
complexity_analyzers.cyclomatic_complexity_mode = standard
complexity_analyzers.cyclomatic_complexity_mode = modified_mccabe
```

Invalid mode values fall back to `standard`.

The public diagnostic is `BIG2001`, category `Complexity`, default severity
`Info`, enabled by default as a descriptor, and functionally inactive until
`maximum_cyclomatic_complexity` is configured. Diagnostics point to the analyzed
executable member's stable diagnostic location and include actual value,
configured maximum, and mode in deterministic diagnostic properties.

## Discovery

#8 is closed by PR #49 and introduced `ExecutableMember` / `ExecutableMemberBody`
for ordinary methods. #9 is closed by PR #50 and expanded root coverage to
constructors, accessors, operators, conversion operators, local functions,
lambdas, anonymous methods, and expression-bodied properties. PR #50 also added
`ExecutableMemberSyntax`, which traverses a member's own body while skipping
nested executable bodies. Issue #10 must consume this infrastructure.

The existing `ComplexityAnalyzer` orchestration already reads per-tree options
from `InterproceduralAnalysisContext`, reports threshold diagnostics at
`member.DiagnosticLocation`, and keeps generated code disabled plus concurrent
execution enabled. The new metric can reuse those properties without affecting
`MethodComplexityExtractor`, actionable Big-O diagnostics, recursion, or source
method traversal.

Issue #11 and #14 require future control-flow and nesting work to isolate nested
executable members. This issue therefore keeps traversal ownership reusable and
does not implement nesting depth or Cognitive Complexity scoring.

## Design

Add a small cyclomatic metric model and analyzer under `Analysis`:

- `CyclomaticComplexityAnalysisMode`;
- `CyclomaticComplexityResult`;
- `CyclomaticComplexityAnalyzer`.

The metric analyzer accepts an `ExecutableMember`, mode, and
`CancellationToken`. It traverses only `ExecutableMemberSyntax`
`DescendantNodesInOwnBody<SyntaxNode>` and counts syntax constructs by Roslyn
node kind/type rather than token text.

Configuration grows two independent options:

- `MaximumCyclomaticComplexity` as `int?`;
- `CyclomaticComplexityMode`, defaulting to `Standard`.

Diagnostic orchestration remains in `ComplexityAnalyzer`. `BIG2001` is emitted
only when the threshold is configured and the computed value is strictly greater
than the maximum. Big-O threshold `BIG1006` remains driven only by
`maximum_complexity`.

## Development

Implementation should keep the change cohesive:

- metric logic separated from diagnostic reporting;
- no new Roslyn/package dependencies;
- no hot-path filesystem, network, process execution, or telemetry;
- cancellation checks during traversal;
- no whole-compilation or whole-solution scans;
- no change to existing Big-O estimates or diagnostics.

## Validation

Required evidence:

- unit tests for each decision construct and both switch conventions;
- analyzer threshold tests for missing, invalid, below, equal, above, tree
  specific options, and Roslyn severity override;
- regression tests proving existing Big-O diagnostics remain present/unchanged;
- Release restore, build, and tests;
- package/consumer and performance validation proportional to the new public
  diagnostic/configuration and traversal logic.

## Delivery

Delivery must update:

- analyzer catalog in English and Brazilian Portuguese;
- configuration docs in English and Brazilian Portuguese;
- README diagnostic/configuration summaries where public rule lists appear;
- `AnalyzerReleases.Unshipped.md`;
- final PR body with `Closes #10` and validation evidence.

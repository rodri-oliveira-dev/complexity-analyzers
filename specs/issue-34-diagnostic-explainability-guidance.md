# Issue 34 Diagnostic Explainability and Guidance

This SDD artifact records the diagnostic explainability convention, discovery
matrix, design decisions, validation plan, and delivery checklist for issue #34.

Issues #31, #32, and #33 are complete and merged:

- #31 was closed by PR #37, merged on 2026-08-26 at
  `9ee805733ea74bf78a82543986676b6077319a2b`.
- #32 was closed by PR #38, merged on 2026-08-26 at
  `06efd98b56839cd264aeafa458857ff09df20d76`.
- #33 was closed by PR #39, merged on 2026-08-26 at
  `00f9d37a9a6e45bb43c064a572af208ba4bfcde8`.

This issue preserves the correctness baseline from #31, the performance and
boundedness baseline from #32, and the Roslyn/package/consumer compatibility
contract from #33.

## Specification

Diagnostics must communicate enough proven information for a developer to trust
and triage a finding without turning concise IDE/build output into long-form
documentation.

### Diagnostic Communication Contract

Each diagnostic should answer these questions when the analyzer has stable,
proven evidence for them:

| Dimension | Meaning | Diagnostic message | Analyzer catalog |
| --- | --- | --- | --- |
| WHAT | What was detected? | Yes, in the title/message. | Yes. |
| WHERE | Which operation or construct caused it? | Yes, through the location and, when useful, a stable operation/method name. | Yes, with examples. |
| WHY | Why does it matter? | Briefly when needed. | Yes. |
| COST | What known cost contributes? | Yes when already computed for the finding. | Yes, with reasoning. |
| CONTEXT | In what execution context does it occur? | Yes when essential, such as inside an iteration. | Yes. |
| THRESHOLD | Which configured value was exceeded? | Yes for threshold diagnostics. | Yes in configuration/docs. |
| GUIDANCE | What improvement direction can be considered? | Only short and conditional if it is generally safe. | Yes, always conditional. |
| LIMIT | What is the analyzer not claiming? | Usually no; keep messages short. | Yes. |

### Message Versus Documentation

Diagnostic messages are concise, stable, actionable statements. They contain the
essential finding, relevant proven arguments, and no implementation internals.

Analyzer catalog entries carry detailed reasoning, examples that trigger and do
not trigger, guidance, and limitations. Configuration docs explain options and
threshold behavior. Architecture docs explain the analysis pipeline at a high
level, not per-diagnostic remediation.

### Diagnostic Properties

`Diagnostic.Properties` may be used for stable structured facts that future IDE
or tooling integration can consume without parsing English text.

Properties are allowed only when they meet all of these criteria:

- the value is already available on the reporting path or can be computed with
  negligible cost from existing values;
- the key is stable and meaningful outside the current implementation;
- the value is deterministic, culture-invariant, and independent from private
  traversal/cache details;
- tests assert the contract.

Do not expose internal details such as cache keys, source-method template state,
solver probe order, budget-boundary reasons, traversal roots, or analyzer
implementation class names.

Stable keys for this issue:

| Key | Meaning |
| --- | --- |
| `complexity` | Known method estimate emitted by `BIG0001`, `BIG1005`, or `BIG1006`. |
| `threshold` | Configured threshold expression emitted by `BIG1006`. |
| `operation` | Stable operation or method display name responsible for an actionable diagnostic. |
| `operationComplexity` | Known cost of the operation or callee at the diagnostic location. |
| `iterationComplexity` | Known enclosing iteration complexity. |
| `combinedComplexity` | Known nested contribution composed by the analyzer for that diagnostic. |
| `recurrenceClass` | Stable recurrence outcome class, currently `exponential`. |
| `diagnosticRole` | Stable infrastructure role for `BIG9000`, currently `execution-probe`. |

### Evidence Language

Use wording that distinguishes:

| Evidence type | Meaning | Wording pattern |
| --- | --- | --- |
| Proven fact | The syntax and semantic model establish the relationship required by the rule. | "`X` is executed inside an analyzable iteration." |
| Known estimate | The model computed a non-`Unknown` complexity expression. | "estimated complexity", "known cost". |
| Conservative fallback | The analyzer could not prove a supported result. | Document as "no diagnostic" or "`Unknown`"; do not guess. |
| Conditional guidance | A possible improvement depends on semantics the analyzer cannot verify. | "Consider ... when ... is appropriate." |

The analyzer must not produce "probably", "likely", or speculative Big-O claims.
`Unknown` remains a first-class result and suppresses `BIG0001` and `BIG1006`.

## Discovery

The current reporting pipeline already has enough data for concise explanations:

- `BIG0001` has the known method complexity and method identifier location.
- `BIG1001` through `BIG1004` have the triggering invocation, formatted
  operation/method name, enclosing iteration complexity, operation/callee
  complexity, and composed nested complexity.
- `BIG1005` has the recursive method symbol and solved exponential complexity.
  The current reporting path does not carry the full recurrence relation, so the
  IDE message should not claim a specific equation.
- `BIG1006` has method name, known actual complexity, and configured threshold
  expression after comparison proves `actual > threshold`.
- `BIG9000` has only infrastructure proof that the analyzer loaded and executed.

No change in triggering heuristics is needed. No new symbol resolution,
recurrence extraction, package dependency, Roslyn version, target framework,
whole-project analysis, or hot-path I/O is required.

### Diagnostic Inventory

| Diagnostic | Current message | Proven evidence | Missing context | Possible guidance |
| --- | --- | --- | --- | --- |
| `BIG0001` | `Estimated time complexity: {complexity}` | Method estimate is known; `Unknown` is not reported. | Method name is available from location but not in message. | Documentation should explain estimates and `Unknown`; no IDE remediation. |
| `BIG1001` | Linear lookup with iteration and combined complexity plus indexed lookup advice. | Operation identity, non-constant lookup cost, analyzable iteration, composed contribution. | Operation cost itself is not a message argument. | Prefer conditional set/indexed lookup guidance in docs; avoid saying replacement is always safe. |
| `BIG1002` | Materialization with iteration and combined complexity. | Materializer identity, source enumeration/materialization, analyzable iteration, composed contribution. | Operation cost itself is not a message argument. | Consider moving materialization when independent of the iteration. |
| `BIG1003` | Ordering consumed inside iteration with combined complexity. | Ordering identity, consumed deferred ordering, analyzable iteration, consumed/composed cost. | Operation cost itself can be expressed from consumed complexity. | Consider sorting once when ordering is iteration-independent. |
| `BIG1004` | Source method contributes cost inside iteration with combined complexity. | Safe source method target, known non-constant substituted callee complexity, analyzable iteration, composed contribution. | Full interprocedural template and cache details should remain hidden. | Consider caching/precomputing/memoization only when semantic equivalence holds. |
| `BIG1005` | Recursive method has estimated exponential complexity. | Direct recurrence was extracted and solved to an exponential expression. | Full recurrence relation is not carried by the diagnostic pipeline. | Consider memoization or iterative formulation when repeated subproblems are equivalent. |
| `BIG1006` | Method estimate exceeds configured maximum. | Known actual estimate, concrete threshold, model comparison returned greater. | None for the concise message. | Adjust code or threshold intentionally; boundary is strictly greater. |
| `BIG9000` | Execution probe is active. | Analyzer initialized and compilation-end action executed. | No performance context exists. | Document only as infrastructure smoke test. |

## Design

### Message Format

Keep `BIG0001`, `BIG1005`, `BIG1006`, and `BIG9000` concise. Refine actionable
messages only where the wording currently omits proven cost or risks
prescriptive advice.

Planned message shapes:

| Diagnostic | Message shape |
| --- | --- |
| `BIG0001` | `Estimated algorithmic complexity for '{method}' is {complexity}` |
| `BIG1001` | `{operation} performs a linear lookup with known cost {operationCost} inside an iteration estimated as {iterationCost}. Estimated contribution: {combinedCost}.` |
| `BIG1002` | `{operation} materializes the sequence with known cost {operationCost} inside an iteration estimated as {iterationCost}. Estimated contribution: {combinedCost}.` |
| `BIG1003` | `{operation} performs ordering with known consumed cost {operationCost} inside an iteration estimated as {iterationCost}. Estimated contribution: {combinedCost}.` |
| `BIG1004` | `Method '{method}' has input-dependent complexity {operationCost} and is invoked inside an iteration estimated as {iterationCost}. Estimated contribution: {combinedCost}.` |
| `BIG1005` | `Recursive method '{method}' exhibits exponential growth with estimated complexity {complexity}` |
| `BIG1006` | `Method '{method}' has estimated complexity {actual}, exceeding configured maximum {threshold}` |
| `BIG9000` | unchanged |

### Properties

Add stable properties only to diagnostics whose reporting path already has the
values:

| Diagnostic | Properties |
| --- | --- |
| `BIG0001` | `complexity` |
| `BIG1001` | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |
| `BIG1002` | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |
| `BIG1003` | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |
| `BIG1004` | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |
| `BIG1005` | `complexity`, `recurrenceClass` |
| `BIG1006` | `complexity`, `threshold` |
| `BIG9000` | `diagnosticRole` |

### Location Policy

Keep existing locations because they already point to useful constructs:

- actionable operation diagnostics point to the triggering invocation;
- method-level estimates, recursive growth, and threshold diagnostics point to
  the method identifier;
- the probe points to source start when available, otherwise no source location.

## Development

Implementation should:

1. add a small internal helper for stable diagnostic-property keys/creation;
2. update descriptor message formats;
3. pass existing method name/complexity/operation values into messages and
   properties without extra analysis;
4. update tests for exact public message text and stable properties;
5. update English and Brazilian Portuguese analyzer catalog entries with the
   explainability convention, examples, reasoning, guidance, and limitations;
6. update release tracking for public diagnostic metadata changes.

## Validation Plan

Run from the repository root:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageConsumerContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerHostCompatibilityContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Before delivery, run `git diff --check`, review the full diff, and confirm that
no generated artifacts are included.

## Delivery

The pull request should use a Conventional Commit title, reference `Closes #34`,
and call out:

- no diagnostic IDs, default severities, default enablement, configuration keys,
  target frameworks, Roslyn versions, package layout, or runtime dependencies
  changed;
- diagnostic messages/properties changed intentionally;
- recommendations remain conditional and documentation carries the longer
  reasoning;
- #31, #32, and #33 validation remains green.

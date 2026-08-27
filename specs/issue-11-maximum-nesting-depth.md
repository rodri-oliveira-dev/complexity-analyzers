# Issue #11 SDD - Maximum Control-Flow Nesting Depth

## Specification

Issue #11 adds a deterministic structural metric for supported C# executable
members:

```text
Maximum Control-Flow Nesting Depth
```

The metric answers: what is the greatest depth of nested control-flow structures
inside this executable member?

The baseline is:

```text
straight-line executable member => 0
```

It is independent from:

- Big-O estimation;
- Cyclomatic Complexity and Modified McCabe;
- Cognitive Complexity;
- NLOC, statement count, token count, and brace count.

Threshold behavior is opt-in through:

```ini
complexity_analyzers.maximum_nesting_depth = 3
```

Missing or invalid configuration produces no `BIG2002` diagnostic. Valid values
are non-negative base-10 integers. The comparison is strictly greater:

```text
actual < maximum  => no diagnostic
actual == maximum => no diagnostic
actual > maximum  => BIG2002
```

### Nesting Rules

| Construct | Rule |
| --- | --- |
| Baseline member body | Starts at depth `0`. |
| `if` | Adds one level for its branch bodies. |
| `else if` | Treated as part of the same flat chain; it does not inherit the parent `if` branch depth. |
| `else` | Does not add a level by itself; its body is evaluated at the owning `if` branch depth. |
| `for`, `foreach`, deconstruction `foreach`, `while`, `do` | Add one level for their bodies. |
| `do` / `while` condition | The construct is counted once, not once for `do` and once for the trailing condition. |
| `switch` statement | Adds one level for all switch sections. |
| switch cases/default labels | Sibling branches; they do not add or accumulate depth. |
| switch expression | Adds one level for all arms. |
| switch expression arms | Sibling branches; they do not add or accumulate depth. |
| `try` | Adds one level for `try`, `catch`, and `finally` branch bodies. |
| `catch` and `finally` | Sibling branches of the `try`; they do not add another level by themselves. |
| multiple catches | Sibling branches; count does not accumulate depth. |
| conditional `?:` expression | Adds one level for the true and false branch expressions. |
| nested conditional expressions | Increase depth when nested inside a conditional branch. |
| boolean `&&` and `||` | Do not add nesting depth. |
| patterns, pattern `and`/`or`/`not`, and `when` guards | Do not add nesting depth by themselves. |
| `lock`, `using`, `fixed`, `checked`, `unchecked` | Lexical/lifetime constructs for this metric; do not add depth by themselves. |
| plain lexical blocks | Do not add depth. |
| object/collection/array/property/anonymous-object initializers | Do not add depth. |
| nested local functions, lambdas, anonymous methods | Not counted in the parent; analyzed as independent executable members. |

Expression children in a control-flow header are evaluated at the current depth.
Branch bodies and branch expressions are evaluated at the incremented depth.

Examples:

```text
if (a) {}
if (b) {}
if (c) {}

Maximum nesting depth => 1
```

```text
if (a)
{
    if (b)
    {
        if (c)
        {
        }
    }
}

Maximum nesting depth => 3
```

## Discovery

Issue #10 is complete. GitHub issue `#10` is closed, and PR `#51` was merged to
`main` on 2026-08-27 with merge commit
`6d4a3f1768b0aa95bd4642ff3a77f73da97a6505`. It added
`CyclomaticComplexityAnalyzer`, `CyclomaticComplexityResult`, `BIG2001`,
`complexity_analyzers.maximum_cyclomatic_complexity`, and
`complexity_analyzers.cyclomatic_complexity_mode`.

The reusable infrastructure from #8/#9/#10 is:

- `ExecutableMember` for member identity, display name, diagnostic location,
  syntax tree, and body ownership;
- `ExecutableMemberBody` for block and expression bodies;
- `ExecutableMemberSyntax` for nested executable body boundaries;
- `ComplexityAnalyzerOptionsReader` for tree-specific analyzer config;
- `ComplexityAnalyzer` orchestration for descriptor registration, option
  lookup, threshold comparison, diagnostic properties, generated-code exclusion,
  and concurrent execution.

`CyclomaticComplexityAnalyzer` is not reused semantically because Cyclomatic
Complexity counts decisions and paths while Maximum Nesting Depth tracks only
the largest nesting depth. Shared infrastructure remains limited to executable
member ownership and analyzer configuration/orchestration.

Issue #14 remains open and will use nesting concepts later for Cognitive
Complexity. This issue intentionally does not implement a cognitive score,
nesting penalties, boolean cognitive penalties, recursion cognitive penalties,
or combined maintainability score.

## Design

Add a separate metric model and analyzer:

- `MaximumNestingDepthResult`;
- `MaximumNestingDepthAnalyzer`.

The analyzer accepts an `ExecutableMember` and `CancellationToken`, keeps state
local to one invocation, traverses the member's owned body, and skips nested
executable bodies. It uses Roslyn syntax node identities rather than brace
counting or keyword-text matching.

The traversal keeps:

```text
currentDepth = 0
maximumDepth = 0

enter counted control-flow construct:
    nextDepth = currentDepth + 1
    maximumDepth = max(maximumDepth, nextDepth)

visit sibling branches using the same nextDepth
restore by returning from recursion
```

Special cases:

- `else if` is recursively analyzed at the original `if` chain depth to avoid
  syntactic nesting inflation.
- switch sections, switch arms, catches, and finalizers are sibling branches.
- `try` establishes the level shared by `try`, `catch`, and `finally` bodies.
- conditional expressions establish a level for true/false branch expressions.

Configuration adds:

- `MaximumNestingDepth` as `int?`;
- `complexity_analyzers.maximum_nesting_depth`;
- invalid fallback to unset.

Diagnostic orchestration adds `BIG2002` in `ComplexityAnalyzer`. It is enabled
by default as a descriptor but functionally inactive until the threshold is
configured. It reports at `member.DiagnosticLocation` and includes deterministic
properties:

- `maximumNestingDepth`;
- `threshold`.

## Development

Implementation is intentionally scoped:

- no new Roslyn package;
- no Workspaces dependency;
- no target framework change;
- no Big-O semantic change;
- no Cyclomatic Complexity semantic change;
- no package-layout change;
- no filesystem, network, process, or telemetry work in analyzer hot paths;
- cancellation checked during traversal;
- state local to the calculator instance invocation.

## Validation

Required evidence:

- calculator tests for baseline, single constructs, nested constructs, sibling
  branches, else-if chains, switches, try/catch/finally, conditional
  expressions, excluded lexical constructs, comments/strings, and nested
  executable body isolation;
- diagnostic tests for missing/invalid/below/equal/above thresholds, zero
  threshold, properties, severity override, tree-specific configuration,
  independence from Big-O and Cyclomatic thresholds, nested lambda roots, and
  supported executable member kinds;
- configuration reader tests for non-negative parsing and invalid fallback;
- descriptor characterization updated for `BIG2002`;
- documentation and release tracking updated;
- performance workload includes flat-heavy, deep-heavy, mixed, and nested-member
  control-flow scenarios.

Local validation should run:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageConsumerContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerHostCompatibilityContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

## Delivery

The PR should be titled:

```text
feat: add Maximum Control-Flow Nesting Depth analysis
```

The PR body should include `Closes #11` and report:

- metric definition and baseline;
- nesting rules table;
- else-if convention;
- switch convention;
- try/catch/finally convention;
- excluded constructs;
- executable-member coverage;
- configuration key and invalid fallback;
- diagnostic ID, message, location, and properties;
- independence from Big-O and Cyclomatic Complexity;
- tests and validation commands actually executed;
- package, compatibility, and performance results;
- remaining risks.

No merge, tag, GitHub Release, NuGet publish, GitHub Packages publish, package
version change, Roslyn upgrade, Workspaces dependency, Cognitive Complexity,
NLOC, token count, statement count, or combined score is part of this issue.

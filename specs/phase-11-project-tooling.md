# Phase 11 SDD - Project-Level Tooling And Duplicate Detection

## Specification

Phase 11 adds a project-level execution surface for `ComplexityAnalysis.Analyzers`
without moving project scanning into `DiagnosticAnalyzer` callbacks. The command
line tool owns filesystem traversal, project entry-point loading, aggregate
reporting, JSON serialization, quality-gate evaluation, and duplicate-code
detection. The analyzer assembly remains independently packable as a Roslyn
analyzer asset.

The first supported command is:

```text
complexity analyze <path> [options]
```

`<path>` must point to a `.csproj`, `.slnx`, or `.sln`. `.csproj` is required.
`.slnx` and `.sln` are supported by deterministically reading referenced C#
project paths and analyzing each project independently. The tool intentionally
uses bounded repository-local project loading based on C# source files and
trusted platform references instead of adding `Microsoft.CodeAnalysis.Workspaces`
to the analyzer package.

## CLI Options

Output selection:

- `--format console` writes a deterministic human-readable report.
- `--format json` writes deterministic machine-readable JSON.
- `--output <path>` writes the selected report to a file instead of stdout.

Project loading and exclusions:

- generated code is excluded by default;
- `bin`, `obj`, `.git`, `.vs`, `.idea`, `.vscode`, `artifacts`, `TestResults`,
  `node_modules`, and package output folders are excluded by default;
- `--include-generated` includes files otherwise classified as generated;
- `--include-build-output` includes known build/output directories.

Quality gates are opt-in only:

- `--max-complexity <constant|log_n|linear|n_log_n|quadratic|cubic|exponential|factorial>`
- `--max-cyclomatic-complexity <int>`
- `--max-nesting-depth <int>`
- `--max-method-nloc <int>`
- `--max-statement-count <int>`
- `--max-token-count <int>`
- `--max-parameters <int>`
- `--max-cognitive-complexity <int>`
- `--max-duplicate-rate <percent>`
- `--max-duplicate-tokens <int>`

Duplicate detection:

- `--detect-duplicates` enables duplicate-code detection;
- `--min-duplicate-tokens <int>` sets the minimum normalized clone window;
- `--min-duplicate-lines <int>` optionally filters occurrences by source line
  count after token matching.

Defaults do not fail builds and do not run duplicate detection. The default
minimum clone size is conservative: 40 normalized tokens.

## Exit Codes

The exit-code taxonomy is intentionally small and CI-friendly:

- `0`: analysis completed and all configured gates passed;
- `1`: command-line, project-loading, parsing, or report-writing error;
- `2`: analysis completed but at least one explicitly configured quality gate
  failed;
- `130`: cancellation was requested.

The same exit code is not used for both configuration/runtime errors and
quality-gate failures.

## Aggregate Report Model

The report root contains:

- schema version;
- deterministic generation mode (`deterministic`);
- analyzed entry point;
- analyzed projects;
- evaluated quality gates;
- duplicate-code summary and clone groups when enabled.

A project record contains:

- project name;
- project file path relative to the invocation working directory when possible;
- language;
- files;
- project-level duplicate rate when duplicate detection is enabled.

A file record contains:

- path;
- generated/build-output exclusion status before filtering when relevant;
- member records;
- token count used for duplicate-rate calculation;
- duplicate token count and duplicate rate when duplicate detection is enabled.

A member record contains:

- member display name;
- member kind;
- stable symbol key when available;
- source location with file path, start/end line, start/end column, and
  character span;
- Big-O estimate represented as text and `isUnknown`;
- Cyclomatic Complexity;
- Maximum Control-Flow Nesting Depth;
- NLOC;
- statement count;
- token count;
- parameter count;
- Cognitive Complexity;
- Halstead primitive and derived metrics.

Unavailable metrics are represented explicitly as:

```json
{ "status": "unknown", "value": null }
```

No unknown metric is coerced to `0`, `O(1)`, or another artificial value.

## Console Report

Console output is sorted by project path, file path, member source start, member
display name, clone group source path, and clone source start. It is intended for
manual review and CI logs. It includes configured gate failures only when a gate
was explicitly configured.

## JSON Report

JSON output uses stable property order from the report DTOs and a deterministic
sort before serialization. Paths are normalized to `/` separators. Machine
readers can map every member and clone occurrence back to project, file, member,
and source range.

## SARIF Seam

Report rendering is separated behind report writers. SARIF can be added later by
implementing another writer over the same aggregate report model. Phase 11 does
not implement SARIF because no issue requires SARIF output yet.

## Duplicate Normalization

Duplicate detection runs only in the project-level tooling layer. It tokenizes
member-owned bodies with Roslyn syntax tokens and skips nested executable bodies
using the same ownership convention as executable-member metrics.

Normalization keeps C# structure and operators material:

- keywords, punctuation, unary operators, binary operators, assignment
  operators, pattern combinators, member access, conditional access, generic
  delimiters, tuple/list/pattern delimiters, interpolation structure, and
  control-flow tokens keep distinct normalized identities;
- parameter identifiers are replaced by stable per-occurrence-role placeholders
  such as `parameter:0`, `parameter:1`;
- local variables and local constants are replaced by stable declaration-order
  placeholders such as `local:0`, `local:1`;
- pattern variables are normalized as locals;
- member and type identifiers are normalized only by syntactic role, preserving
  distinctions between member names, type names, namespaces, labels, and unknown
  identifiers;
- numeric literals normalize to `literal:number`;
- string literals normalize to `literal:string`;
- character literals normalize to `literal:char`;
- `true`, `false`, and `null` keep distinct identities;
- interpolated strings retain interpolation boundaries and normalize literal
  text segments to string-literal placeholders while recursively preserving
  expression tokens.

The normalizer does not reduce all identifiers or all operators to one token.
Changing `+` to `-`, `&&` to `||`, `if` to `switch`, member access to element
access, or a generic type shape to a non-generic shape changes the normalized
sequence.

Positive examples:

```csharp
int Sum(int[] values)
{
    var total = 0;
    foreach (var value in values)
    {
        total += value;
    }

    return total;
}
```

matches the same structure with renamed parameters and locals.

```csharp
return customer.Name + ":" + customer.Id;
```

matches equivalent member-access structure with different local receiver names
and different string literal values.

Negative examples:

```csharp
total += value;
total -= value;
```

do not match each other because the assignment operators differ.

```csharp
if (value > 0) { return value; }
while (value > 0) { return value--; }
```

do not match each other because control flow and operators differ.

## Duplicate Detection Algorithm

The detector uses token-window fingerprinting:

1. Analyze files in deterministic path order.
2. Normalize owned executable-member token streams.
3. Build fixed-size windows of `minDuplicateTokens` normalized tokens.
4. Compute a bounded rolling-style FNV-1a fingerprint for each window and index
   occurrences by hash.
5. For every hash bucket with more than one occurrence, compare the full
   normalized window sequence before accepting candidates. A hash match alone is
   never reported as a clone.
6. Extend verified candidates left/right within their member-owned token stream
   while normalized tokens remain equal.
7. Suppress contained or overlapping occurrences deterministically by keeping
   the longest group, then earliest project/file/start order.

Expected complexity is approximately `O(T * W + C)` where `T` is the number of
normalized tokens, `W` is the configured window size for fingerprint material,
and `C` is the bounded candidate verification cost for repeated fingerprints.
Space is `O(T)` for normalized token storage and the fingerprint index. The
candidate set is reduced by indexing fixed windows rather than comparing all
substrings pairwise.

Collision verification is part of the detector contract and is covered by tests
using an injectable fingerprint strategy.

## Clone Groups

A clone group contains:

- deterministic group id;
- normalized token count;
- occurrence count;
- occurrence source ranges;
- duplicate token count, calculated as `normalizedTokenCount * (occurrences - 1)`;
- duplicate percentage relative to project duplicate-token denominator.

Occurrences may be in the same file or different files. Source ranges are based
on the first and last normalized source token in the occurrence.

Project duplicate rate is:

```text
duplicate tokens / total normalized duplicate-analysis tokens
```

File duplicate rate uses the same formula with file-level denominators and the
file's participating duplicate tokens.

## Validation Plan

Phase 11 requires:

- unit tests for report model unknown values and deterministic serialization;
- integration tests for `.csproj`, `.slnx`, and `.sln` entry points where
  fixtures are available;
- CLI tests for console, JSON, exit codes, quality gates, exclusions, and
  cancellation;
- equivalence tests proving CLI member metrics reuse analyzer metric
  calculators;
- duplicate normalization tests for identifiers, literals, operators, member
  access, generics, patterns, interpolated strings, and modern C# constructs;
- duplicate detection tests for exact, renamed, literal-normalized, same-file,
  cross-file, below-threshold, near-miss, overlap, contained, collision, ordering,
  source range, quality-gate, and cancellation scenarios;
- full repository restore, Release build, test, pack, package/consumer focused
  tests, and performance structural checks before the PR.

# Project-Level Tooling

[English](project-tooling.md) | [Português (Brasil)](../pt-BR/project-tooling.md)

`ComplexityAnalysis.Tool` is the installable `complexity-analysis` command for
deterministic C# project and solution reports, snapshots, regression comparison,
duplicate detection, and CI quality gates.

Choose the Roslyn `ComplexityAnalysis.Analyzers` package for diagnostics during
compilation, this CLI for explicit whole-project or solution runs, and the
separate local MCP host for read-only MCP client integration. None installs or
requires either of the others.

## Requirements And Installation

The tool targets `net10.0` and requires the .NET 10 runtime. After version
`1.0.0` is published to NuGet.org, install it globally:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.0
complexity-analysis --version
```

For a repository-pinned installation:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.0
dotnet tool restore
dotnet tool run complexity-analysis --version
```

Maintain a global installation with `dotnet tool update --global
ComplexityAnalysis.Tool --version <version>` and `dotnet tool uninstall
--global ComplexityAnalysis.Tool`. For a local manifest, omit `--global`; run it
with `dotnet tool run complexity-analysis` or `dotnet complexity-analysis`.

These commands consume a published package. For an unpublished package built
from this repository, use an explicit local source and local-only version:

```bash
dotnet pack src/ComplexityAnalysis.Tool/ComplexityAnalysis.Tool.csproj \
  --configuration Release \
  -p:Version=0.0.0-local \
  -p:PackageVersion=0.0.0-local \
  --output artifacts/local-packages
dotnet tool install ComplexityAnalysis.Tool \
  --version 0.0.0-local \
  --add-source artifacts/local-packages \
  --tool-path artifacts/local-tools
export PATH="$PWD/artifacts/local-tools:$PATH"
complexity-analysis --version
```

## Run The CLI

Analyze a project or solution with the installed command:

```bash
complexity-analysis analyze MySolution.slnx
complexity-analysis analyze MyProject.csproj --format json
complexity-analysis analyze MyProject.csproj --format sarif
complexity-analysis analyze MyProject.csproj --max-cyclomatic-complexity 10
```

Supported entry points:

- `.csproj`;
- `.slnx`;
- `.sln`.

Solution files are read deterministically and each referenced C# project is
analyzed independently. Project loading is intentionally repository-local and
does not add `Microsoft.CodeAnalysis.Workspaces` to the analyzer package.

Use `complexity-analysis --help`, `complexity-analysis analyze --help`, or
`complexity-analysis compare --help` for the installed version's built-in help.

## CLI Reference

`analyze <path>` accepts exactly one `.csproj`, `.slnx`, or `.sln` path.

| Option | Value/default | Rules |
| --- | --- | --- |
| `--format` | `console` (default), `json`, `sarif` | Case-insensitive. |
| `--output` | Path; stdout by default | Parent directory must already exist. |
| `--include-evidence` | Off by default | Requires JSON or SARIF. |
| `--snapshot` | Off by default | Requires JSON and implies evidence. |
| `--include-generated` | Off by default | Includes generated C# files. |
| `--include-build-output` | Off by default | Includes normally excluded output directories. |
| `--max-complexity` | No gate | `constant`, `log_n`, `linear`/`n`, `n_log_n`, `quadratic`/`n2`, `cubic`/`n3`, `exponential`, or `factorial`. |
| `--max-cyclomatic-complexity` | No gate | Positive integer. |
| `--max-nesting-depth` | No gate | Non-negative integer. |
| `--max-method-nloc` | No gate | Positive integer. |
| `--max-statement-count` | No gate | Positive integer. |
| `--max-token-count` | No gate | Positive integer. |
| `--max-parameters` | No gate | Non-negative integer. |
| `--max-cognitive-complexity` | No gate | Non-negative integer. |
| `--detect-duplicates` | Off by default | Enables normalized duplicate detection. |
| `--min-duplicate-tokens` | `40` | Positive integer; also enables detection. |
| `--min-duplicate-lines` | Unset | Positive integer; also enables detection. |
| `--max-duplicate-rate` | No gate | Non-negative invariant-culture number; also enables detection. |
| `--max-duplicate-tokens` | No gate | Positive integer; also enables detection. |

`compare <baseline.json> <current.json>` requires two `complexity-snapshot/1.0`
files. It accepts `--format console|json|sarif` (console by default), `--output
<path>`, and the opt-in `--fail-on-regression` gate. Unknown options or missing
values return exit code `1`.

## Output

Console output is the default:

```bash
complexity-analysis analyze samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj
```

JSON output is available for automation:

```bash
complexity-analysis analyze samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj --format json --output artifacts/project-report.json
```

For a comparison baseline, add `--snapshot`. This emits the independent
`complexity-snapshot/1.0` contract, includes deterministic evidence, and records
normalized origin and analysis configuration. Members use Roslyn documentation
IDs, so overloads remain distinct and source line or file moves do not change
identity. Renames are represented as removal plus addition; duplicate identities
are ambiguous and are never matched heuristically.

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj --format json --snapshot --output artifacts/baseline.json
```

The default JSON contract remains schema `1.0`. Deterministic complexity
evidence is opt-in and uses schema `1.1`:

```bash
complexity-analysis analyze samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj --format json --include-evidence
```

`--include-evidence` is valid with `--format json` or `--format sarif`. Each member then
includes the bounded evidence trace produced by the analysis core: stable step
identity, operation and origin, relative source span, input dimensions,
estimate guarantee and assumptions, composition, truncation state, and an
explicit reason when the final result is `Unknown`. Evidence locations are
relative to the analyzed member and never contain an absolute filesystem path.

Both formats are sorted by project path, file path, member source location, and
clone location. JSON keeps `Unknown` metrics explicit instead of converting
missing information to `0` or `O(1)`.

## Aggregate Metrics

The aggregate report includes project, file, member, symbol, and source-range
identity. For each supported executable member it reports:

- Big-O, including explicit `Unknown`;
- Cyclomatic Complexity;
- Maximum Control-Flow Nesting Depth;
- NLOC;
- statement count;
- token count;
- parameter count;
- Cognitive Complexity;
- Halstead primitive and derived metrics.

The CLI reuses the analyzer metric calculators. It does not reimplement metric
formulas in a parallel reporting stack.

## Exclusions

By default the tool excludes generated code and common output directories:

- generated file names such as `.g.cs`, `.g.i.cs`, `.designer.cs`, and
  `.generated.cs`;
- files with an early `<auto-generated` marker;
- `bin`, `obj`, `artifacts`, `TestResults`, `packages`;
- infrastructure folders such as `.git`, `.vs`, `.idea`, `.vscode`, and
  `node_modules`.

Use `--include-generated` or `--include-build-output` only when a project-level
report intentionally needs those files.

## Quality Gates

Quality gates are opt-in. No default threshold can fail a build.

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj \
  --max-cyclomatic-complexity 10 \
  --max-nesting-depth 3 \
  --max-method-nloc 40 \
  --max-statement-count 25 \
  --max-token-count 300 \
  --max-parameters 5 \
  --max-cognitive-complexity 15
```

Big-O thresholds support `constant`, `log_n`, `linear`/`n`, `n_log_n`,
`quadratic`/`n2`, `cubic`/`n3`, `exponential`, and `factorial`.

Exit codes:

- `0`: analysis completed and configured gates passed;
- `1`: command-line, project-loading, parsing, or report-writing error;
- `2`: at least one explicitly configured quality gate failed;
- `130`: cancellation was requested.

## Compare Snapshots

Compare two previously generated snapshots without invoking Git or an external
service:

```bash
complexity-analysis compare artifacts/base.json artifacts/head.json --format json
```

Use `--format console` (the default) or `--format json`, plus `--output <path>`
when needed. Findings distinguish regressions, improvements, equivalent,
incomparable, added, removed, and ambiguous members. `--fail-on-regression` is
opt-in and returns exit code `2` only for proven regressions. `Unknown` and
incompatible guarantees, assumptions, configurations, or input dimensions do
not fail the gate.

### Opt-in CI regression check

The `Complexity Analyzers CI` workflow exposes the boolean
`run_complexity_regression` input for manual runs. When enabled, it generates a
fresh snapshot for the runnable sample, compares it with the reviewed snapshot
in `performance/baselines`, fails only on proven regressions, uploads the
baseline/current/comparison JSON files as the `complexity-regression` artifact,
and writes classification counts to the job summary.

This job is intentionally opt-in and read-only. It does not comment on pull
requests, mutate the repository, publish packages, or request permissions beyond
`contents: read`. Update the reviewed baseline only when an intentional analysis
change has been validated and explained in the pull request.

## SARIF 2.1.0

Both `analyze` and `compare` accept `--format sarif`. The output follows SARIF
2.1.0, uses relative percent-encoded artifact URIs, stable SHA-256 partial
fingerprints, declared rules, source regions, and deterministic result order.

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj --format sarif --include-evidence --output artifacts/complexity.sarif
complexity-analysis compare artifacts/base.json artifacts/head.json --format sarif --output artifacts/comparison.sarif
```

Known member estimates are notes. A member with `Unknown` complexity is emitted
only when evidence contains a legitimate explicit reason. Proven comparison
regressions are warnings; other comparison findings are notes. Selecting SARIF
does not enable a quality gate or change exit codes—`--fail-on-regression`
remains a separate opt-in switch.

## Duplicate Detection

Duplicate detection is also opt-in:

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj \
  --detect-duplicates \
  --min-duplicate-tokens 40 \
  --max-duplicate-rate 5
```

The detector reports same-file and cross-file clone groups with source ranges,
normalized token count, occurrence count, duplicate token count, and duplicate
rate. `--min-duplicate-lines` can add a source-line filter after token matching.

Normalization is C#-aware:

- parameters, locals, local constants, and pattern variables are normalized by
  stable role/order;
- numeric, string, and character literals are normalized by literal category;
- boolean and `null` literals stay distinct;
- member/type/namespace identifiers keep syntactic roles instead of collapsing
  into one identifier token;
- unary, binary, assignment, member access, generic, pattern, interpolation, and
  control-flow tokens keep distinct identities.

Changing `+` to `-`, `&&` to `||`, `if` to `while`, member access to element
access, or generic shape to non-generic shape changes the normalized sequence.

The algorithm indexes fixed normalized-token windows with an FNV-1a fingerprint,
then verifies every hash candidate by comparing the normalized token sequence.
A hash match alone is never enough to report a clone. Verified candidates are
extended and overlapping or contained findings are suppressed deterministically
by keeping the longest group first, then earliest source order.

Expected complexity is bounded by token count, window size, and repeated-window
candidate verification. The implementation avoids all-pairs comparison of every
substring.

## Architecture Boundary

Project loading, filesystem traversal, JSON/console rendering, quality gates,
and duplicate detection live in `ComplexityAnalysis.Tool`. `ComplexityAnalyzer`
does not scan projects, solutions, or files in `Initialize` or analyzer
callbacks.

The analyzer package remains a `netstandard2.0` Roslyn analyzer asset under
`analyzers/dotnet/cs/`. The CLI may use repository tooling dependencies, but
those dependencies are not exposed to analyzer package consumers.

## GitHub Actions integration

For reusable GitHub CI analysis, use the separately maintained
[Complexity Analysis Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action#quick-start). Its
[stable `v1.0.0` release](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0)
was published on 2026-10-30 and installs the exact published
`ComplexityAnalysis.Tool` version `1.0.1` by default.

Choose the CLI directly for local execution or custom CI orchestration; choose
the Action for reusable GitHub Actions workflows with JSON/SARIF reporting,
opt-in quality gates, and pull-request regression comparison. Both invoke the
same analysis engine and preserve identical metric and snapshot semantics.
Their versions are independent of each other and of the Analyzer and MCP.

For reproducibility, pin the Action to the reviewed `v1.0.0` release commit
`2d807ef29ca03c685d224543a135d8c30dfe2c67`; the published `@v1.0.0` tag is another
supported option. The moving `@v1` alias has not been created yet. See the
[GitHub Action guide](github-action.md) and
[published examples](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/v1.0.0/examples) for
supported inputs and security considerations.

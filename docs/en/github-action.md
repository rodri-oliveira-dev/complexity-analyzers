# GitHub Action: analyze C# complexity in CI

[Português (Brasil)](../pt-BR/github-action.md) · [Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action)

`Complexity Analysis Action` makes the existing [ComplexityAnalysis.Tool](project-tooling.md) available in GitHub Actions workflows. It **does not** implement a second analyzer. Use the Roslyn package for diagnostics in your IDE/build, the CLI for local or custom automation, the MCP server for AI-tool integrations, and the GitHub Action when you want reusable CI checks and pull-request feedback.

The Action lives in a **separate repository** and has its own release cycle. The planned first stable Action release is `v1.0.0`; **as of this documentation update it has not been published**, so `@v1` and `@v1.0.0` must not be treated as existing tags. Review the [Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action) for current status before adopting it.

## What it offers

- Analyze a `.csproj`, `.sln`, or `.slnx` on a supported Ubuntu runner.
- Produce deterministic JSON reports, SARIF 2.1.0, and a GitHub Job Summary.
- Configure opt-in gates for Big-O, cyclomatic/cognitive complexity, nesting depth, method NLOC, and duplication rate.
- Generate baseline/current snapshots and identify **proven** complexity regressions in pull requests.
- Optionally upload SARIF to GitHub Code Scanning or publish a bounded, updated PR comment.

A complexity estimate is **not** a runtime benchmark or proof of actual latency. Unknown, ambiguous, or incomparable evidence must not be described as a confirmed regression.

## Requirements and versioning

The current release candidate supports Ubuntu runners with Bash, Python 3, and access to NuGet.org. It installs the .NET 10 SDK and the **exact** published `ComplexityAnalysis.Tool` version specified by `tool-version` (currently `1.0.1` by default). The CLI, Action, Analyzer, and MCP packages have **independent versions**.

Before the first stable release, use a **reviewed full 40-character commit SHA** from the Action repository in `uses:`. The placeholder below is intentionally not executable. When a stable version is published, a specific release tag such as `@v1.0.0` or a full SHA may be used; `@v1` is only available when the maintainers create that major-version reference.

## Example: analyze a pull request

Save the following as `.github/workflows/complexity.yml` in the **consumer** repository, after replacing `REVIEWED_FULL_40_CHARACTER_SHA` and `MySolution.slnx`:

```yaml
name: Complexity analysis

on:
  pull_request:

permissions:
  contents: read

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd
        with:
          persist-credentials: false
      - id: complexity
        uses: rodri-oliveira-dev/complexity-analysis-action@REVIEWED_FULL_40_CHARACTER_SHA
        with:
          path: MySolution.slnx
          max-cyclomatic-complexity: "15"
          max-cognitive-complexity: "20"
          fail-on-quality-gate: "true"
          compare-with-base: "true"
          fail-on-regression: "true"
```

This example is **read-only**; it does not post comments, upload SARIF, or expose a write-capable token. The Action reports comparison problems separately from confirmed regressions. To retain report files after the job, add an `actions/upload-artifact` step using the Action's output paths.

## Inputs and outputs

`path` is required. Other notable inputs are:

| Input | Default | Purpose |
| --- | --- | --- |
| `tool-version` | `1.0.1` | Exact published CLI version |
| `output-directory` | `complexity-analysis-reports` | Directory for generated files |
| `detect-duplicates` | `false` | Opt into duplicate detection |
| `max-complexity` | unset | Maximum supported Big-O class |
| `max-cyclomatic-complexity` | unset | Cyclomatic threshold |
| `max-cognitive-complexity` | unset | Cognitive threshold |
| `max-nesting-depth` | unset | Nesting threshold |
| `max-method-nloc` | unset | Method NLOC threshold |
| `max-duplicate-rate` | unset | Duplicate-rate threshold |
| `fail-on-quality-gate` | `true` | Fail when a configured gate fails |
| `compare-with-base` | `false` | Compare head with the PR base revision |
| `base-ref` / `baseline-path` | unset | Select explicit Git baseline or existing snapshot |
| `fail-on-regression` | `false` | Fail only for proven regressions |
| `upload-sarif` | `false` | Optional GitHub Code Scanning upload |
| `comment-on-pr` | `false` | Optional PR comment, requires extra permissions |

Additional supported inputs include `include-generated` and `comparison-output`. See the [Action's source-of-truth input reference](https://github.com/rodri-oliveira-dev/complexity-analysis-action#inputs) for the complete list and constraints.

The Action exposes `report-path`, `sarif-path`, `analysis-status`, `quality-gate-status`, and `tool-version`. Regression workflows also expose `baseline-report-path`, `current-report-path`, `comparison-report-path`, `regression-count`, `comparison-status`, and `pr-report-path`. Report paths describe files in the job workspace, **not permanent download links**.

## Permissions and safe PR feedback

| Mode | Minimum relevant permissions |
| --- | --- |
| Analysis, quality gates, baseline/regression comparison | `contents: read` |
| Optional Code Scanning upload | `contents: read`, `security-events: write` |
| Optional direct PR comment in a trusted same-repository workflow | `contents: read`, `pull-requests: write` |
| Fork PR feedback | Read-only analysis plus a separate privileged `workflow_run` publisher |

Never analyze untrusted pull-request code under `pull_request_target`, and never give write-capable credentials or secrets to a job loading untrusted projects. MSBuild project evaluation may execute project-controlled logic; the Action is **not** a sandbox. For fork PRs, keep analysis read-only and use the Action's [isolated publisher example](https://github.com/rodri-oliveira-dev/complexity-analysis-action/blob/feature/release-readiness/examples/secure-pr-publish.yml) if comments are required. Avoid copying that privileged workflow without reviewing the artifact-provenance restrictions.

## Troubleshooting

- **Action not found at `@v1` or `@v1.0.0`:** the initial release and tag alias may not exist. Check [releases](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases) and pin a reviewed commit SHA during evaluation.
- **Unsupported runner:** the current release candidate documents Ubuntu only; Windows/macOS have not passed equivalent end-to-end validation.
- **Missing project or unexpected comparison failure:** confirm the path exists and the entry point can be resolved at both base and head revisions. A missing baseline is **not** “no regressions.”
- **NuGet/tool installation error:** check network access, the .NET 10 requirement, and that `tool-version` is an exact published version.
- **SARIF upload or comment denied:** review token permissions, Code Scanning availability, fork restrictions, and the security split between analysis and publishing.
- **Configured gates fail:** a quality-gate failure is separate from a proven regression; inspect `quality-gate-status`, `comparison-status`, and the JSON reports.

For complete workflow examples, visit the [Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/feature/release-readiness/examples). For standalone CLI commands, use [Project-Level Tooling](project-tooling.md).

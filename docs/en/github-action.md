# GitHub Action: analyze C# complexity in CI

[Português (Brasil)](../pt-BR/github-action.md) · [Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action)

`Complexity Analysis Action` makes the existing [ComplexityAnalysis.Tool](project-tooling.md) available in GitHub Actions workflows. It **does not** implement a second analyzer. Use the Roslyn package for diagnostics in your IDE/build, the CLI for local or custom automation, the MCP server for AI-tool integrations, and the GitHub Action when you want reusable CI checks and pull-request feedback.

The Action lives in a **separate repository** and has its own release cycle.
Its first stable version, [`v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0),
was published on October 30, 2026. The `@v1.0.0` version tag exists and
points to the reviewed commit `2d807ef29ca03c685d224543a135d8c30dfe2c67`. The moving
`@v1` major-version alias has **not** been created yet. GitHub Release
publication does not by itself confirm a public Marketplace listing.

## What it offers

- Analyze a `.csproj`, `.sln`, or `.slnx` on a supported Ubuntu runner.
- Produce deterministic JSON reports, SARIF 2.1.0, and a GitHub Job Summary.
- Configure opt-in gates for Big-O, cyclomatic/cognitive complexity, nesting depth, method NLOC, and duplication rate.
- Generate baseline/current snapshots and identify **proven** complexity regressions in pull requests.
- Optionally upload SARIF to GitHub Code Scanning or publish a bounded, updated PR comment.

A complexity estimate is **not** a runtime benchmark or proof of actual latency. Unknown, ambiguous, or incomparable evidence must not be described as a confirmed regression.

## Requirements and versioning

The published `v1.0.0` supports Ubuntu runners with Bash, Python 3, and
access to NuGet.org. It installs .NET SDK `10.0.403` and uses the exact
published `ComplexityAnalysis.Tool` version supplied by `tool-version`
(`1.0.1` by default). Windows and macOS have not been validated as supported
runners. The CLI, Action, Analyzer, and MCP have **independent versions**.

For reproducible consumption, prefer the full 40-character reviewed release
commit SHA in `uses:`; the published `@v1.0.0` tag is also available.
Do not use `@v1` until its compatibility reference is created and verified.
The public release-tag ruleset and GitHub Release immutability are separate
controls; do not assume the latter is enabled.

## Example: analyze a pull request

Save the following as `.github/workflows/complexity.yml` in the **consumer** repository. It is pinned to the published Action release commit; replace only `MySolution.slnx` with the path to your `.csproj`, `.sln`, or `.slnx`:

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
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
        with:
          persist-credentials: false
      - id: complexity
        uses: rodri-oliveira-dev/complexity-analysis-action@2d807ef29ca03c685d224543a135d8c30dfe2c67
        with:
          path: MySolution.slnx
          max-cyclomatic-complexity: "15"
          max-cognitive-complexity: "20"
          fail-on-quality-gate: "true"
          compare-with-base: "true"
          fail-on-regression: "true"
```

This example uses **read-only** permissions: it does not post comments, upload
SARIF to Code Scanning, or expose a write-capable token. The Action reports
unavailable comparisons separately from confirmed regressions; a missing
baseline is not proof of no regressions. To retain reports after the job,
add a SHA-pinned `actions/upload-artifact` step using the Action's output paths.

For additional runnable workflows, use the
[updated examples on the Action's `main` branch](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples).
The `v1.0.0` tag preserves its original release snapshot, including
documentation examples that still contain pre-publication placeholders;
the corrected, executable examples were committed to `main` after the release.

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

Never analyze untrusted pull-request code under `pull_request_target`, and never give write-capable credentials or secrets to a job loading untrusted projects. MSBuild project evaluation may execute project-controlled logic; the Action is **not** a sandbox. For fork PRs, keep analysis read-only and use the Action's [isolated publisher example](https://github.com/rodri-oliveira-dev/complexity-analysis-action/blob/main/examples/secure-pr-publish.yml) if comments are required. Avoid copying that privileged workflow without reviewing the artifact-provenance restrictions.

## Troubleshooting

- **Action not found at `@v1`:** that moving alias has not yet been created. Use the published `@v1.0.0` tag or approved release commit SHA above. If `@v1.0.0` fails, confirm the repository reference and the [published release](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0).
- **Unsupported runner:** `v1.0.0` supports Ubuntu only; Windows/macOS have not passed equivalent end-to-end validation.
- **Missing project or unexpected comparison failure:** confirm the path exists and the entry point can be resolved at both base and head revisions. A missing baseline is **not** “no regressions.”
- **NuGet/tool installation error:** check network access, the .NET 10 requirement, and that `tool-version` is an exact published version.
- **SARIF upload or comment denied:** review token permissions, Code Scanning availability, fork restrictions, and the security split between analysis and publishing.
- **Configured gates fail:** a quality-gate failure is separate from a proven regression; inspect `quality-gate-status`, `comparison-status`, and the JSON reports.

For complete workflow examples, visit the [Action repository](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/main/examples). For standalone CLI commands, use [Project-Level Tooling](project-tooling.md).

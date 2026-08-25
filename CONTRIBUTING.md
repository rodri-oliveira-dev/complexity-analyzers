# Contributing

Thank you for contributing to `ComplexityAnalysis.Analyzers`. Keep changes focused, reviewable, and aligned with the analyzer's public diagnostics, compatibility guarantees, and conservative analysis model.

## Code of Conduct

By participating in this project, you agree to follow [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Prerequisites

Install:

- .NET SDK 10, or a compatible SDK selected by `global.json`;
- Git.

No globally installed .NET tool should be required for the standard development workflow.

## Prepare the repository

From the repository root:

```bash
dotnet tool restore
dotnet restore ComplexityAnalysis.Analyzers.slnx
```

## Validate a change

Before opening a pull request, run the same core checks expected by CI:

```bash
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

When a change touches formatting or analyzer style rules, also run the repository formatting checks configured by the project.

## Keep changes focused

A contribution should address one cohesive concern. Avoid mixing unrelated refactors, formatting changes, dependency updates, documentation rewrites, and feature work unless they are required by the same change.

Prefer the smallest change that solves the problem while preserving analyzer correctness, package compatibility, build performance, and conservative `Unknown` behavior for unsupported cases.

## Analyzer behavior and diagnostics

Changes that affect complexity estimation or diagnostics should preserve the project's semantic-analysis principles:

- use Roslyn syntax, symbols, and semantic information rather than text-only matching;
- prefer `Unknown` over an unsafe or unproven complexity estimate;
- keep source-method traversal and recurrence solving bounded;
- avoid analyzer hot-path I/O, network access, process execution, or heavyweight reflection;
- preserve compatibility with supported compiler/SDK hosts.

When adding or changing a public diagnostic:

- add or update tests for triggering and non-triggering cases;
- update `src/ComplexityAnalysis.Analyzers/AnalyzerReleases.Unshipped.md` when required by analyzer release tracking;
- update the analyzer catalog under `docs/en/analyzers.md` and `docs/pt-BR/analyzers.md`;
- document any new `.editorconfig`/analyzer-config option in both configuration guides.

## Tests

Behavior changes should be covered by tests. Bug fixes should include a regression test when practical. Analyzer rules should normally test both positive and negative cases to reduce false positives.

Changes to interprocedural analysis, recurrence solving, caching, budgets, or package loading should also consider the existing compatibility and performance test suites.

Do not weaken existing validation merely to make a change pass.

## Performance

Roslyn analyzers execute inside compiler and IDE hosts, so performance regressions can affect every consumer build.

For changes to analysis hot paths, caching, recursion, interprocedural traversal, or known-operation resolution, run the relevant performance tests and review allocation/complexity implications. Avoid broad scans when demand-driven analysis is sufficient.

## Compatibility and dependencies

`Microsoft.CodeAnalysis.CSharp` is intentionally pinned to the repository's selected Roslyn compatibility baseline. Do not upgrade the Roslyn compiler API package as a routine dependency update; such changes require explicit compatibility evaluation across supported hosts.

Do not introduce `Microsoft.CodeAnalysis.Workspaces` or runtime dependencies unless the feature clearly requires them and the architectural/package impact has been reviewed.

## Releases

Production releases are created manually through the `Release` GitHub Actions workflow in `.github/workflows/release.yml`.

Run the workflow from the `main` branch and provide only the semantic package version, without a `v` prefix:

```text
1.0.0
```

The workflow derives the Git tag automatically:

```text
1.0.0 -> v1.0.0
```

Prerelease versions are also supported, for example:

```text
1.1.0-beta.1 -> v1.1.0-beta.1
```

The release pipeline:

1. validates the semantic version and requires execution from `main`;
2. restores, builds, tests, packs, and validates `ComplexityAnalysis.Analyzers`;
3. creates the corresponding `v<version>` Git tag, or verifies it when safely retrying the same release;
4. publishes the `.nupkg` to NuGet.org using Trusted Publishing and GitHub OIDC;
5. publishes the same `.nupkg` to GitHub Packages using the workflow `GITHUB_TOKEN`;
6. creates a GitHub Release for the generated tag and attaches the package artifact.

The NuGet.org Trusted Publishing policy must match the workflow identity exactly:

```text
Repository owner: rodri-oliveira-dev
Repository: complexity-analyzers
Workflow file: release.yml
Environment: release
Package: ComplexityAnalysis.Analyzers
```

The NuGet publishing job uses the GitHub environment named `release` and `id-token: write`; no long-lived NuGet API key should be stored in repository secrets.

Do not manually move, reuse, or recreate an existing release tag for a different commit. Release tags are intended to be immutable.

## Pull requests

Pull requests should:

- explain what changed and why;
- link the relevant issue when one exists;
- describe how the change was validated;
- call out diagnostic, compatibility, package, performance, or security impact;
- update English and Portuguese documentation together when public behavior changes;
- keep generated or unrelated files out of the diff.

Reviewers may request smaller changes, additional tests, clearer documentation, or compatibility evidence before approval.

## Review expectations

Address review feedback with additional commits while the pull request is open. Resolve discussions only after the concern has been addressed or agreement has been reached.

A pull request is ready to merge when the intended behavior is clear, required checks pass, documentation matches the implementation, and no known analyzer-loading, compatibility, security, or performance issue is left unexplained.

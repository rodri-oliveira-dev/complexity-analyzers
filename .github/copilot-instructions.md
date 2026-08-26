# GitHub Copilot Instructions

This repository contains `ComplexityAnalysis.Analyzers`, a standalone Roslyn analyzer for estimating and diagnosing algorithmic complexity in C# code.

## Start here

Before making changes, read the root [`AGENTS.md`](../AGENTS.md). It is the primary repository-specific instruction source and contains the detailed architecture, Roslyn, NuGet, performance, validation, and Git rules.

Use the canonical Definition of Done in [`docs/en/development/quality-gates.md`](../docs/en/development/quality-gates.md) to classify the change type and choose proportional validation. Do not duplicate or bypass that policy.

Use the relevant skills under [`.agents/skills/`](../.agents/skills/) when the task touches their area. Do not load unrelated repository content unnecessarily.

## Core invariants

- Treat the repository root as the canonical workspace. Do not reintroduce an `analyzer/` workspace boundary.
- Preserve conservative analysis: prefer `Unknown` over an unsafe or unproven complexity estimate.
- Use Roslyn syntax, symbols, semantic models, and operation identities instead of text-only matching when semantic identity matters.
- Keep analyzer execution deterministic, bounded, cancellation-aware, and safe for concurrent compiler/IDE execution.
- Treat analyzer performance as a functional requirement. Avoid broad scans, I/O, network access, process execution, and heavyweight reflection in analyzer hot paths.
- Preserve analyzer host compatibility. The analyzer project targets `netstandard2.0` unless an explicit architecture decision changes that contract.
- Do not introduce `Microsoft.CodeAnalysis.Workspaces` unless a feature clearly requires workspace APIs.
- Keep Roslyn authoring dependencies private and avoid unnecessary transitive runtime dependencies in the NuGet package.
- Preserve the analyzer package layout with the analyzer assembly under `analyzers/dotnet/cs/` rather than a normal runtime `lib/` asset.
- Do not routinely upgrade the pinned Roslyn compiler API baseline without explicit compatibility evaluation across supported SDK/compiler hosts.

## Tests and public behavior

- Add or update tests for behavior changes and bug fixes. Analyzer diagnostics should normally include triggering and non-triggering coverage.
- Changes to interprocedural analysis, recursion/recurrence solving, caching, budgets, known operations, or package loading must consider compatibility and performance regressions.
- When a public diagnostic changes, update `src/ComplexityAnalysis.Analyzers/AnalyzerReleases.Unshipped.md` when required by analyzer release tracking.
- Keep English and Portuguese public documentation aligned when behavior, configuration, diagnostics, compatibility, or package usage changes.
- New analyzer configuration options must be documented in both `docs/en/configuration.md` and `docs/pt-BR/configuration.md`.

## Validation

Run validation proportional to the change. For analyzer, MSBuild, NuGet, packaging, or broad repository changes, use the root solution:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

When packaging behavior changes, also validate a local package:

```bash
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj \
  --configuration Release \
  --no-build \
  -p:PackageVersion=0.0.0-local \
  --output artifacts/local-packages
```

Use the repository's focused compatibility, package-contract, coverage, and performance tests when relevant to the change.

## Git and release safety

- Use Conventional Commits.
- Keep pull requests focused and review the final diff before concluding.
- Do not commit local `artifacts/`, packages, coverage output, or build output.
- Do not create tags, GitHub Releases, publish NuGet packages, publish GitHub Packages, or otherwise perform a release unless explicitly requested by the user/maintainer.
- Do not weaken CI, security, compatibility, or quality gates merely to make a change pass.

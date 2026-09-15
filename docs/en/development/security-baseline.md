# Repository Security Baseline

The repository uses layered controls so source-code security, dependency risk, code quality, and package compatibility are evaluated independently.

## CodeQL

`.github/workflows/codeql.yml` performs semantic C# security analysis for pull requests targeting `main`, pushes to `main`, a weekly schedule, and manual runs.

The workflow uses advanced setup with manual build mode, the SDK selected by `global.json`, and the same `ComplexityAnalysis.Analyzers.slnx` Release build contract used by CI. Actions are pinned by full commit SHA and the workflow only requests `contents: read` and `security-events: write`.

The initial baseline uses CodeQL's default high-precision queries. Broader query suites can be evaluated later after the initial findings are understood; new noise should not be hidden merely to keep automation green.

## Dependency Review

Dependency Review is a blocking pull-request gate. It rejects newly introduced dependencies with vulnerabilities of `high` severity or above and is intentionally not configured with `continue-on-error`.

This control evaluates the dependency delta of the pull request. It complements, rather than replaces, NuGet auditing, Dependabot, SonarQube Cloud, analyzer/package tests, and CodeQL.

## Dependabot

Dependabot maintains:

- NuGet dependencies, including the repository's explicit Roslyn compatibility constraints;
- the .NET SDK declared by `global.json`;
- GitHub Actions references.

Automated update pull requests must pass the same applicable quality, compatibility, package, performance, and security gates as manual changes.

## Workflow supply-chain policy

Actions changed as part of security-sensitive workflow work should use full commit SHAs with human-readable version comments. Read-only checkouts should disable credential persistence.

Workflow hardening must not rename required jobs casually. The active ruleset depends on stable check names documented in `quality-gates.md`.

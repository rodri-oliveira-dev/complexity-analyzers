# Repository Security Baseline

The repository uses layered controls so source-code security, dependency risk, code quality, and package compatibility are evaluated independently.

## CodeQL

GitHub CodeQL Default Setup is the authoritative source of alerts under **Security > Code scanning** for this repository.

`.github/workflows/codeql.yml` complements Default Setup by validating semantic C# analysis against the repository's explicit build contract for pull requests targeting `main`, pushes to `main`, a weekly schedule, and manual runs. The workflow uses manual build mode, the SDK selected by `global.json`, and the same `ComplexityAnalysis.Analyzers.slnx` Release build contract used by CI.

Because GitHub does not process CodeQL analyses from advanced configurations while Default Setup is enabled, the versioned workflow intentionally does not upload its SARIF to Code Scanning. It runs the standard high-precision query suite, stores the generated SARIF as a short-lived workflow artifact for diagnostics/audit evidence, and keeps workflow permissions read-only with `contents: read` only.

Broader query suites can be evaluated later after the initial findings are understood; new noise should not be hidden merely to keep automation green.

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

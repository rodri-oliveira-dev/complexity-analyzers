# Repository Security Baseline

The repository uses layered controls so source-code security, dependency risk, code quality, and package compatibility are evaluated independently.

## CodeQL

GitHub CodeQL Default Setup is the authoritative source of alerts under **Security > Code scanning** for this repository.

`.github/workflows/codeql.yml` complements Default Setup by validating semantic C# analysis against the repository's explicit build contract for pull requests targeting `main`, pushes to `main`, a weekly schedule, and manual runs. The workflow uses manual build mode, the SDK selected by `global.json`, and the same `ComplexityAnalysis.Analyzers.slnx` Release build contract used by CI.

Because GitHub does not process CodeQL analyses from advanced configurations while Default Setup is enabled, the versioned workflow intentionally does not upload its SARIF to Code Scanning. It runs the standard high-precision query suite, stores the generated SARIF as a short-lived workflow artifact for diagnostics/audit evidence, and keeps workflow permissions read-only with `contents: read` and `security-events: read` only.

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


## Release provenance and SBOM

The official `Release` workflow generates an SPDX 2.3 SBOM from the exact `.nupkg` produced by `build-and-pack`. The package and SBOM are uploaded together as one immutable workflow artifact and the same downloaded `.nupkg` is reused for NuGet.org, GitHub Packages, attestations, and the GitHub Release.

The `github-release` job uses GitHub OIDC with least-privilege attestation permissions to create:

- a build-provenance attestation for the published `.nupkg` and its standalone SPDX SBOM;
- an SPDX SBOM attestation whose subject is the published `.nupkg`.

The standalone `ComplexityAnalysis.Analyzers.<version>.sbom.spdx.json` file is also attached to the GitHub Release.

After downloading a release package, verify its build provenance with GitHub CLI:

```bash
gh attestation verify ComplexityAnalysis.Analyzers.<version>.nupkg \
  --repo rodri-oliveira-dev/complexity-analyzers \
  --signer-workflow rodri-oliveira-dev/complexity-analyzers/.github/workflows/release.yml
```

Verify the SPDX 2.3 SBOM attestation explicitly with:

```bash
gh attestation verify ComplexityAnalysis.Analyzers.<version>.nupkg \
  --repo rodri-oliveira-dev/complexity-analyzers \
  --signer-workflow rodri-oliveira-dev/complexity-analyzers/.github/workflows/release.yml \
  --predicate-type https://spdx.dev/Document/v2.3
```

Attestations prove artifact provenance and bind the SBOM to the package digest. They complement, rather than replace, package tests, CodeQL, Dependency Review, NuGet auditing, Trusted Publishing, and human review.

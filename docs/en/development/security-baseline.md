# Repository Security Baseline

The repository uses layered controls so source-code security, dependency risk, code quality, and package compatibility are evaluated independently.

## CodeQL

The versioned workflow `.github/workflows/codeql.yml` is the intended authoritative CodeQL configuration. It analyzes pull requests targeting `main`, pushes to `main`, a weekly schedule, and manual runs with independent matrix jobs for C#, JavaScript/TypeScript, and GitHub Actions. The C# job uses manual build mode, the SDK selected by `global.json`, and the explicit `ComplexityAnalysis.Analyzers.slnx` Release build contract. JavaScript/TypeScript and GitHub Actions use `build-mode: none`; they do not install .NET or run its build. Matrix fail-fast is disabled so a failed analysis does not cancel the other languages.

It publishes SARIF results to **Security > Code scanning**, where the existing main ruleset enforces its security and alert severity thresholds. Each language publishes a distinct `/language:<language>` category and retains its own SARIF artifact for seven days for diagnostics and audit evidence. The workflow requests `contents: read` and `security-events: write` only. External pull requests use the ordinary `pull_request` event (not `pull_request_target`); GitHub restricts fork tokens and supports Code Scanning uploads for pull request runs. No repository secrets or elevated fork-token settings are required.

**Migration from Default Setup:** a repository administrator must switch off CodeQL Default Setup before validating this configuration; GitHub rejects advanced-setup uploads while Default Setup is enabled. Keep the ruleset enabled throughout the migration. Validate successful Code Scanning publication for all three languages on the migration PR before merging it, then update open PR branches and confirm the Code Scanning gate passes. If publication fails, diagnose the configuration before merging rather than bypassing the ruleset.

Broader query suites can be evaluated later after the initial findings are understood; new noise should not be hidden merely to keep automation green.

## Dependency Review

Dependency Review is a blocking pull-request gate. It rejects newly introduced dependencies with vulnerabilities of `high` severity or above and is intentionally not configured with `continue-on-error`.

This control evaluates the dependency delta of the pull request. It complements, rather than replaces, NuGet auditing, Dependabot, SonarQube Cloud, analyzer/package tests, and CodeQL.

## Dependabot

Dependabot maintains:

- npm dependencies for the documentation portal under `website/`, grouping compatible minor and patch updates while leaving major updates for separate compatibility review;
- NuGet dependencies, including the repository's explicit Roslyn compatibility constraints;
- the .NET SDK declared by `global.json`;
- GitHub Actions references.

Automated update pull requests must pass the same applicable quality, compatibility, package, performance, and security gates as manual changes.

## Workflow supply-chain policy

Actions changed as part of security-sensitive workflow work should use full commit SHAs with human-readable version comments. Read-only checkouts should disable credential persistence.

Workflow hardening must not rename required jobs casually. The active ruleset depends on stable check names documented in `quality-gates.md`.


## Release provenance and SBOM

The `Release` analyzer workflow and the independent `Release
ComplexityAnalysis.Tool` and `Release ComplexityAnalysis.Mcp` workflows generate
an SPDX 2.3 SBOM from the exact `.nupkg` produced by their build jobs. Each
package and SBOM pair is uploaded as one workflow artifact, and the same
downloaded `.nupkg` is reused for NuGet.org, GitHub Packages, attestations, and
the GitHub Release.

The `github-release` job uses GitHub OIDC with least-privilege attestation permissions to create:

- a build-provenance attestation for the published `.nupkg` and its standalone SPDX SBOM;
- an SPDX SBOM attestation whose subject is the published `.nupkg`.

The standalone `ComplexityAnalysis.Analyzers.<version>.sbom.spdx.json` file is also attached to the GitHub Release.

For byte-for-byte verification, use the `.nupkg` attached to the GitHub Release. A package registry may add repository-signing metadata after publication, which can legitimately change the downloaded package bytes.

Verify the GitHub Release package build provenance with GitHub CLI:

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

For the CLI, substitute `ComplexityAnalysis.Tool.<version>.nupkg` and signer
workflow
`rodri-oliveira-dev/complexity-analyzers/.github/workflows/release-tool.yml` in
the verification commands. CLI release tags use the independent
`tool-v<version>` namespace.

For MCP, substitute `ComplexityAnalysis.Mcp.<version>.nupkg` and signer workflow
`rodri-oliveira-dev/complexity-analyzers/.github/workflows/release-mcp.yml`.
MCP release tags use `mcp-v<version>`, and MCP GitHub Releases are not marked as
the repository's generic latest release.

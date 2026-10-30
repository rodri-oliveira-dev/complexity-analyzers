# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

### ComplexityAnalysis.Analyzers 1.6.1

- Refactored deferred LINQ pipeline analysis into focused resolution and
  composition steps without changing diagnostics, complexity estimates,
  conservative `Unknown` propagation, evidence, cancellation, or concurrency
  behavior.
- Preserved the self-contained `netstandard2.0` analyzer package contract and
  existing Roslyn compatibility constraints.

### ComplexityAnalysis.Tool 1.0.1

- Simplified analysis and comparison option parsing while preserving accepted
  arguments, validation messages, exit codes, JSON/SARIF formats, quality
  gates, and independently versioned .NET Tool packaging.
- Refactored Big-O notation parsing into focused syntax handlers and added
  regression coverage for supported and rejected forms.

### ComplexityAnalysis.Mcp 1.0.1

- Simplified startup option parsing and report traversal while preserving tool
  schemas, response contracts, path boundaries, cache isolation, cancellation,
  timeout behavior, and conservative `Unknown` results.
- Kept the checked-in Official MCP Registry manifest on the currently
  published version; the protected release workflow creates and publishes the
  synchronized `1.0.1` manifest only after package publication.

### ComplexityAnalysis.Mcp 1.0.0

- Prepared the first independently versioned public MCP .NET Tool package with
  the `ComplexityAnalysis.Mcp` package ID and `complexity-mcp` command.
- Added agent-oriented schemas and descriptions, deterministic full/summary
  responses, explicit truncation metadata, bounded exact-snapshot reuse through
  `analysisId`, and clearer conservative `Unknown` semantics.
- Hardened root-relative path reporting, invalid-path sanitization, maximum
  startup limits, concurrent request isolation, cancellation/timeout behavior,
  stdout/stderr separation, and `--help` / `--version` handling.
- Added bilingual package and technical documentation for global/local install,
  VS Code / GitHub Copilot, Codex, and Claude Desktop.
- Added a manual, main-only `mcp-v<version>` release workflow with isolated
  package installation, real MCP client calls, SPDX 2.3 SBOM, NuGet Trusted
  Publishing, GitHub Packages, attestations, and product-specific release notes.
- Added an Official MCP Registry manifest and post-release publication using
  GitHub OIDC, a pinned Sigstore-verified publisher, NuGet ownership proof, and
  synchronized immutable versions.

### ComplexityAnalysis.Tool 1.0.0

- Prepared the first independently versioned public .NET Tool release with the
  `ComplexityAnalysis.Tool` package ID and `complexity-analysis` command.
- Added consumer installation and maintenance guidance, built-in `--help` and
  `--version`, a package-specific bilingual README, and expanded package and
  installed-command contracts.
- Added a manual, main-only `tool-v<version>` release workflow with isolated
  package smoke tests, SPDX SBOM, NuGet Trusted Publishing, GitHub Packages,
  provenance/SBOM attestations, and GitHub Release assets.

### Added

- A formal complexity-estimate model with guarantees, assumptions, bounded
  evidence, and conservative `Unknown` propagation.
- Expanded executable-member, collection-cost, deferred LINQ, recursion, and
  loop-safety analysis.
- Deterministic project-level JSON snapshots, evidence queries, asymptotic
  comparison, SARIF 2.1.0 export, and opt-in regression gates.
- A separately packaged `ComplexityAnalysis.Tool` .NET tool.
- A local read-only MCP `stdio` host with allowed-root, workload, timeout, and
  cancellation controls.
- An opt-in CI snapshot regression job with retained JSON evidence and a job
  summary.

### Compatibility

- The Roslyn analyzer remains a self-contained `netstandard2.0` analyzer asset
  under `analyzers/dotnet/cs/`.
- The CLI and MCP host remain separate from the analyzer package and do not add
  runtime dependencies for analyzer consumers.

### Release impact

- `ComplexityAnalysis.Tool` starts its own SemVer line at `1.0.0`; the analyzer
  retains its existing version line and is not republished as `1.0.0`.
- This work prepares publication after merge and approval. It does not publish
  packages, create tags, or perform a release.

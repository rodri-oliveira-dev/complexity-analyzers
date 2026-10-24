# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

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

- These are backward-compatible capabilities suitable for a future minor
  release. This work does not publish packages, create tags, or perform a
  release.

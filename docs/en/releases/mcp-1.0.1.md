# ComplexityAnalysis.Mcp 1.0.1

This prepared patch release improves maintainability without changing the MCP
tool surface or its compatibility contract.

## Maintenance fixes

- Split startup option parsing into focused validation helpers while preserving
  all accepted options, defaults, limits, and error messages.
- Reused one deterministic member traversal for full evidence lookup and
  bounded summaries while preserving member order, counts, truncation, and
  exact-snapshot cache behavior.
- Preserved allowed-root enforcement, symbolic-link protection, request
  isolation, cancellation, timeouts, and conservative `Unknown` results.

## Compatibility

- The package remains a .NET 10 tool with the `complexity-mcp` command.
- The three read-only MCP tools and their request and response schemas are
  unchanged.
- No source, transport, Registry, or package compatibility break is introduced.

## Publication status

Version `1.0.1` is prepared for release after approval and merge. Until the
protected release workflow publishes the package and verifies the Official MCP
Registry entry, installation and discovery documentation continues to identify
the currently published version.

The workflow creates the immutable `mcp-v1.0.1` tag, packages and validates the
tool, produces its SPDX 2.3 SBOM and attestations, publishes the NuGet and GitHub
artifacts, then synchronizes and verifies the versioned Registry manifest. This
preparation does not perform any of those publication steps.

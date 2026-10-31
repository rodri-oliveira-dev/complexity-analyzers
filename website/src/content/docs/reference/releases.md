---
title: Changelog and releases
description: Verify published versions and distinguish repository capability from package availability.
sidebar:
  order: 3
---

Use the repository's [GitHub Releases](https://github.com/rodri-oliveira-dev/complexity-analyzers/releases) and [`CHANGELOG.md`](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CHANGELOG.md) as the release history.

The Analyzer, CLI, and MCP packages are independently consumable and may have distinct publication evidence. A version mentioned in source documentation is not available until its package can be verified on the relevant registry.

## GitHub Action v1.0.0

The independently versioned [Complexity Analysis Action v1.0.0](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0) was published on October 30, 2026. Use the [Action installation guide](https://github.com/rodri-oliveira-dev/complexity-analysis-action#quick-start) to add it to a GitHub Actions workflow, or inspect the [release notes](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0).

The published `@v1.0.0` tag resolves to commit `2d807ef29ca03c685d224543a135d8c30dfe2c67`. For reproducible automation, reference that full commit SHA in `uses:`. The moving `@v1` alias has not been created; do not use it until that reference exists. The GitHub release alone does not establish Marketplace listing visibility.

The Action invokes `ComplexityAnalysis.Tool` and does not replace the separately versioned Analyzer, CLI, or MCP packages.

For MCP `1.0.0` publication details and registry metadata, see the synchronized [release note](/complexity-analyzers/reference/releases/mcp-1-0-0/).

MCP `1.0.1` is prepared but not yet published. Its synchronized [release
preparation note](/complexity-analyzers/reference/releases/mcp-1-0-1/) records
the maintenance scope and the publication steps that remain protected.

This portal is planned for `https://rodri-oliveira-dev.github.io/complexity-analyzers/`. Do not treat that URL as deployed until the GitHub Pages workflow completes successfully.

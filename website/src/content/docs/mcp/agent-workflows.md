---
title: Client configuration and agent workflows
description: Configure VS Code, Codex, and Claude Desktop and use MCP evidence efficiently.
sidebar:
  order: 2
---

Clients start the same installed command with an absolute root:

```json
{
  "command": "complexity-mcp",
  "args": ["--root", "/absolute/path/to/project"]
}
```

## VS Code and GitHub Copilot

Create `.vscode/mcp.json` with a named `stdio` server and `${workspaceFolder}` as the root. Use **MCP: List Servers** to start or inspect it.

## Codex

```bash
codex mcp add complexity-analysis -- complexity-mcp --root /absolute/path/to/project
codex mcp list
```

## Claude Desktop

Add the command under `mcpServers` in `claude_desktop_config.json`, then fully restart Claude Desktop. GUI applications may inherit a different `PATH` than an interactive shell.

## Efficient investigation

For a large project:

1. call `analyze_project` with `responseMode: "summary"`;
2. inspect `isTruncated`, `totalMemberCount`, and the stable member identities;
3. call `query_member_evidence` with the returned `analysisId` and selected `StableId`;
4. ask the agent to distinguish proven estimates, assumptions, and `Unknown` reasons.

The cache is bounded and exact. If an analysis was not retained or was evicted, the server fails explicitly rather than substituting potentially stale data.

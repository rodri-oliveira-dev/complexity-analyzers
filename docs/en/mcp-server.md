# Local MCP Server

[Português (Brasil)](../pt-BR/mcp-server.md)

`ComplexityAnalysis.Mcp` is the separately versioned, local-only stdio surface
for the repository's project analysis. It exposes tools to compatible external
agents; it is not an autonomous agent. It shares report, evidence, StableId,
and comparison code with `ComplexityAnalysis.Tool` and does not add MCP runtime
dependencies to analyzer consumers.

See the [package README](../../src/ComplexityAnalysis.Mcp/README.md) for the
consumer quickstart, global/local installation, maintenance commands, client
configuration, examples, OS-specific PATH guidance, and known limitations.

## Distribution and startup

The independent package identity is `ComplexityAnalysis.Mcp`; version `1.0.0`
installs the `complexity-mcp` .NET 10 command:

```bash
dotnet tool install --global ComplexityAnalysis.Mcp --version 1.0.0
complexity-mcp --root /absolute/path/to/project
```

For a manifest-local installation:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Mcp --version 1.0.0
dotnet tool run complexity-mcp --root /absolute/path/to/project
```

Informational commands exit before protocol startup:

```bash
complexity-mcp --help
complexity-mcp --version
```

The server accepts `--root` (current directory), `--timeout-seconds` (`30`,
range `1..300`), `--max-input-bytes` (50 MiB, range `1..1073741824`), and
`--max-files` (`2000`, range `1..100000`). Invalid configuration returns exit
code `2` and a sanitized message on stderr.

## Protocol contract

All three tools advertise MCP read-only annotations, structured output, input
schemas with parameter descriptions, and output schemas.

| Tool | Inputs | Result |
| --- | --- | --- |
| `analyze_project` | Project/solution path; optional `responseMode` and `maxMembers` | Full deterministic snapshot or explicit bounded summary, plus `analysisId` |
| `query_member_evidence` | Project/solution path, exact `StableId`, optional `analysisId` | Matching member/evidence or `found: false` |
| `compare_snapshots` | Baseline/current snapshot JSON paths | Deterministic comparison report |

The default `analyze_project` behavior remains compatible: `responseMode` is
`full` and `report` contains the complete `complexity-snapshot/1.0`. Summary
mode returns at most 100 member index entries by default (configurable from 1 to
500) and reports `isTruncated`, total, and returned counts. It does not silently
omit evidence while claiming a complete report.

Each analysis hashes the exact serialized snapshot into an `analysisId`. The
process retains at most four snapshots and 16 MiB. Passing the ID to
`query_member_evidence` avoids repeating analysis and explicitly identifies the
source as `cachedSnapshot`. This is snapshot reuse, not a current-files cache:
the server never treats it as proof that the filesystem is unchanged. Omitting
the ID preserves the original fresh-analysis behavior. Missing/evicted IDs fail
with guidance to run `analyze_project` again.

`Unknown` remains an explicit conservative result. Summary entries use
`complexityStatus: "unknown"` and `complexity: null`; member evidence retains a
typed `UnknownReason` when available. Snapshot comparison classifies Unknown
evidence as incomparable, never as a proven regression.

## Security boundary

The host is local, stdio-only, and read-only. It exposes no write, shell,
network, telemetry, remote transport, or arbitrary process tool.

- Every entry file must be below the canonical `--root` boundary and have a
  supported extension.
- Project/solution references and discovered C# files are validated before
  reading.
- Symbolic-link and reparse-point traversal below the root is rejected or
  skipped during enumeration.
- File-count and aggregate-byte scopes are isolated per request.
- Client cancellation and configured timeouts propagate through loading,
  Roslyn analysis, comparison, and serialization.
- Report paths are relative to `--root`; sanitized errors do not echo absolute
  local paths.
- stdout is reserved for JSON-RPC after startup. Console logging is cleared and
  reconfigured to stderr.

The loader parses supported project and solution files directly; it does not
execute MSBuild, restore packages, run builds, or evaluate arbitrary project
tasks. Security checks reduce accidental data exposure but do not turn an
untrusted local repository into trusted code.

## Client configurations

VS Code / GitHub Copilot uses `.vscode/mcp.json` with top-level `servers`:

```json
{
  "servers": {
    "complexity-analysis": {
      "type": "stdio",
      "command": "complexity-mcp",
      "args": ["--root", "${workspaceFolder}"]
    }
  }
}
```

Codex CLI can register the command directly:

```bash
codex mcp add complexity-analysis -- complexity-mcp --root /absolute/path/to/project
codex mcp list
```

Or use `~/.codex/config.toml` / trusted project `.codex/config.toml`:

```toml
[mcp_servers.complexity-analysis]
command = "complexity-mcp"
args = ["--root", "/absolute/path/to/project"]
```

Claude Desktop local developer configuration uses
`claude_desktop_config.json` with top-level `mcpServers`:

```json
{
  "mcpServers": {
    "complexity-analysis": {
      "command": "complexity-mcp",
      "args": ["--root", "/absolute/path/to/project"]
    }
  }
}
```

These formats are based on the official
[VS Code reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration),
[Codex MCP documentation](https://developers.openai.com/codex/extend/mcp), and
[MCP SDK real-host guide](https://py.sdk.modelcontextprotocol.io/get-started/real-host/).

## Reproduce and validate

The equivalent CLI operations are:

```bash
dotnet run --project ./src/ComplexityAnalysis.Tool -- analyze <project-or-solution> --format json --snapshot
dotnet run --project ./src/ComplexityAnalysis.Tool -- compare <baseline.json> <current.json> --format json
```

The release workflow packs one MCP artifact, installs it in an isolated tool
path, and runs the repository's real `ModelContextProtocol.Client` contract
test against that installed command. The test performs protocol initialization,
tool discovery, and real calls to all three tools. It also validates package
metadata, help/version, SPDX 2.3 SBOM generation, and package isolation before
any manual publication job can run.

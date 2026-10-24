# Local MCP Server

`ComplexityAnalysis.Mcp` is a separate, local-only MCP host that exposes
project complexity analysis over standard input/output. It does not change the
Roslyn analyzer package or add MCP dependencies to analyzer consumers.

## Build and configure

Build the repository before starting the server:

```bash
dotnet build ./ComplexityAnalysis.Analyzers.slnx --configuration Release
```

Use the resulting assembly as the MCP client's subprocess. The exact client
configuration format varies, but the command and arguments are equivalent to:

```json
{
  "command": "dotnet",
  "args": [
    "./src/ComplexityAnalysis.Mcp/bin/Release/net10.0/ComplexityAnalysis.Mcp.dll",
    "--root",
    "."
  ]
}
```

The host accepts these startup limits:

- `--root <directory>`: only files below this directory may be read; defaults
  to the current directory.
- `--timeout-seconds <1..300>`: per-request timeout; defaults to `30`.
- `--max-input-bytes <positive integer>`: aggregate file-size limit per
  request; defaults to 50 MiB.
- `--max-files <positive integer>`: file-count limit per request; defaults to
  `2000`.

## Read-only tools

| Tool | Input | Result |
| --- | --- | --- |
| `analyze_project` | A `.csproj`, `.sln`, or `.slnx` path | A typed response whose `report` is a deterministic `complexity-snapshot/1.0` including evidence |
| `query_member_evidence` | Project path and exact `StableId` | The matching member and evidence, or `Found: false` |
| `compare_snapshots` | Baseline and current snapshot `.json` paths | A typed response whose `comparison` is a deterministic `complexity-comparison/1.0` report |

All tools advertise the MCP read-only annotation. The server offers no tool
that writes files, launches arbitrary commands, accesses the network, or
mutates source code. Requests reject paths outside the allowed root,
unsupported extensions, symbolic-link traversal, missing files, and configured
workload-limit violations. Error messages do not echo absolute paths.

Cancellation from the MCP client and the per-request timeout are propagated to
project analysis and snapshot reads. Inconclusive complexity remains
`Unknown`; the MCP layer does not reinterpret or upgrade the CLI result.

## Reproduce results with the CLI

The MCP operations use the same report, evidence, identity, and comparison
implementations as the .NET tool. Reproduce them with:

```bash
dotnet run --project ./src/ComplexityAnalysis.Tool -- analyze <project-or-solution> --format json --snapshot
dotnet run --project ./src/ComplexityAnalysis.Tool -- compare <baseline.json> <current.json> --format json
```

Use the `StableId` in the first JSON report to reproduce a member-evidence
query. The member returned by MCP is the matching member object from that
snapshot.

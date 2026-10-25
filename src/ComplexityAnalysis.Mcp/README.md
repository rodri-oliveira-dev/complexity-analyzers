# ComplexityAnalysis.Mcp

[Português (Brasil)](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/src/ComplexityAnalysis.Mcp/README.pt-BR.md)

<!-- mcp-name: io.github.rodri-oliveira-dev/complexity-analysis -->

`ComplexityAnalysis.Mcp` is a local, read-only MCP server that lets compatible
agents analyze C# project complexity, inspect the evidence for a member, and
compare deterministic snapshots. It exposes analysis tools to an external
agent; it is not an autonomous agent and never edits source code.

## Choose the right product

- `ComplexityAnalysis.Analyzers` runs inside Roslyn builds and IDE analysis.
- `ComplexityAnalysis.Tool` is the `complexity-analysis` CLI for reports,
  snapshots, quality gates, and automation.
- `ComplexityAnalysis.Mcp` is the `complexity-mcp` stdio server for agent tool
  discovery and invocation. It reuses the CLI analysis implementation.

The MCP package installs independently and includes everything needed to run;
it does not require a repository checkout or a prior CLI installation.

## Requirements and installation

.NET 10 runtime is required. The .NET 10 SDK is recommended for local tool
manifests.

After `1.0.0` is published, install globally:

```bash
dotnet tool install --global ComplexityAnalysis.Mcp --version 1.0.0
complexity-mcp --version
```

Or pin it in a local manifest:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Mcp --version 1.0.0
dotnet tool restore
dotnet tool run complexity-mcp --version
```

Update or uninstall a global installation with:

```bash
dotnet tool update --global ComplexityAnalysis.Mcp --version <version>
dotnet tool uninstall --global ComplexityAnalysis.Mcp
```

For a local manifest, omit `--global`; run with `dotnet tool run
complexity-mcp`. A package built from source is not a published release: use an
explicit local source and local-only version when testing it.

## Start the server

MCP clients normally start the process for you. The launch command is:

```bash
complexity-mcp --root /absolute/path/to/project
```

`--root` is the read boundary, not merely a working-directory hint. Use an
absolute path so the same configuration works regardless of the client's
working directory.

| Startup option | Default | Allowed values |
| --- | --- | --- |
| `--root <directory>` | Current directory | Existing directory |
| `--timeout-seconds <n>` | `30` | `1` to `300` |
| `--max-input-bytes <n>` | `52428800` (50 MiB) | `1` to `1073741824` |
| `--max-files <n>` | `2000` | `1` to `100000` |

Use `complexity-mcp --help` for the installed version's options. `--help` and
`--version` print and exit before stdio protocol startup.

## MCP tools

All tools are annotated read-only and return structured content.

### `analyze_project`

Analyzes a `.csproj`, `.sln`, or `.slnx` below `--root`.

- `path` is required.
- `responseMode` is `full` by default and preserves the v1 contract's complete
  snapshot under `report`.
- `responseMode: "summary"` returns a compact member index under `summary`.
- `maxMembers` applies only to summary mode, defaults to `100`, and accepts
  `1..500`.

Summary truncation is never silent: `isTruncated`, `totalMemberCount`, and
`returnedMemberCount` state exactly what was omitted. Every response also
returns a deterministic `analysisId`. A bounded cache retains up to four exact
snapshots and at most 16 MiB in total; `reusable` reports whether the snapshot
was retained.

### `query_member_evidence`

Looks up an exact `StableId` in a project or solution. Without `analysisId`, it
reanalyzes current files for backward compatibility. With an `analysisId` from
`analyze_project`, it reads that immutable cached snapshot and avoids repeating
Roslyn analysis. It fails explicitly if the snapshot was not retained or was
evicted; it never substitutes potentially stale current results.

### `compare_snapshots`

Compares baseline and current `.json` complexity snapshots below `--root`.
Results are deterministic. An `Unknown` estimate is incomparable and is not
promoted to a proven regression.

## Example agent workflow

Ask an agent:

- "Summarize complexity hotspots in `src/App/App.csproj`."
- "Why is member `M:App.Search(System.Int32[])` reported as quadratic?"
- "Compare `baseline.json` with `current.json`; list only proven regressions."
- "Which results are `Unknown`, and what evidence explains each one?"

For large projects, the efficient sequence is:

1. call `analyze_project` with `responseMode: "summary"`;
2. inspect `isTruncated` and the returned member index;
3. call `query_member_evidence` with the returned `analysisId` and a `StableId`.

A shortened response looks like:

```json
{
  "schemaVersion": "complexity-mcp-analysis/1.0",
  "analysisId": "sha256:<digest>",
  "responseMode": "summary",
  "reusable": true,
  "isTruncated": false,
  "totalMemberCount": 1,
  "returnedMemberCount": 1,
  "report": null,
  "summary": {
    "unknownMemberCount": 1,
    "members": [
      {
        "stableId": "M:Sample.Work",
        "complexityStatus": "unknown",
        "complexity": null
      }
    ]
  }
}
```

`Unknown` means the bounded analyzer could not prove a safe estimate. It is not
`O(1)`, an error, or evidence that the method is efficient.

## Client configuration

The common launch values are:

```json
{
  "command": "complexity-mcp",
  "args": ["--root", "/absolute/path/to/project"]
}
```

### VS Code / GitHub Copilot

Create `.vscode/mcp.json`:

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

Then run **MCP: List Servers** and start or inspect the server. See the official
[VS Code MCP configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration).

### Codex CLI and IDE extension

Register the installed stdio command:

```bash
codex mcp add complexity-analysis -- complexity-mcp --root /absolute/path/to/project
codex mcp list
```

Equivalent `~/.codex/config.toml`:

```toml
[mcp_servers.complexity-analysis]
command = "complexity-mcp"
args = ["--root", "/absolute/path/to/project"]
```

Codex CLI and its IDE extension share this configuration. See the official
[Codex MCP documentation](https://developers.openai.com/codex/extend/mcp).

### Claude Desktop

For a local developer-defined server, add this entry to
`claude_desktop_config.json` and fully restart Claude Desktop:

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

On Windows the file is under `%APPDATA%\Claude`; on macOS it is under
`~/Library/Application Support/Claude`. See the official MCP SDK's
[real-host guide](https://py.sdk.modelcontextprotocol.io/get-started/real-host/).

## Windows, macOS, and Linux notes

- Ensure the .NET global tools directory is on the environment `PATH` inherited
  by the client. `dotnet tool list --global` confirms the installation; if the
  client cannot resolve it, configure the absolute `complexity-mcp` executable
  discovered on that machine without committing the machine-specific path.
- JSON Windows paths require escaped backslashes; forward slashes also work in
  .NET paths.
- GUI clients may inherit a different `PATH` from an interactive shell. Restart
  the client after installation or configure the absolute executable path.
- `--root` should be absolute. The process working directory does not expand
  the read boundary.

## Distribution and Official MCP Registry

The three publication channels have different purposes:

- **NuGet.org** hosts the installable .NET tool used by the commands above.
- **GitHub Packages** mirrors the same validated `.nupkg` for consumers of the
  repository package feed.
- The **Official MCP Registry** stores discovery metadata for
  `io.github.rodri-oliveira-dev/complexity-analysis`; its `nuget` package entry
  points clients to `ComplexityAnalysis.Mcp` on NuGet.org. It does not host a
  second executable or turn this server into a remote service.

Registry discovery does not change the local client configurations above:
clients still install or resolve the NuGet tool and start `complexity-mcp` over
stdio with an explicit `--root`.

Each manual `main` release derives the manifest and package versions from the
validated workflow input. After NuGet.org and the GitHub Release succeed, the
workflow waits for the NuGet README containing the ownership marker, validates
the manifest with a fixed, Sigstore-verified `mcp-publisher`, authenticates with
GitHub OIDC, publishes, and queries the Registry API to verify the exact name
and version. Pull-request runs validate the package and versioned manifest but
never authenticate or publish.

To verify a release, search the [Official MCP Registry](https://registry.modelcontextprotocol.io/)
for the full server name, or query its public API:

```bash
curl --get \
  --data-urlencode "search=io.github.rodri-oliveira-dev/complexity-analysis" \
  https://registry.modelcontextprotocol.io/v0.1/servers
```

Troubleshooting:

- **OIDC login fails:** confirm the run is a manual deployment from `main`, the
  protected `release` environment approved it, and `id-token: write` remains on
  the Registry job. No PAT or persistent Registry secret is expected.
- **Manifest validation fails:** run `mcp-publisher validate .mcp/server.json`
  and keep the server name, NuGet identifier, stdio transport, and schema valid.
- **Package is not available:** wait for NuGet.org flat-container and README
  indexing, then rerun the same release workflow. Published package and release
  artifacts remain intact.
- **Version conflict:** Registry and NuGet versions are immutable. A retry is
  accepted only for compatible metadata; otherwise correct the source and use
  a new semantic version instead of attempting to replace the published one.

See the official [package-type specification](https://github.com/modelcontextprotocol/registry/blob/main/docs/modelcontextprotocol-io/package-types.mdx),
[GitHub Actions publishing guide](https://github.com/modelcontextprotocol/registry/blob/main/docs/modelcontextprotocol-io/github-actions.mdx),
and [.NET publishing guide](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/publish-mcp-registry).

## Security and limitations

The server is local, stdio-only, and read-only. It has no HTTP transport,
network calls, telemetry, shell execution, source mutation, or write tools.
It rejects paths outside `--root`, unsupported extensions, traversal through
symbolic links/reparse points, and requests exceeding file, byte, or timeout
limits. Concurrent requests use independent limit and cancellation scopes.
Paths in reports are relative to `--root`; errors do not echo local absolute
paths. stdout is reserved for MCP JSON-RPC and logs go to stderr.

The analysis is conservative and bounded. It does not build the project, load
MSBuild evaluation, execute code, or solve every valid C# program. Configuration
is read from the project and source files supported by the shared CLI loader.

## Project links

- [Technical MCP guide](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/docs/en/mcp-server.md)
- [Repository](https://github.com/rodri-oliveira-dev/complexity-analyzers)
- [Contributing](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CONTRIBUTING.md)
- [MIT License](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/LICENSE)

using System.Reflection;

namespace ComplexityAnalysis.Mcp;

internal static class McpCommandLine
{
    private const string Help = """
        ComplexityAnalysis.Mcp - local read-only MCP server for C# complexity analysis

        Usage:
          complexity-mcp [options]
          complexity-mcp --help
          complexity-mcp --version

        Options:
          --root <directory>          Allowed read root. Default: current directory.
          --timeout-seconds <1..300>  Timeout for each tool request. Default: 30.
          --max-input-bytes <1..1073741824>
                                      Aggregate bytes read per request. Default: 52428800 (50 MiB).
          --max-files <1..100000>     Files read per request. Default: 2000.

        Transport:
          Local stdio only. stdout is reserved for MCP JSON-RPC after server startup;
          logs and configuration errors are written to stderr.
        """;

    internal static bool TryHandle(string[] args, TextWriter output)
    {
        if (args.Length == 1
            && (StringComparer.Ordinal.Equals(args[0], "--help")
                || StringComparer.Ordinal.Equals(args[0], "-h")))
        {
            output.WriteLine(Help);
            return true;
        }

        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--version"))
        {
            output.WriteLine(GetVersion());
            return true;
        }

        return false;
    }

    internal static string GetVersion()
    {
        string informationalVersion = typeof(McpCommandLine).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? "unknown";
        int buildMetadata = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return buildMetadata < 0 ? informationalVersion : informationalVersion[..buildMetadata];
    }
}

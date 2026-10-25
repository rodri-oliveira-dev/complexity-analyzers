using System.Reflection;
using System.Text.Json;
using System.Xml;

using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Project;
using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Tool.Cli;

internal static class ComplexityToolApplication
{
    private const string GeneralHelp = """
        Complexity Analysis .NET Tool

        Usage:
          complexity-analysis analyze <project.csproj|solution.slnx|solution.sln> [options]
          complexity-analysis compare <baseline.json> <current.json> [options]
          complexity-analysis --help
          complexity-analysis --version

        Commands:
          analyze    Analyze a C# project or solution.
          compare    Compare two JSON snapshots.

        Run 'complexity-analysis <command> --help' for command-specific options.
        """;

    private const string AnalyzeHelp = """
        Usage: complexity-analysis analyze <project.csproj|solution.slnx|solution.sln> [options]

        Report options:
          --format <console|json|sarif>      Output format. Default: console.
          --output <path>                    Write the report to a file.
          --include-evidence                 Include bounded evidence in JSON or SARIF.
          --snapshot                         Emit a comparison snapshot in JSON.
          --include-generated                Include generated C# files.
          --include-build-output             Include files under build-output directories.

        Quality gates:
          --max-complexity <value>           Maximum Big-O classification.
          --max-cyclomatic-complexity <n>    Positive integer.
          --max-nesting-depth <n>            Non-negative integer.
          --max-method-nloc <n>              Positive integer.
          --max-statement-count <n>          Positive integer.
          --max-token-count <n>              Positive integer.
          --max-parameters <n>               Non-negative integer.
          --max-cognitive-complexity <n>     Non-negative integer.

        Duplicate detection:
          --detect-duplicates                Enable normalized duplicate detection.
          --min-duplicate-tokens <n>         Positive integer. Default: 40.
          --min-duplicate-lines <n>          Positive integer.
          --max-duplicate-rate <n>           Non-negative percentage.
          --max-duplicate-tokens <n>         Positive integer.

        Big-O values:
          constant, log_n, linear, n, n_log_n, quadratic, n2, cubic, n3,
          exponential, factorial
        """;

    private const string CompareHelp = """
        Usage: complexity-analysis compare <baseline.json> <current.json> [options]

        Options:
          --format <console|json|sarif>      Output format. Default: console.
          --output <path>                    Write the comparison to a file.
          --fail-on-regression               Return exit code 2 for proven regressions.
        """;

    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));
        _ = output ?? throw new ArgumentNullException(nameof(output));
        _ = error ?? throw new ArgumentNullException(nameof(error));

        try
        {
            if (IsHelpRequest(args))
            {
                await output.WriteLineAsync(GetHelp(args));
                return ToolExitCodes.Success;
            }

            if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--version"))
            {
                await output.WriteLineAsync(GetVersion());
                return ToolExitCodes.Success;
            }

            if (args.Length > 0 && StringComparer.Ordinal.Equals(args[0], "compare"))
            {
                return await RunComparisonAsync(args, output, error, cancellationToken);
            }

            if (!ToolOptions.TryParse(args, out ToolOptions? options, out string? parseError)
                || options is null)
            {
                await error.WriteLineAsync(parseError ?? "Invalid command line.");
                return ToolExitCodes.Error;
            }

            ProjectReport report = await ProjectAnalysisRunner.AnalyzeAsync(options, cancellationToken);
            QualityGateEvaluator.Evaluate(report, options);

            string rendered = options.Format switch
            {
                ReportFormat.Json => new JsonReportWriter().Write(report),
                ReportFormat.Sarif => SarifReportWriter.Write(report),
                ReportFormat.Console => new ConsoleReportWriter().Write(report),
                _ => new ConsoleReportWriter().Write(report),
            };

            if (options.OutputPath is null)
            {
                await output.WriteAsync(rendered);
            }
            else
            {
                await File.WriteAllTextAsync(options.OutputPath, rendered, cancellationToken);
            }

            return report.QualityGates.Exists(gate => !gate.Passed)
                ? ToolExitCodes.QualityGateFailed
                : ToolExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Analysis was canceled.");
            return ToolExitCodes.Canceled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or XmlException or JsonException)
        {
            await error.WriteLineAsync(ex.Message);
            return ToolExitCodes.Error;
        }
    }

    private static bool IsHelpRequest(string[] args)
    {
        return (args.Length == 1 && IsHelpOption(args[0]))
            || (args.Length == 2
            && (StringComparer.Ordinal.Equals(args[0], "analyze")
                || StringComparer.Ordinal.Equals(args[0], "compare"))
            && IsHelpOption(args[1]));
    }

    private static bool IsHelpOption(string value)
    {
        return StringComparer.Ordinal.Equals(value, "--help")
            || StringComparer.Ordinal.Equals(value, "-h");
    }

    private static string GetHelp(string[] args)
    {
        return args.Length == 2 && StringComparer.Ordinal.Equals(args[0], "analyze")
            ? AnalyzeHelp
            : args.Length == 2 && StringComparer.Ordinal.Equals(args[0], "compare")
            ? CompareHelp
            : GeneralHelp;
    }

    private static string GetVersion()
    {
        string informationalVersion = typeof(ComplexityToolApplication).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? "unknown";
        int buildMetadata = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return buildMetadata < 0 ? informationalVersion : informationalVersion[..buildMetadata];
    }

    private static async Task<int> RunComparisonAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!ComparisonOptions.TryParse(args, out ComparisonOptions? options, out string? parseError)
            || options is null)
        {
            await error.WriteLineAsync(parseError ?? "Invalid comparison command line.");
            return ToolExitCodes.Error;
        }

        ProjectReport baseline = await SnapshotReader.ReadAsync(options.BaselinePath, cancellationToken);
        ProjectReport current = await SnapshotReader.ReadAsync(options.CurrentPath, cancellationToken);
        ComplexityComparisonReport comparison = ComplexityRegressionComparer.Compare(
            baseline,
            current,
            cancellationToken);
        string rendered = options.Format switch
        {
            ReportFormat.Json => ComparisonReportWriter.WriteJson(comparison),
            ReportFormat.Sarif => SarifReportWriter.Write(comparison),
            ReportFormat.Console => ComparisonReportWriter.WriteConsole(comparison),
            _ => ComparisonReportWriter.WriteConsole(comparison),
        };

        if (options.OutputPath is null)
        {
            await output.WriteAsync(rendered);
        }
        else
        {
            await File.WriteAllTextAsync(options.OutputPath, rendered, cancellationToken);
        }

        return options.FailOnRegression
            && comparison.Findings.Exists(finding => StringComparer.Ordinal.Equals(finding.Classification, "regression"))
            ? ToolExitCodes.QualityGateFailed
            : ToolExitCodes.Success;
    }
}

using System.Text.Json;
using System.Xml;

using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Project;
using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Tool.Cli;

internal static class ComplexityToolApplication
{
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

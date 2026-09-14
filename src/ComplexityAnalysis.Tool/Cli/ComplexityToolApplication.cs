using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

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
            if (!ToolOptions.TryParse(args, out ToolOptions? options, out string? parseError)
                || options is null)
            {
                await error.WriteLineAsync(parseError ?? "Invalid command line.");
                return ToolExitCodes.Error;
            }

            ProjectReport report = await ProjectAnalysisRunner.AnalyzeAsync(options, cancellationToken);
            QualityGateEvaluator.Evaluate(report, options);

            IReportWriter writer = options.Format == ReportFormat.Json
                ? new JsonReportWriter()
                : new ConsoleReportWriter();
            string rendered = writer.Write(report);

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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or XmlException)
        {
            await error.WriteLineAsync(ex.Message);
            return ToolExitCodes.Error;
        }
    }
}

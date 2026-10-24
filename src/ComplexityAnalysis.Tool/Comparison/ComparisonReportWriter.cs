using System.Text;
using System.Text.Json;

namespace ComplexityAnalysis.Tool.Comparison;

internal static class ComparisonReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static string WriteJson(ComplexityComparisonReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions) + "\n";
    }

    internal static string WriteConsole(ComplexityComparisonReport report)
    {
        StringBuilder builder = new();
        _ = builder.AppendLine("Complexity Comparison Report");
        _ = builder.AppendLine("Baseline schema: " + report.BaselineSchemaVersion);
        _ = builder.AppendLine("Current schema: " + report.CurrentSchemaVersion);
        foreach (ComplexityComparisonFinding finding in report.Findings)
        {
            _ = builder.Append(finding.Classification.ToUpperInvariant());
            _ = builder.Append(' ');
            _ = builder.Append(finding.StableId);
            if (finding.Before is not null || finding.After is not null)
            {
                _ = builder.Append(" before=");
                _ = builder.Append(finding.Before ?? "Unknown");
                _ = builder.Append(" after=");
                _ = builder.Append(finding.After ?? "Unknown");
            }

            _ = builder.Append(" reason=");
            _ = builder.AppendLine(finding.Reason);
        }

        return builder.ToString();
    }
}

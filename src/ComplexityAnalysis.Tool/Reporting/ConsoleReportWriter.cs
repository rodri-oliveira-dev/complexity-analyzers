using System.Globalization;
using System.Text;

namespace ComplexityAnalysis.Tool.Reporting;

internal sealed class ConsoleReportWriter : IReportWriter
{
    public string Write(ProjectReport report)
    {
        StringBuilder builder = new();
        _ = builder.AppendLine("Complexity Analysis Report");
        _ = builder.AppendLine("Entry: " + report.EntryPoint);
        _ = builder.AppendLine("Projects: " + report.Projects.Count.ToString(CultureInfo.InvariantCulture));

        foreach (AnalyzedProjectReport project in report.Projects)
        {
            _ = builder.AppendLine();
            _ = builder.AppendLine("Project: " + project.Name + " (" + project.Path + ")");
            foreach (AnalyzedFileReport file in project.Files)
            {
                _ = builder.AppendLine("  File: " + file.Path);
                foreach (MemberReport member in file.Members)
                {
                    _ = builder.Append("    ");
                    _ = builder.Append(member.Kind);
                    _ = builder.Append(' ');
                    _ = builder.Append(member.DisplayName);
                    _ = builder.Append(" [");
                    _ = builder.Append(member.Location.StartLine.ToString(CultureInfo.InvariantCulture));
                    _ = builder.Append(':');
                    _ = builder.Append(member.Location.StartColumn.ToString(CultureInfo.InvariantCulture));
                    _ = builder.Append("] BigO=");
                    _ = builder.Append(member.BigO.Value ?? "Unknown");
                    _ = builder.Append(" CC=");
                    _ = builder.Append(Format(member.CyclomaticComplexity));
                    _ = builder.Append(" Nesting=");
                    _ = builder.Append(Format(member.MaximumNestingDepth));
                    _ = builder.Append(" NLOC=");
                    _ = builder.Append(Format(member.Nloc));
                    _ = builder.Append(" Statements=");
                    _ = builder.Append(Format(member.StatementCount));
                    _ = builder.Append(" Tokens=");
                    _ = builder.Append(Format(member.TokenCount));
                    _ = builder.Append(" Parameters=");
                    _ = builder.Append(Format(member.ParameterCount));
                    _ = builder.Append(" Cognitive=");
                    _ = builder.Append(Format(member.CognitiveComplexity));
                    _ = builder.AppendLine();
                }
            }
        }

        if (report.Duplicates.Enabled)
        {
            _ = builder.AppendLine();
            _ = builder.AppendLine(
                "Duplicates: "
                + report.Duplicates.CloneGroups.Count.ToString(CultureInfo.InvariantCulture)
                + " groups, rate "
                + Format(report.Duplicates.DuplicateRate));
            foreach (CloneGroupReport group in report.Duplicates.CloneGroups)
            {
                _ = builder.AppendLine(
                    "  "
                    + group.Id
                    + ": "
                    + group.NormalizedTokenCount.ToString(CultureInfo.InvariantCulture)
                    + " tokens, "
                    + group.OccurrenceCount.ToString(CultureInfo.InvariantCulture)
                    + " occurrences");
                foreach (CloneOccurrenceReport occurrence in group.Occurrences)
                {
                    _ = builder.AppendLine(
                        "    "
                        + occurrence.FilePath
                        + ":"
                        + occurrence.Location.StartLine.ToString(CultureInfo.InvariantCulture)
                        + ":"
                        + occurrence.Location.StartColumn.ToString(CultureInfo.InvariantCulture)
                        + "-"
                        + occurrence.Location.EndLine.ToString(CultureInfo.InvariantCulture)
                        + ":"
                        + occurrence.Location.EndColumn.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        if (report.QualityGates.Count > 0)
        {
            _ = builder.AppendLine();
            _ = builder.AppendLine("Quality Gates:");
            foreach (QualityGateReport gate in report.QualityGates)
            {
                _ = builder.AppendLine(
                    "  "
                    + (gate.Passed ? "PASS" : "FAIL")
                    + " "
                    + gate.Metric
                    + " "
                    + gate.Subject
                    + " actual="
                    + gate.Actual
                    + " threshold="
                    + gate.Threshold);
            }
        }

        return builder.ToString();
    }

    private static string Format(MetricReport<int> metric)
    {
        return metric.Value?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
    }

    private static string Format(MetricReport<double> metric)
    {
        return metric.Value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "Unknown";
    }
}

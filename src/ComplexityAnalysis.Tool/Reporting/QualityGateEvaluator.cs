using System;
using System.Globalization;
using System.Linq;

using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;
using ComplexityAnalysis.Tool.Cli;

namespace ComplexityAnalysis.Tool.Reporting;

internal static class QualityGateEvaluator
{
    internal static void Evaluate(ProjectReport report, ToolOptions options)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        foreach (AnalyzedProjectReport project in report.Projects)
        {
            foreach (AnalyzedFileReport file in project.Files)
            {
                foreach (MemberReport member in file.Members)
                {
                    EvaluateMember(report, member, options);
                }
            }
        }

        if (options.MaximumDuplicateRate is { } maximumDuplicateRate
            && report.Duplicates.DuplicateRate.Value is { } duplicateRate)
        {
            AddNumericGate(
                report,
                "duplicateRate",
                "project",
                duplicateRate,
                maximumDuplicateRate);
        }

        if (options.MaximumDuplicateTokens is { } maximumDuplicateTokens)
        {
            AddNumericGate(
                report,
                "duplicateTokens",
                "project",
                report.Duplicates.DuplicateTokenCount,
                maximumDuplicateTokens);
        }

        report.QualityGates.Sort((left, right) =>
        {
            int metric = StringComparer.Ordinal.Compare(left.Metric, right.Metric);
            if (metric != 0)
            {
                return metric;
            }

            return StringComparer.Ordinal.Compare(left.Subject, right.Subject);
        });
    }

    private static void EvaluateMember(ProjectReport report, MemberReport member, ToolOptions options)
    {
        if (options.MaximumComplexity is { } maximumComplexity)
        {
            EvaluateBigOGate(report, member, maximumComplexity);
        }

        AddMetricGate(report, "cyclomaticComplexity", member.DisplayName, member.CyclomaticComplexity, options.MaximumCyclomaticComplexity);
        AddMetricGate(report, "maximumNestingDepth", member.DisplayName, member.MaximumNestingDepth, options.MaximumNestingDepth);
        AddMetricGate(report, "nloc", member.DisplayName, member.Nloc, options.MaximumMethodNloc);
        AddMetricGate(report, "statementCount", member.DisplayName, member.StatementCount, options.MaximumStatementCount);
        AddMetricGate(report, "tokenCount", member.DisplayName, member.TokenCount, options.MaximumTokenCount);
        AddMetricGate(report, "parameterCount", member.DisplayName, member.ParameterCount, options.MaximumParameters);
        AddMetricGate(report, "cognitiveComplexity", member.DisplayName, member.CognitiveComplexity, options.MaximumCognitiveComplexity);
    }

    private static void EvaluateBigOGate(ProjectReport report, MemberReport member, string maximumComplexity)
    {
        if (member.BigO.IsUnknown || member.BigO.Value is null)
        {
            return;
        }

        ComplexityThreshold threshold = ParseThreshold(maximumComplexity);
        if (!threshold.TryCreateExpression(out ComplexityExpression thresholdExpression))
        {
            throw new InvalidOperationException("Unsupported complexity threshold: " + maximumComplexity);
        }

        ComplexityExpression? actualExpression = ParseKnownComplexity(member.BigO.Value);
        if (actualExpression is null)
        {
            return;
        }

        GrowthComparison comparison = ComplexityGrowthComparer.Compare(actualExpression, thresholdExpression);
        if (comparison == GrowthComparison.Incomparable)
        {
            return;
        }

        report.QualityGates.Add(new QualityGateReport
        {
            Metric = "bigO",
            Subject = member.DisplayName,
            Threshold = threshold.ToString(),
            Actual = member.BigO.Value,
            Passed = comparison is GrowthComparison.Less or GrowthComparison.Equivalent,
        });
    }

    private static ComplexityThreshold ParseThreshold(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "constant" => ComplexityThreshold.Constant,
            "log_n" => ComplexityThreshold.LogN,
            "linear" or "n" => ComplexityThreshold.Linear,
            "n_log_n" => ComplexityThreshold.NLogN,
            "quadratic" or "n2" => ComplexityThreshold.Quadratic,
            "cubic" or "n3" => ComplexityThreshold.Cubic,
            "exponential" => ComplexityThreshold.Exponential,
            "factorial" => ComplexityThreshold.Factorial,
            _ => ComplexityThreshold.None,
        };
    }

    private static ComplexityExpression? ParseKnownComplexity(string value)
    {
        return value switch
        {
            "O(1)" => ComplexityFactory.Constant(),
            "O(log n)" => ComplexityFactory.LogN(ComplexityVariable.N),
            "O(n)" => ComplexityFactory.Linear(ComplexityVariable.N),
            "O(n log n)" => ComplexityFactory.NLogN(ComplexityVariable.N),
            "O(n^2)" or "O(n²)" => ComplexityFactory.Polynomial(ComplexityVariable.N, 2),
            "O(n^3)" or "O(n³)" => ComplexityFactory.Polynomial(ComplexityVariable.N, 3),
            "O(2^n)" => ComplexityFactory.Exponential(ComplexityVariable.N, 2),
            "O(n!)" => ComplexityFactory.Factorial(ComplexityVariable.N),
            _ => null,
        };
    }

    private static void AddMetricGate(
        ProjectReport report,
        string metric,
        string subject,
        MetricReport<int> actual,
        int? threshold)
    {
        if (threshold is null || actual.Value is null)
        {
            return;
        }

        AddNumericGate(report, metric, subject, actual.Value.Value, threshold.Value);
    }

    private static void AddNumericGate(
        ProjectReport report,
        string metric,
        string subject,
        double actual,
        double threshold)
    {
        report.QualityGates.Add(new QualityGateReport
        {
            Metric = metric,
            Subject = subject,
            Threshold = threshold.ToString(CultureInfo.InvariantCulture),
            Actual = actual.ToString(CultureInfo.InvariantCulture),
            Passed = actual <= threshold,
        });
    }

    private static void AddNumericGate(
        ProjectReport report,
        string metric,
        string subject,
        int actual,
        int threshold)
    {
        report.QualityGates.Add(new QualityGateReport
        {
            Metric = metric,
            Subject = subject,
            Threshold = threshold.ToString(CultureInfo.InvariantCulture),
            Actual = actual.ToString(CultureInfo.InvariantCulture),
            Passed = actual <= threshold,
        });
    }
}

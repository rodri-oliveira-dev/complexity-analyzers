using ComplexityAnalysis.Analyzers.Model;
using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Tool.Comparison;

internal static class ComplexityRegressionComparer
{
    internal static ComplexityComparisonReport Compare(
        ProjectReport baseline,
        ProjectReport current,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SnapshotMatchResult matches = SnapshotMemberMatcher.Match(baseline, current, cancellationToken);
        ComplexityComparisonReport report = new()
        {
            BaselineSchemaVersion = baseline.SchemaVersion,
            CurrentSchemaVersion = current.SchemaVersion,
        };

        bool configurationCompatible = AreConfigurationsCompatible(baseline.Configuration, current.Configuration);
        foreach (SnapshotMemberMatch match in matches.Matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            report.Findings.Add(CompareMatch(match, configurationCompatible));
        }

        foreach (MemberReport member in matches.Added)
        {
            cancellationToken.ThrowIfCancellationRequested();
            report.Findings.Add(CreateUnmatched("added", member));
        }

        foreach (MemberReport member in matches.Removed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            report.Findings.Add(CreateUnmatched("removed", member));
        }

        foreach (string id in matches.AmbiguousIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            report.Findings.Add(new ComplexityComparisonFinding
            {
                StableId = id,
                Classification = "ambiguous",
                Severity = "note",
                Reason = "The stable identity occurs more than once in a snapshot.",
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        report.Findings.Sort((left, right) => StringComparer.Ordinal.Compare(left.StableId, right.StableId));
        return report;
    }

    private static ComplexityComparisonFinding CompareMatch(SnapshotMemberMatch match, bool configurationCompatible)
    {
        ComplexityComparisonFinding finding = new()
        {
            StableId = match.StableId,
            Before = match.Before.BigO.Value,
            After = match.After.BigO.Value,
            Location = match.After.Location,
            Evidence = match.After.Evidence,
        };

        if (!configurationCompatible)
        {
            return finding.AsIncomparable("Snapshot configurations differ.");
        }

        if (match.Before.BigO.IsUnknown || match.After.BigO.IsUnknown)
        {
            return finding.AsIncomparable("At least one estimate is Unknown.");
        }

        return !TryGetContract(match.Before, out string beforeContract)
            || !TryGetContract(match.After, out string afterContract)
            || !StringComparer.Ordinal.Equals(beforeContract, afterContract)
            ? finding.AsIncomparable("Guarantees or cost assumptions differ.")
            : !BigONotationParser.TryParse(match.Before.BigO.Value, out ComplexityExpression before)
            || !BigONotationParser.TryParse(match.After.BigO.Value, out ComplexityExpression after)
            ? finding.AsIncomparable("The estimates do not have a supported total ordering.")
            : ComplexityGrowthComparer.Compare(before, after) switch
            {
                GrowthComparison.Less => finding.With("regression", "warning", "The current estimate grows faster than the baseline."),
                GrowthComparison.Greater => finding.With("improvement", "note", "The current estimate grows slower than the baseline."),
                GrowthComparison.Equivalent => finding.With("equivalent", "note", "The estimates have equivalent asymptotic growth."),
                GrowthComparison.Incomparable => finding.AsIncomparable("The estimates use independent input dimensions."),
                _ => finding.AsIncomparable("The estimates use independent input dimensions."),
            };
    }

    private static bool TryGetContract(MemberReport member, out string contract)
    {
        if (member.Evidence is null
            || member.Evidence.Steps.Count == 0
            || member.Evidence.Steps.Any(step => !StringComparer.Ordinal.Equals(step.Estimate.Status, "known")))
        {
            contract = string.Empty;
            return false;
        }

        string[] guarantees =
        [
            .. member.Evidence.Steps
                .Select(step => step.Estimate.Guarantee)
                .Distinct(StringComparer.Ordinal)
        ];
        if (guarantees.Length != 1)
        {
            contract = string.Empty;
            return false;
        }

        contract = guarantees[0] + "|" + string.Join(
            ";",
            member.Evidence.Steps
                .SelectMany(step => step.Estimate.Assumptions)
                .OrderBy(assumption => assumption.Kind, StringComparer.Ordinal)
                .ThenBy(assumption => assumption.Description, StringComparer.Ordinal)
                .Select(assumption => assumption.Kind + ":" + assumption.Description)
                .Distinct(StringComparer.Ordinal));
        return true;
    }

    private static bool AreConfigurationsCompatible(SnapshotConfigurationReport? before, SnapshotConfigurationReport? after)
    {
        return before is null || after is null
            ? before is null && after is null
            : before.IncludeGenerated == after.IncludeGenerated
            && before.IncludeBuildOutput == after.IncludeBuildOutput
            && before.IncludeEvidence == after.IncludeEvidence
            && before.DetectDuplicates == after.DetectDuplicates
            && before.MinimumDuplicateTokens == after.MinimumDuplicateTokens
            && before.MinimumDuplicateLines == after.MinimumDuplicateLines;
    }

    private static ComplexityComparisonFinding CreateUnmatched(string classification, MemberReport member)
    {
        return new()
        {
            StableId = member.StableId ?? member.SymbolId,
            Classification = classification,
            Severity = "note",
            Reason = classification == "added" ? "No baseline member has this identity." : "No current member has this identity.",
            Before = classification == "removed" ? member.BigO.Value : null,
            After = classification == "added" ? member.BigO.Value : null,
            Location = member.Location,
        };
    }
}

internal sealed class ComplexityComparisonReport
{
    public string SchemaVersion { get; init; } = "complexity-comparison/1.0";

    public required string BaselineSchemaVersion
    {
        get; init;
    }

    public required string CurrentSchemaVersion
    {
        get; init;
    }

    public List<ComplexityComparisonFinding> Findings { get; } = [];
}

internal sealed class ComplexityComparisonFinding
{
    public required string StableId
    {
        get; init;
    }

    public string Classification { get; set; } = "incomparable";

    public string Severity { get; set; } = "note";

    public string Reason { get; set; } = string.Empty;

    public string? Before
    {
        get; init;
    }

    public string? After
    {
        get; init;
    }

    public SourceLocationReport? Location
    {
        get; init;
    }

    public ComplexityEvidenceReport? Evidence
    {
        get; init;
    }

    internal ComplexityComparisonFinding AsIncomparable(string reason)
    {
        return With("incomparable", "note", reason);
    }

    internal ComplexityComparisonFinding With(string classification, string severity, string reason)
    {
        Classification = classification;
        Severity = severity;
        Reason = reason;
        return this;
    }
}

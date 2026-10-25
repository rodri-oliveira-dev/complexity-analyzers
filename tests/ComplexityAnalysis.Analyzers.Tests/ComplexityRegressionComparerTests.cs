using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Reporting;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityRegressionComparerTests
{
    [Theory]
    [InlineData("O(n)", "O(n²)", "regression")]
    [InlineData("O(n²)", "O(n)", "improvement")]
    [InlineData("O(n)", "O(n)", "equivalent")]
    [InlineData("O(n^1)", "O(n log n)", "regression")]
    [InlineData("O(n)", "O(m)", "incomparable")]
    public void Compare_classifies_supported_growth_conservatively(string before, string after, string expected)
    {
        ComplexityComparisonFinding finding = Assert.Single(ComplexityRegressionComparer.Compare(
            Report(Member("M:Sample.Work", before)),
            Report(Member("M:Sample.Work", after))).Findings);

        Assert.Equal(expected, finding.Classification);
        Assert.Equal(expected == "regression" ? "warning" : "note", finding.Severity);
    }

    [Fact]
    public void Unknown_never_becomes_a_regression()
    {
        ComplexityComparisonFinding finding = Assert.Single(ComplexityRegressionComparer.Compare(
            Report(Member("M:Sample.Work", null)),
            Report(Member("M:Sample.Work", "O(n²)"))).Findings);

        Assert.Equal("incomparable", finding.Classification);
        Assert.Contains("Unknown", finding.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("O(0^n)")]
    [InlineData("O(1^n)")]
    [InlineData("O(n^-1)")]
    [InlineData("O(n^NaN)")]
    [InlineData("O(foo bar)")]
    [InlineData("O()")]
    public void Unsupported_or_out_of_domain_notation_is_incomparable(string notation)
    {
        ComplexityComparisonFinding finding = Assert.Single(ComplexityRegressionComparer.Compare(
            Report(Member("M:Sample.Work", notation)),
            Report(Member("M:Sample.Work", "O(n)"))).Findings);

        Assert.Equal("incomparable", finding.Classification);
    }

    [Fact]
    public void Zero_polynomial_degree_is_compared_as_constant()
    {
        ComplexityComparisonFinding finding = Assert.Single(ComplexityRegressionComparer.Compare(
            Report(Member("M:Sample.Work", "O(n^0)")),
            Report(Member("M:Sample.Work", "O(1)"))).Findings);

        Assert.Equal("equivalent", finding.Classification);
    }

    [Fact]
    public void Incompatible_assumptions_and_configuration_never_become_regressions()
    {
        MemberReport before = Member("M:Sample.Work", "O(n)", "uniform input");
        MemberReport after = Member("M:Sample.Work", "O(n²)", "adversarial input");
        ComplexityComparisonFinding assumptionFinding = Assert.Single(ComplexityRegressionComparer.Compare(
            Report(before), Report(after)).Findings);

        ProjectReport configuredBefore = Report(Member("M:Sample.Work", "O(n)"));
        ProjectReport configuredAfter = Report(Member("M:Sample.Work", "O(n²)"), includeGenerated: true);
        ComplexityComparisonFinding configurationFinding = Assert.Single(
            ComplexityRegressionComparer.Compare(configuredBefore, configuredAfter).Findings);

        Assert.Equal("incomparable", assumptionFinding.Classification);
        Assert.Equal("incomparable", configurationFinding.Classification);
    }

    [Fact]
    public void Missing_or_mixed_evidence_contracts_are_incomparable()
    {
        MemberReport missingEvidence = Member("M:Sample.Work", "O(n)", includeEvidence: false);
        MemberReport mixedGuarantees = Member("M:Sample.Work", "O(nÂ²)");
        mixedGuarantees.Evidence!.Steps.Add(new ComplexityEvidenceStepReport
        {
            Identity = "amortized",
            Kind = "knownOperation",
            Origin = "knownOperationRegistry",
            Operation = "list-add",
            Location = new ComplexityEvidenceLocationReport(),
            Estimate = new ComplexityEstimateReport
            {
                Status = "known",
                Value = "O(1)",
                Guarantee = "amortized",
            },
        });

        ComplexityComparisonFinding missing = Assert.Single(ComplexityRegressionComparer.Compare(
            Report(missingEvidence),
            Report(Member("M:Sample.Work", "O(nÂ²)"))).Findings);
        ComplexityComparisonFinding mixed = Assert.Single(ComplexityRegressionComparer.Compare(
            Report(Member("M:Sample.Work", "O(n)")),
            Report(mixedGuarantees)).Findings);

        Assert.Equal("incomparable", missing.Classification);
        Assert.Equal("incomparable", mixed.Classification);
    }

    [Fact]
    public void Findings_are_deterministically_ordered_and_keep_unmatched_states()
    {
        ComplexityComparisonReport comparison = ComplexityRegressionComparer.Compare(
            Report(Member("M:Sample.Z", "O(n)"), Member("M:Sample.A", "O(n)")),
            Report(Member("M:Sample.Z", "O(n²)"), Member("M:Sample.B", "O(1)")));

        Assert.Equal(["M:Sample.A", "M:Sample.B", "M:Sample.Z"], comparison.Findings.Select(finding => finding.StableId));
        Assert.Equal(["removed", "added", "regression"], comparison.Findings.Select(finding => finding.Classification));
    }

    [Fact]
    public void Identical_member_ids_in_different_projects_do_not_match()
    {
        SnapshotMatchResult result = SnapshotMemberMatcher.Match(
            Report(Member("M:Shared.Work", "O(n)"), projectPath: "src/First.csproj"),
            Report(Member("M:Shared.Work", "O(n)"), projectPath: "src/Second.csproj"));

        Assert.Empty(result.Matches);
        _ = Assert.Single(result.Removed);
        _ = Assert.Single(result.Added);
        Assert.Empty(result.AmbiguousIds);
    }

    private static ProjectReport Report(
        MemberReport first,
        MemberReport? second = null,
        bool includeGenerated = false,
        string projectPath = "Sample.csproj")
    {
        ProjectReport report = new()
        {
            SchemaVersion = "complexity-snapshot/1.0",
            EntryPoint = "sample.slnx",
            Configuration = new SnapshotConfigurationReport
            {
                IncludeEvidence = true,
                IncludeGenerated = includeGenerated,
                MinimumDuplicateTokens = 40,
            },
        };
        AnalyzedProjectReport project = new()
        {
            Name = "Sample",
            Path = projectPath
        };
        AnalyzedFileReport file = new()
        {
            Path = "Sample.cs"
        };
        file.Members.Add(first);
        if (second is not null)
        {
            file.Members.Add(second);
        }

        project.Files.Add(file);
        report.Projects.Add(project);
        return report;
    }

    private static MemberReport Member(
        string id,
        string? bigO,
        string assumption = "same",
        bool includeEvidence = true)
    {
        ComplexityEvidenceReport evidence = new()
        {
            MaximumDepth = 1,
            MaximumSteps = 1
        };
        ComplexityEvidenceStepReport step = new()
        {
            Identity = "root",
            Kind = "analysis",
            Origin = "syntax",
            Operation = "method",
            Location = new ComplexityEvidenceLocationReport(),
            Estimate = new ComplexityEstimateReport
            {
                Status = bigO is null ? "unknown" : "known",
                Value = bigO,
                Guarantee = bigO is null ? "unknown" : "expected",
            },
        };
        if (bigO is not null)
        {
            step.Estimate.Assumptions.Add(new ComplexityAssumptionReport
            {
                Kind = "inputDistribution",
                Description = assumption,
            });
        }

        evidence.Steps.Add(step);
        return new MemberReport
        {
            StableId = id,
            SymbolId = id,
            DisplayName = id,
            Kind = "Method",
            Location = new SourceLocationReport { FilePath = "Sample.cs", StartLine = 1, StartColumn = 1, EndLine = 1, EndColumn = 2, Length = 1 },
            BigO = bigO is null ? BigOReport.Unknown() : BigOReport.Known(bigO),
            Evidence = includeEvidence ? evidence : null,
        };
    }
}

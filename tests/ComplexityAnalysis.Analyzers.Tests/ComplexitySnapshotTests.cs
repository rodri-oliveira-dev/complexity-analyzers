using System.Text.Json;

using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Reporting;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexitySnapshotTests
{
    [Fact]
    public async Task Snapshot_is_versioned_normalized_and_deterministic()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        string project = Path.Combine(root, "samples", "ComplexityAnalysis.Sample", "ComplexityAnalysis.Sample.csproj");
        (int firstCode, string first, string firstError) = await RunAsync("analyze", project, "--format", "json", "--snapshot");
        (int secondCode, string second, _) = await RunAsync("analyze", project, "--format", "json", "--snapshot");

        Assert.Equal(ToolExitCodes.Success, firstCode);
        Assert.Equal(ToolExitCodes.Success, secondCode);
        Assert.Equal(string.Empty, firstError);
        Assert.Equal(first, second);
        using JsonDocument document = JsonDocument.Parse(first);
        JsonElement report = document.RootElement;
        Assert.Equal("complexity-snapshot/1.0", report.GetProperty("SchemaVersion").GetString());
        Assert.Equal("projectAnalysis", report.GetProperty("Origin").GetProperty("Kind").GetString());
        Assert.True(report.GetProperty("Configuration").GetProperty("IncludeEvidence").GetBoolean());
        Assert.False(Path.IsPathFullyQualified(report.GetProperty("EntryPoint").GetString()!));
        JsonElement member = report.GetProperty("Projects")[0].GetProperty("Files")[0].GetProperty("Members")[0];
        Assert.StartsWith("M:", member.GetProperty("StableId").GetString(), StringComparison.Ordinal);
        Assert.True(member.TryGetProperty("Evidence", out _));
    }

    [Fact]
    public void Matching_uses_symbol_identity_across_file_and_line_changes()
    {
        SnapshotMatchResult result = SnapshotMemberMatcher.Match(
            Report(Member("M:Sample.Work(System.Int32)", "Old.cs", 10)),
            Report(Member("M:Sample.Work(System.Int32)", "Moved.cs", 80)));
        _ = Assert.Single(result.Matches);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        Assert.Empty(result.AmbiguousIds);
    }

    [Fact]
    public void Matching_distinguishes_overloads_and_treats_rename_as_add_remove()
    {
        SnapshotMatchResult result = SnapshotMemberMatcher.Match(
            Report(Member("M:Sample.Work(System.Int32)", "A.cs", 1), Member("M:Sample.Work(System.String)", "A.cs", 2)),
            Report(Member("M:Sample.Renamed(System.Int32)", "B.cs", 4), Member("M:Sample.Work(System.String)", "B.cs", 5)));
        _ = Assert.Single(result.Matches);
        _ = Assert.Single(result.Added);
        _ = Assert.Single(result.Removed);
    }

    [Fact]
    public void Matching_does_not_fabricate_a_match_for_duplicate_identity()
    {
        SnapshotMatchResult result = SnapshotMemberMatcher.Match(
            Report(Member("M:Sample.Work", "A.cs", 1)),
            Report(Member("M:Sample.Work", "B.cs", 1), Member("M:Sample.Work", "C.cs", 1)));
        Assert.Empty(result.Matches);
        Assert.Equal(["M:Sample.Work"], result.AmbiguousIds);
    }

    private static ProjectReport Report(params MemberReport[] members)
    {
        ProjectReport report = new()
        {
            EntryPoint = "sample.slnx"
        };
        AnalyzedProjectReport project = new()
        {
            Name = "Sample",
            Path = "Sample.csproj"
        };
        AnalyzedFileReport file = new()
        {
            Path = "Sample.cs"
        };
        file.Members.AddRange(members);
        project.Files.Add(file);
        report.Projects.Add(project);
        return report;
    }

    private static MemberReport Member(string stableId, string path, int line)
    {
        return new()
        {
            StableId = stableId,
            DisplayName = stableId,
            Kind = "Method",
            SymbolId = stableId,
            Location = new SourceLocationReport { FilePath = path, StartLine = line, StartColumn = 1, EndLine = line, EndColumn = 2, Start = line, Length = 1 },
            BigO = BigOReport.Known("O(n)"),
        };
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = await ComplexityToolApplication.RunAsync(args, output, error, CancellationToken.None);
        return (exitCode, output.ToString(), error.ToString());
    }
}

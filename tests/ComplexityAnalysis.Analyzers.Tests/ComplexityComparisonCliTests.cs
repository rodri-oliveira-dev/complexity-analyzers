using System.Text.Json;

using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Reporting;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityComparisonCliTests
{
    [Fact]
    public async Task Compare_emits_deterministic_json_and_opt_in_gate_fails_only_for_regression()
    {
        using SnapshotFiles files = SnapshotFiles.Create("O(n)", "O(n²)");

        (int noGateCode, string first, string firstError) = await RunAsync(
            "compare", files.Baseline, files.Current, "--format", "json");
        (int gateCode, string second, string secondError) = await RunAsync(
            "compare", files.Baseline, files.Current, "--format", "json", "--fail-on-regression");

        Assert.Equal(ToolExitCodes.Success, noGateCode);
        Assert.Equal(ToolExitCodes.QualityGateFailed, gateCode);
        Assert.Equal(string.Empty, firstError);
        Assert.Equal(string.Empty, secondError);
        Assert.Equal(first, second);
        using JsonDocument document = JsonDocument.Parse(first);
        Assert.Equal("complexity-comparison/1.0", document.RootElement.GetProperty("SchemaVersion").GetString());
        Assert.Equal("regression", document.RootElement.GetProperty("Findings")[0].GetProperty("Classification").GetString());
    }

    [Fact]
    public async Task Compare_console_reports_unknown_as_incomparable_without_failing_gate()
    {
        using SnapshotFiles files = SnapshotFiles.Create(null, "O(n²)");

        (int code, string output, string error) = await RunAsync(
            "compare", files.Baseline, files.Current, "--fail-on-regression");

        Assert.Equal(ToolExitCodes.Success, code);
        Assert.Equal(string.Empty, error);
        Assert.Contains("INCOMPARABLE M:Sample.Work", output, StringComparison.Ordinal);
        Assert.Contains("Unknown", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_rejects_non_snapshot_schema_as_operational_error()
    {
        using SnapshotFiles files = SnapshotFiles.Create("O(n)", "O(n)");
        File.WriteAllText(files.Baseline, /*lang=json,strict*/ "{\"SchemaVersion\":\"1.0\",\"EntryPoint\":\"sample\"}");

        (int code, _, string error) = await RunAsync("compare", files.Baseline, files.Current);

        Assert.Equal(ToolExitCodes.Error, code);
        Assert.Contains("Unsupported complexity snapshot schema", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--unknown", null, "Unknown option")]
    [InlineData("--format", null, "requires a value")]
    [InlineData("--format", "xml", "Format must be")]
    public async Task Compare_rejects_invalid_options_without_unhandled_exceptions(
        string option,
        string? value,
        string expectedError)
    {
        using SnapshotFiles files = SnapshotFiles.Create("O(n)", "O(n)");
        List<string> args = ["compare", files.Baseline, files.Current, option];
        if (value is not null)
        {
            args.Add(value);
        }

        (int code, _, string error) = await RunAsync([.. args]);

        Assert.Equal(ToolExitCodes.Error, code);
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_reports_missing_and_malformed_snapshots_as_operational_errors()
    {
        using SnapshotFiles files = SnapshotFiles.Create("O(n)", "O(n)");
        (int missingCode, _, string missingError) = await RunAsync(
            "compare",
            Path.Combine(files.Directory, "missing.json"),
            files.Current);
        File.WriteAllText(files.Baseline, "{");
        (int malformedCode, _, string malformedError) = await RunAsync(
            "compare",
            files.Baseline,
            files.Current);

        Assert.Equal(ToolExitCodes.Error, missingCode);
        Assert.NotEmpty(missingError);
        Assert.Equal(ToolExitCodes.Error, malformedCode);
        Assert.NotEmpty(malformedError);
    }

    [Fact]
    public async Task Compare_writes_empty_json_and_sarif_reports_without_enabling_the_gate()
    {
        using SnapshotFiles files = SnapshotFiles.CreateEmpty();
        string jsonPath = Path.Combine(files.Directory, "comparison.json");
        string sarifPath = Path.Combine(files.Directory, "comparison.sarif");

        (int jsonCode, string jsonOutput, string jsonError) = await RunAsync(
            "compare", files.Baseline, files.Current, "--format", "json", "--output", jsonPath);
        (int sarifCode, string sarifOutput, string sarifError) = await RunAsync(
            "compare", files.Baseline, files.Current, "--format", "sarif", "--output", sarifPath);

        Assert.Equal(ToolExitCodes.Success, jsonCode);
        Assert.Equal(ToolExitCodes.Success, sarifCode);
        Assert.Equal(string.Empty, jsonOutput + jsonError + sarifOutput + sarifError);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(jsonPath));
        using JsonDocument sarif = JsonDocument.Parse(File.ReadAllText(sarifPath));
        Assert.Empty(json.RootElement.GetProperty("Findings").EnumerateArray());
        Assert.Empty(sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray());
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = await ComplexityToolApplication.RunAsync(args, output, error, CancellationToken.None);
        return (exitCode, output.ToString(), error.ToString());
    }

    private sealed class SnapshotFiles : IDisposable
    {
        private SnapshotFiles(string directory)
        {
            Directory = directory;
            Baseline = Path.Combine(directory, "baseline.json");
            Current = Path.Combine(directory, "current.json");
        }

        internal string Directory
        {
            get;
        }

        internal string Baseline
        {
            get;
        }

        internal string Current
        {
            get;
        }

        internal static SnapshotFiles Create(string? before, string? after)
        {
            string directory = Path.Combine(Path.GetTempPath(), "complexity-comparison-tests", Guid.NewGuid().ToString("N"));
            _ = System.IO.Directory.CreateDirectory(directory);
            SnapshotFiles files = new(directory);
            File.WriteAllText(files.Baseline, new JsonReportWriter().Write(Report(before)));
            File.WriteAllText(files.Current, new JsonReportWriter().Write(Report(after)));
            return files;
        }

        internal static SnapshotFiles CreateEmpty()
        {
            string directory = Path.Combine(Path.GetTempPath(), "complexity-comparison-tests", Guid.NewGuid().ToString("N"));
            _ = System.IO.Directory.CreateDirectory(directory);
            SnapshotFiles files = new(directory);
            ProjectReport report = new()
            {
                SchemaVersion = "complexity-snapshot/1.0",
                EntryPoint = "sample.slnx",
                Configuration = new SnapshotConfigurationReport { IncludeEvidence = true, MinimumDuplicateTokens = 40 },
            };
            string json = new JsonReportWriter().Write(report);
            File.WriteAllText(files.Baseline, json);
            File.WriteAllText(files.Current, json);
            return files;
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }

        private static ProjectReport Report(string? complexity)
        {
            ProjectReport report = new()
            {
                SchemaVersion = "complexity-snapshot/1.0",
                EntryPoint = "sample.slnx",
                Configuration = new SnapshotConfigurationReport { IncludeEvidence = true, MinimumDuplicateTokens = 40 },
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
            MemberReport member = new()
            {
                StableId = "M:Sample.Work",
                SymbolId = "Sample.Work",
                DisplayName = "Work",
                Kind = "Method",
                Location = new SourceLocationReport { FilePath = "Sample.cs", StartLine = 1, StartColumn = 1, EndLine = 1, EndColumn = 2, Length = 1 },
                BigO = complexity is null ? BigOReport.Unknown() : BigOReport.Known(complexity),
                Evidence = CreateEvidence(complexity),
            };
            file.Members.Add(member);
            project.Files.Add(file);
            report.Projects.Add(project);
            return report;
        }

        private static ComplexityEvidenceReport CreateEvidence(string? complexity)
        {
            ComplexityEvidenceReport evidence = new()
            {
                MaximumDepth = 1,
                MaximumSteps = 1,
            };
            evidence.Steps.Add(new ComplexityEvidenceStepReport
            {
                Identity = "root",
                Kind = "analysis",
                Origin = "syntax",
                Operation = "method",
                Location = new ComplexityEvidenceLocationReport(),
                Estimate = new ComplexityEstimateReport
                {
                    Status = complexity is null ? "unknown" : "known",
                    Value = complexity,
                    Guarantee = complexity is null ? "unknown" : "worstCaseUpperBound",
                },
            });
            return evidence;
        }
    }
}

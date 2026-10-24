using System.Text.Json;

using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Reporting;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class SarifReportWriterTests
{
    [Fact]
    public void Empty_report_is_valid_deterministic_sarif()
    {
        ProjectReport report = new()
        {
            EntryPoint = "sample.slnx"
        };

        string first = SarifReportWriter.Write(report);
        string second = SarifReportWriter.Write(report);

        Assert.Equal(first, second);
        using JsonDocument document = JsonDocument.Parse(first);
        Assert.Equal("2.1.0", document.RootElement.GetProperty("version").GetString());
        Assert.Equal("https://json.schemastore.org/sarif-2.1.0.json", document.RootElement.GetProperty("$schema").GetString());
        Assert.Empty(document.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray());
    }

    [Fact]
    public void Sarif_escapes_messages_sanitizes_uris_and_stabilizes_fingerprints_across_projects()
    {
        ProjectReport report = new()
        {
            EntryPoint = "sample.slnx"
        };
        report.Projects.Add(Project("First", "src/First.csproj", "src/Données & One.cs", "M:First.Work", "Work \"one\""));
        report.Projects.Add(Project("Second", "src/Second.csproj", "../sensitive/Second.cs", "M:Second.Work", "Work\nTwo"));

        string json = SarifReportWriter.Write(report);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement results = document.RootElement.GetProperty("runs")[0].GetProperty("results");

        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal("src/Donn%C3%A9es%20%26%20One.cs", results[0].GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString());
        Assert.Equal("Second.cs", results[1].GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString());
        string firstFingerprint = results[0].GetProperty("partialFingerprints").GetProperty("complexityMember/v1").GetString()!;
        Assert.Equal(64, firstFingerprint.Length);
        Assert.DoesNotContain("sensitive", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_is_emitted_only_with_a_legitimate_evidence_reason()
    {
        ProjectReport report = new()
        {
            EntryPoint = "sample.slnx"
        };
        AnalyzedProjectReport project = Project("Sample", "Sample.csproj", "Unknown.cs", "M:Sample.Unknown", "Unknown");
        project.Files[0].Members[0] = UnknownMember("M:Sample.Silent", null);
        project.Files[0].Members.Add(UnknownMember("M:Sample.Explained", "unprovenLoopBound"));
        report.Projects.Add(project);

        using JsonDocument document = JsonDocument.Parse(SarifReportWriter.Write(report));
        JsonElement results = document.RootElement.GetProperty("runs")[0].GetProperty("results");

        _ = Assert.Single(results.EnumerateArray());
        Assert.Equal("CA0002", results[0].GetProperty("ruleId").GetString());
    }

    [Fact]
    public async Task Sarif_format_does_not_change_comparison_gate_exit_code()
    {
        using TempSnapshots snapshots = TempSnapshots.Create();
        using StringWriter output = new();
        using StringWriter error = new();

        int code = await ComplexityToolApplication.RunAsync(
            ["compare", snapshots.Before, snapshots.After, "--format", "sarif", "--fail-on-regression"],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(ToolExitCodes.QualityGateFailed, code);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal("CA1001", document.RootElement.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("ruleId").GetString());
    }

    private static AnalyzedProjectReport Project(string name, string projectPath, string filePath, string id, string displayName)
    {
        AnalyzedProjectReport project = new()
        {
            Name = name,
            Path = projectPath
        };
        AnalyzedFileReport file = new()
        {
            Path = filePath
        };
        file.Members.Add(new MemberReport
        {
            StableId = id,
            SymbolId = id,
            DisplayName = displayName,
            Kind = "Method",
            Location = Location(filePath),
            BigO = BigOReport.Known("O(n)"),
        });
        project.Files.Add(file);
        return project;
    }

    private static MemberReport UnknownMember(string id, string? reason)
    {
        return new MemberReport
        {
            StableId = id,
            SymbolId = id,
            DisplayName = id,
            Kind = "Method",
            Location = Location("Unknown.cs"),
            BigO = BigOReport.Unknown(),
            Evidence = reason is null ? null : new ComplexityEvidenceReport { UnknownReason = reason },
        };
    }

    private static SourceLocationReport Location(string path)
    {
        return new()
        {
            FilePath = path,
            StartLine = 1,
            StartColumn = 2,
            EndLine = 1,
            EndColumn = 8,
            Length = 6,
        };
    }

    private sealed class TempSnapshots : IDisposable
    {
        private TempSnapshots(string directory, string before, string after)
        {
            Directory = directory;
            Before = before;
            After = after;
        }

        internal string Directory
        {
            get;
        }

        internal string Before
        {
            get;
        }

        internal string After
        {
            get;
        }

        internal static TempSnapshots Create()
        {
            string directory = Path.Combine(Path.GetTempPath(), "sarif-tests", Guid.NewGuid().ToString("N"));
            _ = System.IO.Directory.CreateDirectory(directory);
            string before = Path.Combine(directory, "before.json");
            string after = Path.Combine(directory, "after.json");
            File.WriteAllText(before, new JsonReportWriter().Write(Snapshot("O(n)")));
            File.WriteAllText(after, new JsonReportWriter().Write(Snapshot("O(n²)")));
            return new TempSnapshots(directory, before, after);
        }

        public void Dispose()
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }

        private static ProjectReport Snapshot(string complexity)
        {
            ProjectReport report = new()
            {
                SchemaVersion = "complexity-snapshot/1.0",
                EntryPoint = "sample"
            };
            report.Projects.Add(Project("Sample", "Sample.csproj", "Sample.cs", "M:Sample.Work", "Work"));
            report.Projects[0].Files[0].Members[0] = new MemberReport
            {
                StableId = "M:Sample.Work",
                SymbolId = "M:Sample.Work",
                DisplayName = "Work",
                Kind = "Method",
                Location = Location("Sample.cs"),
                BigO = BigOReport.Known(complexity),
            };
            return report;
        }
    }
}

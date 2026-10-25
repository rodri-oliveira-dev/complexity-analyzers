using System.Text.Json;

using ComplexityAnalysis.Mcp;
using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Reporting;

using ModelContextProtocol.Client;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class McpServerContractTests
{
    [Fact]
    public async Task Stdio_client_discovers_the_read_only_tool_surface()
    {
        using McpFixture fixture = McpFixture.Create();
        string assemblyPath = typeof(ComplexityTools).Assembly.Location;
        StdioClientTransport transport = new(new StdioClientTransportOptions
        {
            Name = "complexity-analysis-contract",
            Command = "dotnet",
            Arguments = [assemblyPath, "--root", fixture.Root],
        });

        await using var client = await McpClient.CreateAsync(transport);
        IList<McpClientTool> tools = await client.ListToolsAsync();

        Assert.Equal(
            ["analyze_project", "compare_snapshots", "query_member_evidence"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));

        var result = await client.CallToolAsync(
            "analyze_project",
            new Dictionary<string, object?> { ["path"] = "Fixture.csproj" });
        JsonElement structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("complexity-mcp-analysis/1.0", structured.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            "complexity-snapshot/1.0",
            structured.GetProperty("report").GetProperty("SchemaVersion").GetString());

        JsonElement report = structured.GetProperty("report");
        string stableId = Assert.IsType<string>(report.GetProperty("Projects")[0]
            .GetProperty("Files")[0]
            .GetProperty("Members")[0]
            .GetProperty("StableId").GetString());
        var evidenceResult = await client.CallToolAsync(
            "query_member_evidence",
            new Dictionary<string, object?>
            {
                ["path"] = "Fixture.csproj",
                ["stableId"] = stableId,
            });
        JsonElement evidence = Assert.IsType<JsonElement>(evidenceResult.StructuredContent);
        Assert.True(evidence.GetProperty("found").GetBoolean());
        Assert.Equal(stableId, evidence.GetProperty("stableId").GetString());

        fixture.WriteJson("baseline.json", report);
        fixture.WriteJson("current.json", report);
        var comparisonResult = await client.CallToolAsync(
            "compare_snapshots",
            new Dictionary<string, object?>
            {
                ["baselinePath"] = "baseline.json",
                ["currentPath"] = "current.json",
            });
        JsonElement comparison = Assert.IsType<JsonElement>(comparisonResult.StructuredContent);
        Assert.Equal(
            "complexity-comparison/1.0",
            comparison.GetProperty("comparison").GetProperty("SchemaVersion").GetString());

        var invalidResult = await client.CallToolAsync(
            "query_member_evidence",
            new Dictionary<string, object?>
            {
                ["path"] = "Fixture.csproj",
                ["stableId"] = string.Empty,
            });
        Assert.True(invalidResult.IsError);
    }

    [Fact]
    public async Task Analyze_and_member_query_reproduce_cli_evidence()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService();

        McpAnalysisResponse response = await service.AnalyzeProjectAsync("Fixture.csproj", CancellationToken.None);
        JsonElement report = response.Report;
        JsonElement member = report.GetProperty("Projects")[0]
            .GetProperty("Files")[0]
            .GetProperty("Members")[0];
        string stableId = Assert.IsType<string>(member.GetProperty("StableId").GetString());
        McpMemberEvidenceResponse evidence = await service.QueryMemberEvidenceAsync(
            "Fixture.csproj",
            stableId,
            CancellationToken.None);

        Assert.Equal("complexity-snapshot/1.0", report.GetProperty("SchemaVersion").GetString());
        Assert.True(evidence.Found);
        Assert.Equal(stableId, evidence.StableId);
        Assert.True(evidence.Member!.Value.TryGetProperty("Evidence", out _));
    }

    [Fact]
    public async Task Snapshot_comparison_is_structured_and_deterministic()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService();
        JsonElement snapshot = (await service.AnalyzeProjectAsync("Fixture.csproj", CancellationToken.None)).Report;
        fixture.WriteJson("baseline.json", snapshot);
        fixture.WriteJson("current.json", snapshot);

        McpComparisonResponse firstResponse = await service.CompareSnapshotsAsync(
            "baseline.json",
            "current.json",
            CancellationToken.None);
        McpComparisonResponse secondResponse = await service.CompareSnapshotsAsync(
            "baseline.json",
            "current.json",
            CancellationToken.None);

        JsonElement first = firstResponse.Comparison;
        JsonElement second = secondResponse.Comparison;
        Assert.Equal("complexity-comparison/1.0", first.GetProperty("SchemaVersion").GetString());
        Assert.Equal(first.GetRawText(), second.GetRawText());
        Assert.All(
            first.GetProperty("Findings").EnumerateArray(),
            finding => Assert.Equal("equivalent", finding.GetProperty("Classification").GetString()));
    }

    [Fact]
    public async Task Requests_reject_traversal_size_overflow_and_cancellation()
    {
        using McpFixture fixture = McpFixture.Create(maximumBytes: 8);
        ComplexityMcpService service = fixture.CreateService();

        ArgumentException traversal = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnalyzeProjectAsync(Path.Combine("..", "outside.csproj"), CancellationToken.None));
        ArgumentException size = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnalyzeProjectAsync("Fixture.csproj", CancellationToken.None));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.CreateService(maximumBytes: 1_000_000)
                .AnalyzeProjectAsync("Fixture.csproj", cancellation.Token));

        Assert.Equal("Path is outside the allowed root.", traversal.Message);
        Assert.Equal("The analysis exceeds the configured input-size limit.", size.Message);
        Assert.DoesNotContain(fixture.Root, traversal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Comparison_and_serialization_observe_cancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ProjectReport report = new()
        {
            SchemaVersion = "complexity-snapshot/1.0",
            EntryPoint = "sample.slnx",
        };
        ComplexityComparisonReport comparison = new()
        {
            BaselineSchemaVersion = report.SchemaVersion,
            CurrentSchemaVersion = report.SchemaVersion,
        };

        _ = Assert.Throws<OperationCanceledException>(() =>
            ComplexityRegressionComparer.Compare(report, report, cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JsonReportWriter.WriteAsync(report, cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ComparisonReportWriter.WriteJsonAsync(comparison, cancellation.Token));
    }

    [Fact]
    public void Volume_root_allows_files_below_the_root_boundary()
    {
        string file = Path.GetTempFileName();
        try
        {
            string volumeRoot = Path.GetPathRoot(file)!;
            AllowedPathPolicy policy = new(new McpHostOptions(
                volumeRoot,
                TimeSpan.FromSeconds(1),
                1_000_000,
                10));

            Assert.Equal(Path.GetFullPath(file), policy.ResolveInput(file, ".tmp"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private sealed class McpFixture : IDisposable
    {
        private readonly long _maximumBytes;

        private McpFixture(string root, long maximumBytes)
        {
            Root = root;
            _maximumBytes = maximumBytes;
        }

        internal string Root
        {
            get;
        }

        internal static McpFixture Create(long maximumBytes = 1_000_000)
        {
            string root = Path.Combine(Path.GetTempPath(), "complexity-mcp-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "Fixture.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            File.WriteAllText(
                Path.Combine(root, "Fixture.cs"),
                "public static class Fixture { public static int Sum(int[] values) { int total = 0; foreach (int value in values) { total += value; } return total; } }");
            return new(root, maximumBytes);
        }

        internal ComplexityMcpService CreateService(long? maximumBytes = null)
        {
            return new(new McpHostOptions(
                Root,
                TimeSpan.FromSeconds(30),
                maximumBytes ?? _maximumBytes,
                100));
        }

        internal void WriteJson(string name, JsonElement value)
        {
            File.WriteAllText(Path.Combine(Root, name), value.GetRawText());
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

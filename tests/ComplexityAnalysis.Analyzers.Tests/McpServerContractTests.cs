using System.Diagnostics;
using System.Text.Json;

using ComplexityAnalysis.Mcp;
using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Reporting;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class McpServerContractTests
{
    [Fact]
    public async Task Stdio_client_discovers_the_read_only_tool_surface()
    {
        using McpFixture fixture = McpFixture.Create();
        StdioClientTransport transport = CreateTransport(fixture.Root);

        await using var client = await McpClient.CreateAsync(transport);
        IList<McpClientTool> tools = await client.ListToolsAsync();

        Assert.Equal(
            ["analyze_project", "compare_snapshots", "query_member_evidence"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));
        Assert.All(tools, tool => Assert.NotNull(tool.ProtocolTool.OutputSchema));
        McpClientTool analyzeTool = Assert.Single(tools, tool => tool.Name == "analyze_project");
        Assert.Contains("Unknown", analyzeTool.Description, StringComparison.Ordinal);
        Assert.Equal(
            ["maxMembers", "path", "responseMode"],
            analyzeTool.ProtocolTool.InputSchema.GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            ["path"],
            analyzeTool.ProtocolTool.InputSchema.GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString()));

        var result = await client.CallToolAsync(
            "analyze_project",
            new Dictionary<string, object?> { ["path"] = "Fixture.csproj" });
        JsonElement structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("complexity-mcp-analysis/1.0", structured.GetProperty("schemaVersion").GetString());
        Assert.Equal("full", structured.GetProperty("responseMode").GetString());
        Assert.False(structured.GetProperty("isTruncated").GetBoolean());
        Assert.Equal(
            "complexity-snapshot/1.0",
            structured.GetProperty("report").GetProperty("SchemaVersion").GetString());

        JsonElement report = structured.GetProperty("report");
        string stableId = Assert.IsType<string>(report.GetProperty("Projects")[0]
            .GetProperty("Files")[0]
            .GetProperty("Members")[0]
            .GetProperty("StableId").GetString());
        string analysisId = Assert.IsType<string>(structured.GetProperty("analysisId").GetString());
        var evidenceResult = await client.CallToolAsync(
            "query_member_evidence",
            new Dictionary<string, object?>
            {
                ["path"] = "Fixture.csproj",
                ["stableId"] = stableId,
                ["analysisId"] = analysisId,
            });
        JsonElement evidence = Assert.IsType<JsonElement>(evidenceResult.StructuredContent);
        Assert.True(evidence.GetProperty("found").GetBoolean());
        Assert.Equal(stableId, evidence.GetProperty("stableId").GetString());
        Assert.Equal("cachedSnapshot", evidence.GetProperty("source").GetString());

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

        var unavailableResult = await client.CallToolAsync(
            "query_member_evidence",
            new Dictionary<string, object?>
            {
                ["path"] = "Fixture.csproj",
                ["stableId"] = stableId,
                ["analysisId"] = "sha256:" + new string('0', 64),
            });
        Assert.True(unavailableResult.IsError);
        TextContentBlock error = Assert.IsType<TextContentBlock>(Assert.Single(unavailableResult.Content));
        Assert.Contains("The requested analysisId is unavailable.", error.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_and_member_query_reproduce_cli_evidence()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService();

        McpAnalysisResponse response = await service.AnalyzeProjectAsync("Fixture.csproj", CancellationToken.None);
        JsonElement report = response.Report!.Value;
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
        Assert.Equal("fresh", evidence.Source);
        Assert.DoesNotContain(fixture.Root, report.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Snapshot_comparison_is_structured_and_deterministic()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService();
        JsonElement snapshot = (await service.AnalyzeProjectAsync("Fixture.csproj", CancellationToken.None)).Report!.Value;
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
        Dictionary<string, string> stableIds = snapshot.GetProperty("Projects")[0]
            .GetProperty("Files")[0]
            .GetProperty("Members")
            .EnumerateArray()
            .ToDictionary(
                member => member.GetProperty("DisplayName").GetString()!,
                member => member.GetProperty("StableId").GetString()!,
                StringComparer.Ordinal);
        JsonElement[] findings = first.GetProperty("Findings").EnumerateArray().ToArray();
        Assert.Equal(
            "incomparable",
            Assert.Single(findings, finding =>
                StringComparer.Ordinal.Equals(
                    finding.GetProperty("StableId").GetString(),
                    stableIds["Mystery"]))
                .GetProperty("Classification")
                .GetString());
        Assert.Equal(
            "equivalent",
            Assert.Single(findings, finding =>
                StringComparer.Ordinal.Equals(
                    finding.GetProperty("StableId").GetString(),
                    stableIds["Sum"]))
                .GetProperty("Classification")
                .GetString());
        Assert.Contains(
            findings,
            finding => StringComparer.Ordinal.Equals(
                finding.GetProperty("Classification").GetString(),
                "incomparable"));
    }

    [Fact]
    public async Task Summary_is_bounded_and_cached_snapshot_avoids_reanalysis()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService();

        McpAnalysisResponse response = await service.AnalyzeProjectAsync(
            "Fixture.csproj",
            "summary",
            1,
            CancellationToken.None);

        Assert.Null(response.Report);
        Assert.NotNull(response.Summary);
        Assert.True(response.Reusable);
        Assert.True(response.IsTruncated);
        Assert.Equal(2, response.TotalMemberCount);
        _ = Assert.Single(response.Summary.Members);

        McpMemberSummary indexedMember = response.Summary.Members[0];
        File.WriteAllText(Path.Combine(fixture.Root, "Fixture.cs"), "public static class Fixture { }");
        McpMemberEvidenceResponse cached = await service.QueryMemberEvidenceAsync(
            "Fixture.csproj",
            indexedMember.StableId,
            response.AnalysisId,
            CancellationToken.None);
        McpMemberEvidenceResponse fresh = await service.QueryMemberEvidenceAsync(
            "Fixture.csproj",
            indexedMember.StableId,
            null,
            CancellationToken.None);

        Assert.True(cached.Found);
        Assert.Equal("cachedSnapshot", cached.Source);
        Assert.False(fresh.Found);
        Assert.Equal("fresh", fresh.Source);
    }

    [Fact]
    public async Task Analysis_ids_and_response_options_reject_invalid_or_unavailable_values()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService();

        ArgumentException malformed = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryMemberEvidenceAsync(
                "Fixture.csproj",
                "M:Fixture.Sum",
                "sha256:not-a-digest",
                CancellationToken.None));
        Assert.Equal("analysisId must be the identifier returned by analyze_project.", malformed.Message);

        InvalidOperationException unavailable = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QueryMemberEvidenceAsync(
                "Fixture.csproj",
                "M:Fixture.Sum",
                "sha256:" + new string('0', 64),
                CancellationToken.None));
        Assert.Equal(
            "The requested analysisId is unavailable. Run analyze_project again and retry with its new analysisId.",
            unavailable.Message);

        ArgumentException fullLimit = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnalyzeProjectAsync("Fixture.csproj", "full", 1, CancellationToken.None));
        Assert.Equal("maxMembers is supported only when responseMode is 'summary'.", fullLimit.Message);

        ArgumentException invalidMode = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnalyzeProjectAsync("Fixture.csproj", "compact", null, CancellationToken.None));
        Assert.Equal("responseMode must be 'full' or 'summary'.", invalidMode.Message);

        foreach (int invalidLimit in new[] { 0, 501 })
        {
            ArgumentException summaryLimit = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.AnalyzeProjectAsync("Fixture.csproj", "summary", invalidLimit, CancellationToken.None));
            Assert.Equal("maxMembers must be between 1 and 500.", summaryLimit.Message);
        }
    }

    [Fact]
    public void Analysis_result_cache_enforces_lru_entry_and_byte_limits()
    {
        AnalysisResultCache cache = new();
        string entryPath = Path.Combine(Path.GetTempPath(), "cache-fixture.csproj");
        CacheRegistration[] registrations = Enumerable.Range(0, 5)
            .Select(index => cache.Register(
                entryPath,
                JsonSerializer.SerializeToElement(new
                {
                    Index = index
                })))
            .ToArray();

        Assert.All(registrations, registration => Assert.True(registration.Reusable));
        Assert.False(cache.TryGet(registrations[0].AnalysisId, entryPath, out _));
        Assert.True(cache.TryGet(registrations[4].AnalysisId, entryPath, out JsonElement newest));
        Assert.Equal(4, newest.GetProperty("Index").GetInt32());

        JsonElement oversized = JsonSerializer.SerializeToElement(
            new
            {
                Payload = new string('x', (16 * 1024 * 1024) + 1)
            });
        CacheRegistration oversizedRegistration = cache.Register(entryPath, oversized);
        Assert.False(oversizedRegistration.Reusable);
        Assert.False(cache.TryGet(oversizedRegistration.AnalysisId, entryPath, out _));
    }

    [Fact]
    public async Task Unknown_and_concurrent_requests_remain_explicit_and_isolated()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService();

        McpAnalysisResponse[] responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            service.AnalyzeProjectAsync("Fixture.csproj", "summary", 10, CancellationToken.None)));

        _ = Assert.Single(responses.Select(response => response.AnalysisId).Distinct(StringComparer.Ordinal));
        Assert.All(responses, response => Assert.Equal(2, response.TotalMemberCount));
        McpMemberSummary unknown = Assert.Single(
            responses[0].Summary!.Members,
            member => StringComparer.Ordinal.Equals(member.ComplexityStatus, "unknown"));
        Assert.Null(unknown.Complexity);
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
    public async Task Configured_timeout_is_distinct_from_client_cancellation()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService service = fixture.CreateService(timeout: TimeSpan.Zero);

        TimeoutException timeout = await Assert.ThrowsAsync<TimeoutException>(() =>
            service.AnalyzeProjectAsync("Fixture.csproj", CancellationToken.None));

        Assert.Equal("The analysis exceeded the configured request timeout.", timeout.Message);
        Assert.DoesNotContain(fixture.Root, timeout.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tool_boundary_preserves_client_cancellation()
    {
        using McpFixture fixture = McpFixture.Create();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ComplexityTools.AnalyzeProjectAsync(
                fixture.CreateService(),
                "Fixture.csproj",
                cancellation.Token));
    }

    [Fact]
    public async Task Requests_reject_file_limit_and_symbolic_link_traversal()
    {
        using McpFixture fixture = McpFixture.Create();
        ComplexityMcpService limitedService = fixture.CreateService(maximumFiles: 1);

        ArgumentException fileLimit = await Assert.ThrowsAsync<ArgumentException>(() =>
            limitedService.AnalyzeProjectAsync("Fixture.csproj", CancellationToken.None));
        Assert.Equal("The analysis exceeds the configured file limit.", fileLimit.Message);

        string outside = Path.Combine(Path.GetTempPath(), "complexity-mcp-outside-" + Guid.NewGuid().ToString("N") + ".json");
        string link = Path.Combine(fixture.Root, "linked.json");
        await File.WriteAllTextAsync(outside, "{}");
        try
        {
            try
            {
                _ = File.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            AllowedPathPolicy policy = new(new McpHostOptions(
                fixture.Root,
                TimeSpan.FromSeconds(1),
                1_000_000,
                10));
            ArgumentException symbolicLink = Assert.Throws<ArgumentException>(() => policy.ResolveInput("linked.json", ".json"));
            Assert.Equal("Symbolic-link traversal is not allowed.", symbolicLink.Message);
        }
        finally
        {
            File.Delete(link);
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Informational_commands_exit_without_starting_the_protocol()
    {
        string assemblyPath = typeof(ComplexityTools).Assembly.Location;
        foreach (string argument in new[] { "--help", "--version" })
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{assemblyPath}\" {argument}",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;
            process.StandardInput.Close();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            string output;
            string error;
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                output = await outputTask;
                error = await errorTask;
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            Assert.Equal(0, process.ExitCode);
            Assert.True(string.IsNullOrEmpty(error), error);
            Assert.DoesNotContain("jsonrpc", output, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(output));
        }
    }

    [Theory]
    [InlineData("--timeout-seconds", "0", "--timeout-seconds must be between 1 and 300.")]
    [InlineData("--max-input-bytes", "1073741825", "--max-input-bytes must be between 1 and 1073741824.")]
    [InlineData("--max-files", "100001", "--max-files must be between 1 and 100000.")]
    public void Invalid_startup_limits_are_rejected_without_paths(string option, string value, string expected)
    {
        Assert.False(McpHostOptions.TryParse([option, value], out _, out string? error));
        Assert.Equal(expected, error);
        Assert.DoesNotContain(Path.GetTempPath(), error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_and_input_paths_validate_positive_and_negative_cases()
    {
        using McpFixture fixture = McpFixture.Create();

        Assert.True(McpHostOptions.TryParse(
            ["--root", fixture.Root, "--timeout-seconds", "5", "--max-input-bytes", "1024", "--max-files", "10"],
            out McpHostOptions? options,
            out string? successError));
        Assert.NotNull(options);
        Assert.Null(successError);

        Assert.False(McpHostOptions.TryParse(["--unknown", "value"], out _, out string? unknownError));
        Assert.Equal("Unknown option '--unknown'.", unknownError);

        Assert.False(McpHostOptions.TryParse(["--root", "\0"], out _, out string? rootError));
        Assert.Equal("The allowed root path is invalid.", rootError);

        AllowedPathPolicy policy = new(options);
        ArgumentException invalidInput = Assert.Throws<ArgumentException>(() =>
            policy.ResolveInput("\0", ".json"));
        Assert.Equal("Input path is invalid.", invalidInput.Message);
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

    private static StdioClientTransport CreateTransport(string root)
    {
        string? installedCommand = Environment.GetEnvironmentVariable("COMPLEXITY_MCP_COMMAND");
        return new(new StdioClientTransportOptions
        {
            Name = "complexity-analysis-contract",
            Command = installedCommand ?? "dotnet",
            Arguments = installedCommand is null
                ? [typeof(ComplexityTools).Assembly.Location, "--root", root]
                : ["--root", root],
        });
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
                "public static class Fixture { public static int Sum(int[] values) { int total = 0; foreach (int value in values) { total += value; } return total; } public static void Mystery() { while (true) { } } }");
            return new(root, maximumBytes);
        }

        internal ComplexityMcpService CreateService(
            long? maximumBytes = null,
            int maximumFiles = 100,
            TimeSpan? timeout = null)
        {
            return new(new McpHostOptions(
                Root,
                timeout ?? TimeSpan.FromSeconds(30),
                maximumBytes ?? _maximumBytes,
                maximumFiles));
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

using System.Text.Json;
using System.Xml;

using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Project;
using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Mcp;

public sealed class ComplexityMcpService
{
    private readonly AllowedPathPolicy _paths;
    private readonly TimeSpan _requestTimeout;

    internal ComplexityMcpService(McpHostOptions options)
    {
        _paths = new(options);
        _requestTimeout = options.RequestTimeout;
    }

    internal async Task<McpAnalysisResponse> AnalyzeProjectAsync(string path, CancellationToken cancellationToken)
    {
        string entryPath = _paths.ResolveInput(path, ".csproj", ".sln", ".slnx");
        using CancellationTokenSource timeout = CreateTimeout(cancellationToken);
        AllowedPathPolicy.OperationScope scope = _paths.CreateOperationScope();

        if (!ToolOptions.TryParse(
            ["analyze", entryPath, "--format", "json", "--snapshot"],
            out ToolOptions? options,
            out string? error)
            || options is null)
        {
            throw new InvalidOperationException(error ?? "The analysis request is invalid.");
        }

        try
        {
            ProjectReport report = await ProjectAnalysisRunner.AnalyzeAsync(options, timeout.Token, scope.Validate);
            return new("complexity-mcp-analysis/1.0", Parse(new JsonReportWriter().Write(report)));
        }
        catch (Exception exception) when (IsInputFailure(exception))
        {
            throw new InvalidOperationException("Unable to read analysis inputs within the allowed root.");
        }
    }

    internal async Task<McpMemberEvidenceResponse> QueryMemberEvidenceAsync(
        string path,
        string stableId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stableId) || stableId.Length > 2_048)
        {
            throw new ArgumentException("Stable member identity must contain between 1 and 2048 characters.");
        }

        McpAnalysisResponse analysis = await AnalyzeProjectAsync(path, cancellationToken);
        JsonElement report = analysis.Report;
        foreach (JsonElement project in report.GetProperty("Projects").EnumerateArray())
        {
            foreach (JsonElement file in project.GetProperty("Files").EnumerateArray())
            {
                foreach (JsonElement member in file.GetProperty("Members").EnumerateArray())
                {
                    if (member.TryGetProperty("StableId", out JsonElement id)
                        && StringComparer.Ordinal.Equals(id.GetString(), stableId))
                    {
                        return new(
                            "complexity-member-evidence/1.0",
                            stableId,
                            true,
                            member.Clone());
                    }
                }
            }
        }

        return new(
            "complexity-member-evidence/1.0",
            stableId,
            false,
            null);
    }

    internal async Task<McpComparisonResponse> CompareSnapshotsAsync(
        string baselinePath,
        string currentPath,
        CancellationToken cancellationToken)
    {
        string baseline = _paths.ResolveInput(baselinePath, ".json");
        string current = _paths.ResolveInput(currentPath, ".json");
        using CancellationTokenSource timeout = CreateTimeout(cancellationToken);
        AllowedPathPolicy.OperationScope scope = _paths.CreateOperationScope();
        scope.Validate(baseline);
        scope.Validate(current);

        try
        {
            ProjectReport baselineReport = await SnapshotReader.ReadAsync(baseline, timeout.Token);
            ProjectReport currentReport = await SnapshotReader.ReadAsync(current, timeout.Token);
            ComplexityComparisonReport comparison = ComplexityRegressionComparer.Compare(baselineReport, currentReport);
            return new("complexity-mcp-comparison/1.0", Parse(ComparisonReportWriter.WriteJson(comparison)));
        }
        catch (Exception exception) when (IsInputFailure(exception))
        {
            throw new InvalidOperationException("Unable to read complexity snapshots within the allowed root.");
        }
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(_requestTimeout);
        return source;
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static bool IsInputFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or XmlException or JsonException;
    }
}

public sealed record McpAnalysisResponse(string SchemaVersion, JsonElement Report);

public sealed record McpMemberEvidenceResponse(
    string SchemaVersion,
    string StableId,
    bool Found,
    JsonElement? Member);

public sealed record McpComparisonResponse(string SchemaVersion, JsonElement Comparison);

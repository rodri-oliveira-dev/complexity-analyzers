using System.Text.Json;
using System.Xml;

using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Comparison;
using ComplexityAnalysis.Tool.Project;
using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Mcp;

public sealed class ComplexityMcpService
{
    private const int DefaultSummaryMembers = 100;
    private const int MaximumSummaryMembers = 500;

    private readonly AllowedPathPolicy _paths;
    private readonly AnalysisResultCache _cache = new();
    private readonly TimeSpan _requestTimeout;
    private readonly string _displayRoot;

    internal ComplexityMcpService(McpHostOptions options)
    {
        _paths = new(options);
        _requestTimeout = options.RequestTimeout;
        _displayRoot = options.AllowedRoot;
    }

    internal async Task<McpAnalysisResponse> AnalyzeProjectAsync(
        string path,
        string responseMode,
        int? maxMembers,
        CancellationToken cancellationToken)
    {
        string normalizedMode = NormalizeResponseMode(responseMode, maxMembers);
        int summaryLimit = ValidateSummaryLimit(maxMembers);
        string entryPath = _paths.ResolveInput(path, ".csproj", ".sln", ".slnx");
        JsonElement report = await AnalyzeFreshAsync(entryPath, cancellationToken);
        CacheRegistration registration = _cache.Register(entryPath, report);

        if (StringComparer.Ordinal.Equals(normalizedMode, "full"))
        {
            int memberCount = CountMembers(report);
            return new(
                "complexity-mcp-analysis/1.0",
                registration.AnalysisId,
                "full",
                registration.Reusable,
                IsTruncated: false,
                memberCount,
                memberCount,
                report,
                null);
        }

        McpAnalysisSummary summary = CreateSummary(report, summaryLimit);
        return new(
            "complexity-mcp-analysis/1.0",
            registration.AnalysisId,
            "summary",
            registration.Reusable,
            summary.IsTruncated,
            summary.TotalMemberCount,
            summary.Members.Count,
            null,
            summary);
    }

    internal Task<McpAnalysisResponse> AnalyzeProjectAsync(string path, CancellationToken cancellationToken)
    {
        return AnalyzeProjectAsync(path, "full", null, cancellationToken);
    }

    internal async Task<McpMemberEvidenceResponse> QueryMemberEvidenceAsync(
        string path,
        string stableId,
        string? analysisId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stableId) || stableId.Length > 2_048)
        {
            throw new ArgumentException("Stable member identity must contain between 1 and 2048 characters.");
        }

        string entryPath = _paths.ResolveInput(path, ".csproj", ".sln", ".slnx");
        JsonElement report;
        string resolvedAnalysisId;
        string source;
        if (analysisId is null)
        {
            report = await AnalyzeFreshAsync(entryPath, cancellationToken);
            CacheRegistration registration = _cache.Register(entryPath, report);
            resolvedAnalysisId = registration.AnalysisId;
            source = "fresh";
        }
        else
        {
            if (!IsValidAnalysisId(analysisId))
            {
                throw new ArgumentException("analysisId must be the identifier returned by analyze_project.");
            }

            if (!_cache.TryGet(analysisId, entryPath, out report))
            {
                throw new InvalidOperationException(
                    "The requested analysisId is unavailable. Run analyze_project again and retry with its new analysisId.");
            }

            resolvedAnalysisId = analysisId;
            source = "cachedSnapshot";
        }

        foreach (JsonElement member in EnumerateMembers(report))
        {
            if (member.TryGetProperty("StableId", out JsonElement id)
                && StringComparer.Ordinal.Equals(id.GetString(), stableId))
            {
                return new(
                    "complexity-member-evidence/1.0",
                    stableId,
                    true,
                    member.Clone(),
                    resolvedAnalysisId,
                    source);
            }
        }

        return new(
            "complexity-member-evidence/1.0",
            stableId,
            false,
            null,
            resolvedAnalysisId,
            source);
    }

    internal Task<McpMemberEvidenceResponse> QueryMemberEvidenceAsync(
        string path,
        string stableId,
        CancellationToken cancellationToken)
    {
        return QueryMemberEvidenceAsync(path, stableId, null, cancellationToken);
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

        try
        {
            scope.Validate(baseline);
            scope.Validate(current);
            ProjectReport baselineReport = await SnapshotReader.ReadAsync(baseline, timeout.Token);
            ProjectReport currentReport = await SnapshotReader.ReadAsync(current, timeout.Token);
            ComplexityComparisonReport comparison = ComplexityRegressionComparer.Compare(
                baselineReport,
                currentReport,
                timeout.Token);
            string json = await ComparisonReportWriter.WriteJsonAsync(comparison, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            return new("complexity-mcp-comparison/1.0", Parse(json));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The comparison exceeded the configured request timeout.");
        }
        catch (Exception exception) when (IsInputFailure(exception))
        {
            throw new InvalidOperationException("Unable to read complexity snapshots within the allowed root.");
        }
    }

    private async Task<JsonElement> AnalyzeFreshAsync(string entryPath, CancellationToken cancellationToken)
    {
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
            ProjectReport report = await ProjectAnalysisRunner.AnalyzeAsync(
                options,
                timeout.Token,
                scope.Validate,
                skipReparsePoints: true,
                displayBaseDirectory: _displayRoot);
            string json = await JsonReportWriter.WriteAsync(report, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            return Parse(json);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The analysis exceeded the configured request timeout.");
        }
        catch (Exception exception) when (IsInputFailure(exception))
        {
            throw new InvalidOperationException("Unable to read analysis inputs within the allowed root.");
        }
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_requestTimeout <= TimeSpan.Zero)
        {
            source.Cancel();
        }
        else
        {
            source.CancelAfter(_requestTimeout);
        }

        return source;
    }

    private static string NormalizeResponseMode(string responseMode, int? maxMembers)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(responseMode, "full"))
        {
            if (maxMembers is not null)
            {
                throw new ArgumentException("maxMembers is supported only when responseMode is 'summary'.");
            }

            return "full";
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(responseMode, "summary"))
        {
            return "summary";
        }

        throw new ArgumentException("responseMode must be 'full' or 'summary'.");
    }

    private static int ValidateSummaryLimit(int? maxMembers)
    {
        int value = maxMembers ?? DefaultSummaryMembers;
        if (value is < 1 or > MaximumSummaryMembers)
        {
            throw new ArgumentException("maxMembers must be between 1 and 500.");
        }

        return value;
    }

    private static bool IsValidAnalysisId(string analysisId)
    {
        const string prefix = "sha256:";
        return analysisId.Length == prefix.Length + 64
            && analysisId.StartsWith(prefix, StringComparison.Ordinal)
            && HasOnlyLowercaseHex(analysisId.AsSpan(prefix.Length));
    }

    private static bool HasOnlyLowercaseHex(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static int CountMembers(JsonElement report)
    {
        int count = 0;
        foreach (JsonElement project in report.GetProperty("Projects").EnumerateArray())
        {
            foreach (JsonElement file in project.GetProperty("Files").EnumerateArray())
            {
                count = checked(count + file.GetProperty("Members").GetArrayLength());
            }
        }

        return count;
    }

    private static McpAnalysisSummary CreateSummary(JsonElement report, int maximumMembers)
    {
        List<McpMemberSummary> members = [];
        int projectCount = report.GetProperty("Projects").GetArrayLength();
        int fileCount = CountFiles(report);
        int memberCount = 0;
        int unknownMemberCount = 0;
        foreach (JsonElement member in EnumerateMembers(report))
        {
            memberCount++;
            JsonElement bigO = member.GetProperty("BigO");
            if (bigO.GetProperty("IsUnknown").GetBoolean())
            {
                unknownMemberCount++;
            }

            if (members.Count < maximumMembers)
            {
                JsonElement value = bigO.GetProperty("Value");
                members.Add(new(
                    member.GetProperty("StableId").GetString()!,
                    member.GetProperty("DisplayName").GetString()!,
                    member.GetProperty("Kind").GetString()!,
                    bigO.GetProperty("Status").GetString()!,
                    value.ValueKind == JsonValueKind.Null ? null : value.GetString()));
            }
        }

        return new(
            report.GetProperty("SchemaVersion").GetString()!,
            projectCount,
            fileCount,
            memberCount,
            unknownMemberCount,
            members.Count < memberCount,
            members);
    }

    private static int CountFiles(JsonElement report)
    {
        int count = 0;
        foreach (JsonElement project in report.GetProperty("Projects").EnumerateArray())
        {
            count = checked(count + project.GetProperty("Files").GetArrayLength());
        }

        return count;
    }

    private static IEnumerable<JsonElement> EnumerateMembers(JsonElement report)
    {
        foreach (JsonElement project in report.GetProperty("Projects").EnumerateArray())
        {
            foreach (JsonElement file in project.GetProperty("Files").EnumerateArray())
            {
                foreach (JsonElement member in file.GetProperty("Members").EnumerateArray())
                {
                    yield return member;
                }
            }
        }
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

public sealed record McpAnalysisResponse(
    string SchemaVersion,
    string AnalysisId,
    string ResponseMode,
    bool Reusable,
    bool IsTruncated,
    int TotalMemberCount,
    int ReturnedMemberCount,
    JsonElement? Report,
    McpAnalysisSummary? Summary);

public sealed record McpAnalysisSummary(
    string ReportSchemaVersion,
    int ProjectCount,
    int FileCount,
    int TotalMemberCount,
    int UnknownMemberCount,
    bool IsTruncated,
    IReadOnlyList<McpMemberSummary> Members);

public sealed record McpMemberSummary(
    string StableId,
    string DisplayName,
    string Kind,
    string ComplexityStatus,
    string? Complexity);

public sealed record McpMemberEvidenceResponse(
    string SchemaVersion,
    string StableId,
    bool Found,
    JsonElement? Member,
    string AnalysisId,
    string Source);

public sealed record McpComparisonResponse(string SchemaVersion, JsonElement Comparison);

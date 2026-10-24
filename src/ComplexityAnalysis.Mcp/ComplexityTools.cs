using System.ComponentModel;

using ModelContextProtocol.Server;

namespace ComplexityAnalysis.Mcp;

[McpServerToolType]
public sealed class ComplexityTools
{
    [McpServerTool(
        Name = "analyze_project",
        ReadOnly = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpAnalysisResponse))]
    [Description("Analyzes a C# project or solution under the configured root and returns a deterministic complexity snapshot with evidence.")]
    public static Task<McpAnalysisResponse> AnalyzeProjectAsync(
        ComplexityMcpService service,
        [Description("Relative path to a .csproj, .sln, or .slnx file under the configured root.")] string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.AnalyzeProjectAsync(path, cancellationToken);
    }

    [McpServerTool(
        Name = "query_member_evidence",
        ReadOnly = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpMemberEvidenceResponse))]
    [Description("Returns the complexity estimate and evidence for one stable member identity in a C# project or solution.")]
    public static Task<McpMemberEvidenceResponse> QueryMemberEvidenceAsync(
        ComplexityMcpService service,
        [Description("Relative path to a .csproj, .sln, or .slnx file under the configured root.")] string path,
        [Description("Exact StableId returned by analyze_project.")] string stableId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.QueryMemberEvidenceAsync(path, stableId, cancellationToken);
    }

    [McpServerTool(
        Name = "compare_snapshots",
        ReadOnly = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpComparisonResponse))]
    [Description("Compares two versioned complexity snapshot JSON files under the configured root.")]
    public static Task<McpComparisonResponse> CompareSnapshotsAsync(
        ComplexityMcpService service,
        [Description("Relative path to the baseline complexity snapshot JSON file.")] string baselinePath,
        [Description("Relative path to the current complexity snapshot JSON file.")] string currentPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.CompareSnapshotsAsync(baselinePath, currentPath, cancellationToken);
    }
}

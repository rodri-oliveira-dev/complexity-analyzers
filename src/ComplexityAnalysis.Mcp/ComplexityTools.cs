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
    [Description("Analyzes a C# project or solution below --root. Returns a deterministic snapshot with explicit Unknown results. Use responseMode='summary' to reduce context, then query_member_evidence with analysisId for exact member details.")]
    public static Task<McpAnalysisResponse> AnalyzeProjectAsync(
        ComplexityMcpService service,
        [Description("Relative or absolute path to a .csproj, .sln, or .slnx file below the configured --root.")] string path,
        CancellationToken cancellationToken,
        [Description("Response detail: 'full' preserves the complete report; 'summary' returns a bounded member index with explicit truncation metadata.")] string responseMode = "full",
        [Description("Maximum member summaries (1-500) when responseMode is 'summary'; default 100. Supplying it in full mode is rejected.")] int? maxMembers = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.AnalyzeProjectAsync(path, responseMode, maxMembers, cancellationToken);
    }

    [McpServerTool(
        Name = "query_member_evidence",
        ReadOnly = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpMemberEvidenceResponse))]
    [Description("Returns one member's estimate and evidence by exact StableId. Pass analyze_project's analysisId to reuse that immutable snapshot; omit it to analyze current files again.")]
    public static Task<McpMemberEvidenceResponse> QueryMemberEvidenceAsync(
        ComplexityMcpService service,
        [Description("Relative or absolute path to a .csproj, .sln, or .slnx file below the configured --root.")] string path,
        [Description("Exact StableId returned by analyze_project.")] string stableId,
        CancellationToken cancellationToken,
        [Description("Optional analysisId returned by analyze_project. Reuses only that exact in-memory snapshot and fails explicitly if unavailable.")] string? analysisId = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.QueryMemberEvidenceAsync(path, stableId, analysisId, cancellationToken);
    }

    [McpServerTool(
        Name = "compare_snapshots",
        ReadOnly = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpComparisonResponse))]
    [Description("Compares baseline and current complexity-snapshot JSON files below --root. Unknown estimates remain incomparable and are never promoted to proven regressions.")]
    public static Task<McpComparisonResponse> CompareSnapshotsAsync(
        ComplexityMcpService service,
        [Description("Relative or absolute path to the baseline .json snapshot below --root.")] string baselinePath,
        [Description("Relative or absolute path to the current .json snapshot below --root.")] string currentPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.CompareSnapshotsAsync(baselinePath, currentPath, cancellationToken);
    }
}

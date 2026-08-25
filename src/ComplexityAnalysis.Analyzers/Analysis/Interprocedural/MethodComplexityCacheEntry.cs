using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class MethodComplexityCacheEntry
{
    private MethodComplexityCacheEntry(
        MethodComplexityCacheEntryKind kind,
        InterproceduralAnalysisResult? result)
    {
        Kind = kind;
        Result = result;
    }

    internal MethodComplexityCacheEntryKind Kind
    {
        get;
    }

    internal InterproceduralAnalysisResult? Result
    {
        get;
    }

    internal static MethodComplexityCacheEntry InProgress()
    {
        return new MethodComplexityCacheEntry(
            MethodComplexityCacheEntryKind.InProgress,
            null);
    }

    internal static MethodComplexityCacheEntry Completed(InterproceduralAnalysisResult result)
    {
        _ = result ?? throw new ArgumentNullException(nameof(result));

        return new MethodComplexityCacheEntry(
            MethodComplexityCacheEntryKind.Completed,
            result);
    }
}

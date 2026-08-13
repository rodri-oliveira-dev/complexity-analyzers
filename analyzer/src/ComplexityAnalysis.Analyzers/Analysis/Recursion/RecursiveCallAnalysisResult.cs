using System;
using System.Collections.Immutable;
using System.Linq;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecursiveCallAnalysisResult
{
    private RecursiveCallAnalysisResult(
        bool isSupported,
        ImmutableArray<BaseCaseEvidence> baseCaseEvidence,
        ImmutableArray<RecursiveExecutionPath> executionPaths,
        string? unsupportedReason)
    {
        if (baseCaseEvidence.IsDefault)
        {
            throw new ArgumentException("Base-case evidence cannot be default.", nameof(baseCaseEvidence));
        }

        if (executionPaths.IsDefault)
        {
            throw new ArgumentException("Execution paths cannot be default.", nameof(executionPaths));
        }

        IsSupported = isSupported;
        BaseCaseEvidence = baseCaseEvidence;
        ExecutionPaths = executionPaths;
        UnsupportedReason = unsupportedReason;
    }

    internal bool IsSupported
    {
        get;
    }

    internal ImmutableArray<BaseCaseEvidence> BaseCaseEvidence
    {
        get;
    }

    internal ImmutableArray<RecursiveExecutionPath> ExecutionPaths
    {
        get;
    }

    internal string? UnsupportedReason
    {
        get;
    }

    internal bool HasBaseCaseEvidence => BaseCaseEvidence.Length > 0;

    internal bool HasDirectRecursiveCalls => ExecutionPaths.Any(path => path.RecursiveCallCount > 0);

    internal static RecursiveCallAnalysisResult Supported(
        ImmutableArray<BaseCaseEvidence> baseCaseEvidence,
        ImmutableArray<RecursiveExecutionPath> executionPaths)
    {
        return new RecursiveCallAnalysisResult(
            isSupported: true,
            baseCaseEvidence,
            executionPaths,
            unsupportedReason: null);
    }

    internal static RecursiveCallAnalysisResult Unsupported(string reason)
    {
        return new RecursiveCallAnalysisResult(
            isSupported: false,
            baseCaseEvidence: ImmutableArray<BaseCaseEvidence>.Empty,
            executionPaths: ImmutableArray<RecursiveExecutionPath>.Empty,
            unsupportedReason: reason);
    }
}

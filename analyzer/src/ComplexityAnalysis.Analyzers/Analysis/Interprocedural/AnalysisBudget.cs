using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class AnalysisBudget
{
    internal const int DefaultMaximumCallDepth = 5;
    internal const int DefaultMaximumMethodsPerRootAnalysis = 32;

    internal static readonly AnalysisBudget Default = new(
        DefaultMaximumCallDepth,
        DefaultMaximumMethodsPerRootAnalysis);

    internal AnalysisBudget(
        int maximumCallDepth,
        int maximumMethodsPerRootAnalysis)
    {
        if (maximumCallDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCallDepth), "Maximum call depth must be non-negative.");
        }

        if (maximumMethodsPerRootAnalysis < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMethodsPerRootAnalysis), "Maximum methods per root analysis must be non-negative.");
        }

        MaximumCallDepth = maximumCallDepth;
        MaximumMethodsPerRootAnalysis = maximumMethodsPerRootAnalysis;
    }

    internal int MaximumCallDepth
    {
        get;
    }

    internal int MaximumMethodsPerRootAnalysis
    {
        get;
    }
}

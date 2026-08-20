using System;

using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

namespace ComplexityAnalysis.Analyzers.Configuration;

internal sealed class ComplexityAnalyzerOptions : IEquatable<ComplexityAnalyzerOptions>
{
    internal const bool DefaultInterproceduralAnalysisEnabled = true;
    internal const bool DefaultRecursionAnalysisEnabled = true;
    internal const int DefaultMaxCallDepth = AnalysisBudget.DefaultMaximumCallDepth;
    internal const int DefaultMaxMethodsPerRoot = AnalysisBudget.DefaultMaximumMethodsPerRootAnalysis;
    internal const int MaximumMaxCallDepth = 16;
    internal const int MaximumMaxMethodsPerRoot = 128;

    internal static readonly ComplexityAnalyzerOptions Default = new(
        DefaultInterproceduralAnalysisEnabled,
        DefaultRecursionAnalysisEnabled,
        DefaultMaxCallDepth,
        DefaultMaxMethodsPerRoot,
        ComplexityThreshold.None);

    internal ComplexityAnalyzerOptions(
        bool interproceduralAnalysisEnabled,
        bool recursionAnalysisEnabled,
        int maxCallDepth,
        int maxMethodsPerRoot,
        ComplexityThreshold maximumComplexity)
    {
        if (maxCallDepth is < 0 or > MaximumMaxCallDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCallDepth), "Maximum call depth must be within the supported public range.");
        }

        if (maxMethodsPerRoot is < 0 or > MaximumMaxMethodsPerRoot)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMethodsPerRoot), "Maximum methods per root must be within the supported public range.");
        }

        InterproceduralAnalysisEnabled = interproceduralAnalysisEnabled;
        RecursionAnalysisEnabled = recursionAnalysisEnabled;
        MaxCallDepth = maxCallDepth;
        MaxMethodsPerRoot = maxMethodsPerRoot;
        MaximumComplexity = maximumComplexity ?? throw new ArgumentNullException(nameof(maximumComplexity));
    }

    internal bool InterproceduralAnalysisEnabled
    {
        get;
    }

    internal bool RecursionAnalysisEnabled
    {
        get;
    }

    internal int MaxCallDepth
    {
        get;
    }

    internal int MaxMethodsPerRoot
    {
        get;
    }

    internal ComplexityThreshold MaximumComplexity
    {
        get;
    }

    internal ComplexityAnalyzerOptions WithAnalysisBudget(AnalysisBudget budget)
    {
        _ = budget ?? throw new ArgumentNullException(nameof(budget));

        return MaxCallDepth == budget.MaximumCallDepth
            && MaxMethodsPerRoot == budget.MaximumMethodsPerRootAnalysis
            ? this
            : new ComplexityAnalyzerOptions(
                InterproceduralAnalysisEnabled,
                RecursionAnalysisEnabled,
                budget.MaximumCallDepth,
                budget.MaximumMethodsPerRootAnalysis,
                MaximumComplexity);
    }

    public bool Equals(ComplexityAnalyzerOptions? other)
    {
        return other is not null
            && InterproceduralAnalysisEnabled == other.InterproceduralAnalysisEnabled
            && RecursionAnalysisEnabled == other.RecursionAnalysisEnabled
            && MaxCallDepth == other.MaxCallDepth
            && MaxMethodsPerRoot == other.MaxMethodsPerRoot
            && MaximumComplexity.Equals(other.MaximumComplexity);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComplexityAnalyzerOptions);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = InterproceduralAnalysisEnabled.GetHashCode();
            hash = (hash * 397) ^ RecursionAnalysisEnabled.GetHashCode();
            hash = (hash * 397) ^ MaxCallDepth;
            hash = (hash * 397) ^ MaxMethodsPerRoot;
            hash = (hash * 397) ^ MaximumComplexity.GetHashCode();
            return hash;
        }
    }
}

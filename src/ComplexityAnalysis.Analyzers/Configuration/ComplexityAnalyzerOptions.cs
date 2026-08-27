using System;

using ComplexityAnalysis.Analyzers.Analysis;
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
        ComplexityThreshold.None,
        maximumCyclomaticComplexity: null,
        CyclomaticComplexityAnalysisMode.Standard,
        maximumNestingDepth: null,
        maximumMethodNloc: null,
        maximumStatementCount: null,
        maximumTokenCount: null,
        maximumParameters: null);

    internal ComplexityAnalyzerOptions(
        bool interproceduralAnalysisEnabled,
        bool recursionAnalysisEnabled,
        int maxCallDepth,
        int maxMethodsPerRoot,
        ComplexityThreshold maximumComplexity,
        int? maximumCyclomaticComplexity = null,
        CyclomaticComplexityAnalysisMode cyclomaticComplexityMode = CyclomaticComplexityAnalysisMode.Standard,
        int? maximumNestingDepth = null,
        int? maximumMethodNloc = null,
        int? maximumStatementCount = null,
        int? maximumTokenCount = null,
        int? maximumParameters = null)
    {
        if (maxCallDepth is < 0 or > MaximumMaxCallDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCallDepth), "Maximum call depth must be within the supported public range.");
        }

        if (maxMethodsPerRoot is < 0 or > MaximumMaxMethodsPerRoot)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMethodsPerRoot), "Maximum methods per root must be within the supported public range.");
        }

        if (maximumCyclomaticComplexity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCyclomaticComplexity), "Maximum cyclomatic complexity must be positive when configured.");
        }

        if (maximumNestingDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumNestingDepth), "Maximum nesting depth must be non-negative when configured.");
        }

        if (maximumMethodNloc < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMethodNloc), "Maximum method NLOC must be non-negative when configured.");
        }

        if (maximumStatementCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStatementCount), "Maximum statement count must be non-negative when configured.");
        }

        if (maximumTokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTokenCount), "Maximum token count must be non-negative when configured.");
        }

        if (maximumParameters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumParameters), "Maximum parameters must be non-negative when configured.");
        }

        InterproceduralAnalysisEnabled = interproceduralAnalysisEnabled;
        RecursionAnalysisEnabled = recursionAnalysisEnabled;
        MaxCallDepth = maxCallDepth;
        MaxMethodsPerRoot = maxMethodsPerRoot;
        MaximumComplexity = maximumComplexity ?? throw new ArgumentNullException(nameof(maximumComplexity));
        MaximumCyclomaticComplexity = maximumCyclomaticComplexity;
        CyclomaticComplexityMode = cyclomaticComplexityMode;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumMethodNloc = maximumMethodNloc;
        MaximumStatementCount = maximumStatementCount;
        MaximumTokenCount = maximumTokenCount;
        MaximumParameters = maximumParameters;
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

    internal int? MaximumCyclomaticComplexity
    {
        get;
    }

    internal CyclomaticComplexityAnalysisMode CyclomaticComplexityMode
    {
        get;
    }

    internal int? MaximumNestingDepth
    {
        get;
    }

    internal int? MaximumMethodNloc
    {
        get;
    }

    internal int? MaximumStatementCount
    {
        get;
    }

    internal int? MaximumTokenCount
    {
        get;
    }

    internal int? MaximumParameters
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
                MaximumComplexity,
                MaximumCyclomaticComplexity,
                CyclomaticComplexityMode,
                MaximumNestingDepth,
                MaximumMethodNloc,
                MaximumStatementCount,
                MaximumTokenCount,
                MaximumParameters);
    }

    public bool Equals(ComplexityAnalyzerOptions? other)
    {
        return other is not null
            && InterproceduralAnalysisEnabled == other.InterproceduralAnalysisEnabled
            && RecursionAnalysisEnabled == other.RecursionAnalysisEnabled
            && MaxCallDepth == other.MaxCallDepth
            && MaxMethodsPerRoot == other.MaxMethodsPerRoot
            && MaximumComplexity.Equals(other.MaximumComplexity)
            && MaximumCyclomaticComplexity == other.MaximumCyclomaticComplexity
            && CyclomaticComplexityMode == other.CyclomaticComplexityMode
            && MaximumNestingDepth == other.MaximumNestingDepth
            && MaximumMethodNloc == other.MaximumMethodNloc
            && MaximumStatementCount == other.MaximumStatementCount
            && MaximumTokenCount == other.MaximumTokenCount
            && MaximumParameters == other.MaximumParameters;
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
            hash = (hash * 397) ^ MaximumCyclomaticComplexity.GetHashCode();
            hash = (hash * 397) ^ CyclomaticComplexityMode.GetHashCode();
            hash = (hash * 397) ^ MaximumNestingDepth.GetHashCode();
            hash = (hash * 397) ^ MaximumMethodNloc.GetHashCode();
            hash = (hash * 397) ^ MaximumStatementCount.GetHashCode();
            hash = (hash * 397) ^ MaximumTokenCount.GetHashCode();
            hash = (hash * 397) ^ MaximumParameters.GetHashCode();
            return hash;
        }
    }
}

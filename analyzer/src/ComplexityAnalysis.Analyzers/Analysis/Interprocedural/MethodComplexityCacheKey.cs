using System;
using System.Collections.Generic;

using ComplexityAnalysis.Analyzers.Configuration;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal readonly struct MethodComplexityCacheKey : IEquatable<MethodComplexityCacheKey>
{
    internal static readonly IEqualityComparer<MethodComplexityCacheKey> Comparer = new MethodComplexityCacheKeyComparer();

    private readonly MethodSymbolKey method;
    private readonly bool interproceduralAnalysisEnabled;
    private readonly bool recursionAnalysisEnabled;
    private readonly int maximumCallDepth;
    private readonly int maximumMethodsPerRootAnalysis;

    private MethodComplexityCacheKey(
        MethodSymbolKey method,
        bool interproceduralAnalysisEnabled,
        bool recursionAnalysisEnabled,
        int maximumCallDepth,
        int maximumMethodsPerRootAnalysis)
    {
        this.method = method;
        this.interproceduralAnalysisEnabled = interproceduralAnalysisEnabled;
        this.recursionAnalysisEnabled = recursionAnalysisEnabled;
        this.maximumCallDepth = maximumCallDepth;
        this.maximumMethodsPerRootAnalysis = maximumMethodsPerRootAnalysis;
    }

    internal static MethodComplexityCacheKey Create(
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        return new MethodComplexityCacheKey(
            MethodSymbolKey.Create(methodSymbol),
            options.InterproceduralAnalysisEnabled,
            options.RecursionAnalysisEnabled,
            options.MaxCallDepth,
            options.MaxMethodsPerRoot);
    }

    public bool Equals(MethodComplexityCacheKey other)
    {
        return method.Equals(other.method)
            && interproceduralAnalysisEnabled == other.interproceduralAnalysisEnabled
            && recursionAnalysisEnabled == other.recursionAnalysisEnabled
            && maximumCallDepth == other.maximumCallDepth
            && maximumMethodsPerRootAnalysis == other.maximumMethodsPerRootAnalysis;
    }

    public override bool Equals(object? obj)
    {
        return obj is MethodComplexityCacheKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = method.GetHashCode();
            hash = (hash * 397) ^ interproceduralAnalysisEnabled.GetHashCode();
            hash = (hash * 397) ^ recursionAnalysisEnabled.GetHashCode();
            hash = (hash * 397) ^ maximumCallDepth;
            hash = (hash * 397) ^ maximumMethodsPerRootAnalysis;
            return hash;
        }
    }

    private sealed class MethodComplexityCacheKeyComparer : IEqualityComparer<MethodComplexityCacheKey>
    {
        public bool Equals(MethodComplexityCacheKey x, MethodComplexityCacheKey y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(MethodComplexityCacheKey obj)
        {
            return obj.GetHashCode();
        }
    }
}

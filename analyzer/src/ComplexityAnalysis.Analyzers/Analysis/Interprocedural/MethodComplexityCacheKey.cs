using System;
using System.Collections.Generic;

using ComplexityAnalysis.Analyzers.Configuration;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal readonly struct MethodComplexityCacheKey : IEquatable<MethodComplexityCacheKey>
{
    internal static readonly IEqualityComparer<MethodComplexityCacheKey> Comparer = new MethodComplexityCacheKeyComparer();

    private readonly MethodSymbolKey _method;
    private readonly bool _interproceduralAnalysisEnabled;
    private readonly bool _recursionAnalysisEnabled;
    private readonly int _maximumCallDepth;
    private readonly int _maximumMethodsPerRootAnalysis;

    private MethodComplexityCacheKey(
        MethodSymbolKey method,
        bool interproceduralAnalysisEnabled,
        bool recursionAnalysisEnabled,
        int maximumCallDepth,
        int maximumMethodsPerRootAnalysis)
    {
        _method = method;
        _interproceduralAnalysisEnabled = interproceduralAnalysisEnabled;
        _recursionAnalysisEnabled = recursionAnalysisEnabled;
        _maximumCallDepth = maximumCallDepth;
        _maximumMethodsPerRootAnalysis = maximumMethodsPerRootAnalysis;
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
        return _method.Equals(other._method)
            && _interproceduralAnalysisEnabled == other._interproceduralAnalysisEnabled
            && _recursionAnalysisEnabled == other._recursionAnalysisEnabled
            && _maximumCallDepth == other._maximumCallDepth
            && _maximumMethodsPerRootAnalysis == other._maximumMethodsPerRootAnalysis;
    }

    public override bool Equals(object? obj)
    {
        return obj is MethodComplexityCacheKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = _method.GetHashCode();
            hash = (hash * 397) ^ _interproceduralAnalysisEnabled.GetHashCode();
            hash = (hash * 397) ^ _recursionAnalysisEnabled.GetHashCode();
            hash = (hash * 397) ^ _maximumCallDepth;
            hash = (hash * 397) ^ _maximumMethodsPerRootAnalysis;
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

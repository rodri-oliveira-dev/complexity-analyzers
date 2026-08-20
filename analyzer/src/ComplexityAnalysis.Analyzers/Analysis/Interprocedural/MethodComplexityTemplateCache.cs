using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using ComplexityAnalysis.Analyzers.Configuration;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class MethodComplexityTemplateCache
{
    private readonly ConcurrentDictionary<MethodComplexityCacheKey, MethodComplexityCacheEntry> _entries =
        new(MethodComplexityCacheKey.Comparer);

    internal int Count
        => _entries.Count;

    internal bool TryGetCompleted(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken,
        out InterproceduralAnalysisResult result)
    {
        return TryGetCompleted(
            methodSymbol,
            ComplexityAnalyzerOptions.Default,
            cancellationToken,
            out result);
    }

    internal bool TryGetCompleted(
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken,
        out InterproceduralAnalysisResult result)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        if (_entries.TryGetValue(MethodComplexityCacheKey.Create(methodSymbol, options), out MethodComplexityCacheEntry? entry)
            && entry.Kind == MethodComplexityCacheEntryKind.Completed
            && entry.Result is not null)
        {
            result = entry.Result;
            return true;
        }

        result = InterproceduralAnalysisResult.Unknown(string.Empty);
        return false;
    }

    internal bool TryReserveAnalysis(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken,
        out InterproceduralAnalysisResult? completedResult)
    {
        return TryReserveAnalysis(
            methodSymbol,
            ComplexityAnalyzerOptions.Default,
            cancellationToken,
            out completedResult);
    }

    internal bool TryReserveAnalysis(
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken,
        out InterproceduralAnalysisResult? completedResult)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        MethodComplexityCacheKey key = MethodComplexityCacheKey.Create(methodSymbol, options);
        if (_entries.TryAdd(key, MethodComplexityCacheEntry.InProgress()))
        {
            completedResult = null;
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        completedResult = _entries.TryGetValue(key, out MethodComplexityCacheEntry? existing)
            && existing.Kind == MethodComplexityCacheEntryKind.Completed
            ? existing.Result
            : null;

        return false;
    }

    internal void StoreCompleted(
        IMethodSymbol methodSymbol,
        InterproceduralAnalysisResult result,
        CancellationToken cancellationToken)
    {
        StoreCompleted(
            methodSymbol,
            ComplexityAnalyzerOptions.Default,
            result,
            cancellationToken);
    }

    internal void StoreCompleted(
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options,
        InterproceduralAnalysisResult result,
        CancellationToken cancellationToken)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));
        _ = result ?? throw new ArgumentNullException(nameof(result));

        cancellationToken.ThrowIfCancellationRequested();

        _entries[MethodComplexityCacheKey.Create(methodSymbol, options)] = MethodComplexityCacheEntry.Completed(result);
    }

    internal bool AbandonAnalysis(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        return AbandonAnalysis(
            methodSymbol,
            ComplexityAnalyzerOptions.Default,
            cancellationToken);
    }

    internal bool AbandonAnalysis(
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        MethodComplexityCacheKey key = MethodComplexityCacheKey.Create(methodSymbol, options);
        return _entries.TryGetValue(key, out MethodComplexityCacheEntry? existing)
            && existing.Kind == MethodComplexityCacheEntryKind.InProgress
            && ((ICollection<KeyValuePair<MethodComplexityCacheKey, MethodComplexityCacheEntry>>)_entries)
                .Remove(new KeyValuePair<MethodComplexityCacheKey, MethodComplexityCacheEntry>(key, existing));
    }
}

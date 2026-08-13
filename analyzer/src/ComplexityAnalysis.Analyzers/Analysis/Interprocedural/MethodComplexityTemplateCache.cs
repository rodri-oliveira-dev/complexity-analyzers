using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class MethodComplexityTemplateCache
{
    private readonly ConcurrentDictionary<MethodSymbolKey, MethodComplexityCacheEntry> entries =
        new(MethodSymbolKey.Comparer);

    internal int Count
        => entries.Count;

    internal bool TryGetCompleted(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken,
        out InterproceduralAnalysisResult result)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        if (entries.TryGetValue(MethodSymbolKey.Create(methodSymbol), out MethodComplexityCacheEntry? entry)
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
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        MethodSymbolKey key = MethodSymbolKey.Create(methodSymbol);
        if (entries.TryAdd(key, MethodComplexityCacheEntry.InProgress()))
        {
            completedResult = null;
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        completedResult = entries.TryGetValue(key, out MethodComplexityCacheEntry? existing)
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
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = result ?? throw new ArgumentNullException(nameof(result));

        cancellationToken.ThrowIfCancellationRequested();

        entries[MethodSymbolKey.Create(methodSymbol)] = MethodComplexityCacheEntry.Completed(result);
    }

    internal bool AbandonAnalysis(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        MethodSymbolKey key = MethodSymbolKey.Create(methodSymbol);
        return entries.TryGetValue(key, out MethodComplexityCacheEntry? existing)
            && existing.Kind == MethodComplexityCacheEntryKind.InProgress
            && ((ICollection<KeyValuePair<MethodSymbolKey, MethodComplexityCacheEntry>>)entries)
                .Remove(new KeyValuePair<MethodSymbolKey, MethodComplexityCacheEntry>(key, existing));
    }
}

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal readonly struct MethodSymbolKey : IEquatable<MethodSymbolKey>
{
    internal static readonly IEqualityComparer<MethodSymbolKey> Comparer = new MethodSymbolKeyComparer();

    private readonly IMethodSymbol originalDefinition;

    private MethodSymbolKey(IMethodSymbol originalDefinition)
    {
        this.originalDefinition = originalDefinition;
    }

    internal static MethodSymbolKey Create(IMethodSymbol methodSymbol)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        IMethodSymbol sourceMethod = methodSymbol.ReducedFrom ?? methodSymbol;
        return new MethodSymbolKey(sourceMethod.OriginalDefinition);
    }

    public bool Equals(MethodSymbolKey other)
    {
        return SymbolEqualityComparer.Default.Equals(originalDefinition, other.originalDefinition);
    }

    public override bool Equals(object? obj)
    {
        return obj is MethodSymbolKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return SymbolEqualityComparer.Default.GetHashCode(originalDefinition);
    }

    private sealed class MethodSymbolKeyComparer : IEqualityComparer<MethodSymbolKey>
    {
        public bool Equals(MethodSymbolKey x, MethodSymbolKey y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(MethodSymbolKey obj)
        {
            return obj.GetHashCode();
        }
    }
}

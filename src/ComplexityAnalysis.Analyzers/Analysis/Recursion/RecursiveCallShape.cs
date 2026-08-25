using System;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecursiveCallShape : IEquatable<RecursiveCallShape>
{
    internal RecursiveCallShape(
        IMethodSymbol targetMethod,
        ImmutableArray<RecursiveArgumentRelation> argumentRelations)
    {
        TargetMethod = targetMethod ?? throw new ArgumentNullException(nameof(targetMethod));

        if (argumentRelations.IsDefault)
        {
            throw new ArgumentException("Recursive argument relations cannot be default.", nameof(argumentRelations));
        }

        if (argumentRelations.Any(relation => relation is null))
        {
            throw new ArgumentException("Recursive argument relations cannot contain null entries.", nameof(argumentRelations));
        }

        ArgumentRelations = argumentRelations;
    }

    internal IMethodSymbol TargetMethod
    {
        get;
    }

    internal ImmutableArray<RecursiveArgumentRelation> ArgumentRelations
    {
        get;
    }

    internal ImmutableArray<RecursiveArgumentRelation> ReducingArgumentRelations => ArgumentRelations
        .Where(relation => relation.IsReducing)
        .ToImmutableArray();

    public bool Equals(RecursiveCallShape? other)
    {
        return other is not null
            && SymbolEqualityComparer.Default.Equals(TargetMethod, other.TargetMethod)
            && ArgumentRelations.SequenceEqual(other.ArgumentRelations);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RecursiveCallShape);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = SymbolEqualityComparer.Default.GetHashCode(TargetMethod);
            foreach (RecursiveArgumentRelation relation in ArgumentRelations)
            {
                hash = (hash * 397) ^ relation.GetHashCode();
            }

            return hash;
        }
    }
}

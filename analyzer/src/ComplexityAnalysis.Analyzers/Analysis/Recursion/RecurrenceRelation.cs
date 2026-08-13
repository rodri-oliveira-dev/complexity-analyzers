using System;
using System.Collections.Immutable;
using System.Linq;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceRelation : IEquatable<RecurrenceRelation>
{
    internal RecurrenceRelation(
        ComplexityVariable complexityVariable,
        ImmutableArray<RecurrenceTerm> recursiveTerms,
        ComplexityExpression nonRecursiveWork)
    {
        ComplexityVariable = complexityVariable ?? throw new ArgumentNullException(nameof(complexityVariable));
        NonRecursiveWork = nonRecursiveWork ?? throw new ArgumentNullException(nameof(nonRecursiveWork));

        if (recursiveTerms.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A recurrence relation must contain at least one recursive term.", nameof(recursiveTerms));
        }

        if (recursiveTerms.Any(term => term is null))
        {
            throw new ArgumentException("Recursive terms cannot contain null entries.", nameof(recursiveTerms));
        }

        RecursiveTerms = recursiveTerms;
    }

    internal ComplexityVariable ComplexityVariable
    {
        get;
    }

    internal ImmutableArray<RecurrenceTerm> RecursiveTerms
    {
        get;
    }

    internal ComplexityExpression NonRecursiveWork
    {
        get;
    }

    public bool Equals(RecurrenceRelation? other)
    {
        return other is not null
            && ComplexityVariable.Equals(other.ComplexityVariable)
            && NonRecursiveWork.Equals(other.NonRecursiveWork)
            && RecursiveTerms.SequenceEqual(other.RecursiveTerms);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RecurrenceRelation);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ComplexityVariable.GetHashCode();
            foreach (RecurrenceTerm term in RecursiveTerms)
            {
                hash = (hash * 397) ^ term.GetHashCode();
            }

            hash = (hash * 397) ^ NonRecursiveWork.GetHashCode();
            return hash;
        }
    }
}

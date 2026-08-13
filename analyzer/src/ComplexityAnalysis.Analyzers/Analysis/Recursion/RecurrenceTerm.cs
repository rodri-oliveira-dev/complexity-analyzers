using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceTerm : IEquatable<RecurrenceTerm>
{
    internal RecurrenceTerm(int multiplicity, RecurrenceReduction reduction)
    {
        if (multiplicity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplicity), "Multiplicity must be greater than zero.");
        }

        Multiplicity = multiplicity;
        Reduction = reduction ?? throw new ArgumentNullException(nameof(reduction));
    }

    internal int Multiplicity
    {
        get;
    }

    internal RecurrenceReduction Reduction
    {
        get;
    }

    public bool Equals(RecurrenceTerm? other)
    {
        return other is not null
            && Multiplicity == other.Multiplicity
            && Reduction.Equals(other.Reduction);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RecurrenceTerm);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Multiplicity * 397) ^ Reduction.GetHashCode();
        }
    }
}

using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class ComplexityAssumption : IEquatable<ComplexityAssumption>
{
    internal ComplexityAssumption(ComplexityAssumptionKind kind, string description)
    {
        if (!Enum.IsDefined(typeof(ComplexityAssumptionKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        _ = description ?? throw new ArgumentNullException(nameof(description));
        if (description.Length == 0)
        {
            throw new ArgumentException("Assumption description must not be empty.", nameof(description));
        }

        Kind = kind;
        Description = description;
    }

    internal ComplexityAssumptionKind Kind
    {
        get;
    }

    internal string Description
    {
        get;
    }

    public bool Equals(ComplexityAssumption? other)
    {
        return other is not null
            && Kind == other.Kind
            && StringComparer.Ordinal.Equals(Description, other.Description);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComplexityAssumption);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Description);
        }
    }
}

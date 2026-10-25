using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Evidence;

internal readonly struct ComplexityEvidenceLocation : IEquatable<ComplexityEvidenceLocation>
{
    internal ComplexityEvidenceLocation(int relativeStart, int length)
    {
        if (relativeStart < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeStart));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        RelativeStart = relativeStart;
        Length = length;
    }

    internal int RelativeStart
    {
        get;
    }

    internal int Length
    {
        get;
    }

    public bool Equals(ComplexityEvidenceLocation other)
    {
        return RelativeStart == other.RelativeStart && Length == other.Length;
    }

    public override bool Equals(object? obj)
    {
        return obj is ComplexityEvidenceLocation other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (RelativeStart * 397) ^ Length;
        }
    }
}

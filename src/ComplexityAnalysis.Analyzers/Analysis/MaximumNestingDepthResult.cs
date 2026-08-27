using System;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct MaximumNestingDepthResult : IEquatable<MaximumNestingDepthResult>
{
    internal MaximumNestingDepthResult(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Maximum nesting depth must be non-negative.");
        }

        Value = value;
    }

    internal int Value
    {
        get;
    }

    public bool Equals(MaximumNestingDepthResult other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is MaximumNestingDepthResult other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

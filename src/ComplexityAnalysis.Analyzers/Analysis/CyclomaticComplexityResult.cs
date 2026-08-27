using System;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct CyclomaticComplexityResult : IEquatable<CyclomaticComplexityResult>
{
    internal CyclomaticComplexityResult(int value)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Cyclomatic complexity must be positive.");
        }

        Value = value;
    }

    internal int Value
    {
        get;
    }

    public bool Equals(CyclomaticComplexityResult other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is CyclomaticComplexityResult other && Equals(other);
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

using System;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct CognitiveComplexity : IEquatable<CognitiveComplexity>
{
    internal CognitiveComplexity(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Cognitive complexity must be non-negative.");
        }

        Value = value;
    }

    internal int Value
    {
        get;
    }

    public bool Equals(CognitiveComplexity other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is CognitiveComplexity other && Equals(other);
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

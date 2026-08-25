using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class UnknownComplexity : ComplexityExpression, IEquatable<UnknownComplexity>
{
    internal static readonly UnknownComplexity Instance = new();

    private UnknownComplexity()
    {
    }

    public bool Equals(UnknownComplexity? other)
    {
        return other is not null;
    }

    public override bool Equals(object? obj)
    {
        return obj is UnknownComplexity;
    }

    public override int GetHashCode()
    {
        return typeof(UnknownComplexity).GetHashCode();
    }

    internal override string ToBigOBody()
    {
        return "Unknown";
    }

    internal override string ToBigONotation()
    {
        return "Unknown";
    }
}

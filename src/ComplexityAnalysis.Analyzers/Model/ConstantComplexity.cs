using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class ConstantComplexity : ComplexityExpression, IEquatable<ConstantComplexity>
{
    internal static readonly ConstantComplexity Instance = new();

    private ConstantComplexity()
    {
    }

    public bool Equals(ConstantComplexity? other)
    {
        return other is not null;
    }

    public override bool Equals(object? obj)
    {
        return obj is ConstantComplexity;
    }

    public override int GetHashCode()
    {
        return typeof(ConstantComplexity).GetHashCode();
    }

    internal override string ToBigOBody()
    {
        return "1";
    }
}

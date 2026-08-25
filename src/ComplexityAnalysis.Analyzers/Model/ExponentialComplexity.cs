using System;
namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class ExponentialComplexity : ComplexityExpression, IEquatable<ExponentialComplexity>
{
    internal ExponentialComplexity(ComplexityVariable variable, double @base)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));

        Base = ExponentialBaseNormalizer.Normalize(@base);
    }

    internal ComplexityVariable Variable
    {
        get;
    }

    internal double Base
    {
        get;
    }

    public bool Equals(ExponentialComplexity? other)
    {
        return other is not null
            && Base.CompareTo(other.Base) == 0
            && Variable.Equals(other.Variable);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ExponentialComplexity);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Variable.GetHashCode() * 397) ^ Base.GetHashCode();
        }
    }

    internal override string ToBigOBody()
    {
        return ExponentialBaseNormalizer.Format(Base) + "^" + Variable.Name;
    }
}

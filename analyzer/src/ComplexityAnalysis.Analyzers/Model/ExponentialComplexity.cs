using System;
using System.Globalization;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class ExponentialComplexity : ComplexityExpression, IEquatable<ExponentialComplexity>
{
    internal ExponentialComplexity(ComplexityVariable variable, double @base)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));

        if (double.IsNaN(@base) || double.IsInfinity(@base) || @base <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(@base), "Exponential base must be finite and greater than one.");
        }

        Base = @base;
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
            && Base.Equals(other.Base)
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

    internal override string ToBigONotation()
    {
        return "O(" + Base.ToString("G", CultureInfo.InvariantCulture) + "^" + Variable.Name + ")";
    }
}

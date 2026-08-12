using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class FactorialComplexity : ComplexityExpression, IEquatable<FactorialComplexity>
{
    internal FactorialComplexity(ComplexityVariable variable)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));
    }

    internal ComplexityVariable Variable
    {
        get;
    }

    public bool Equals(FactorialComplexity? other)
    {
        return other is not null && Variable.Equals(other.Variable);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FactorialComplexity);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Variable.GetHashCode() * 397) ^ typeof(FactorialComplexity).GetHashCode();
        }
    }

    internal override string ToBigONotation()
    {
        return "O(" + Variable.Name + "!)";
    }
}

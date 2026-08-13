using System;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class BaseCaseEvidence : IEquatable<BaseCaseEvidence>
{
    internal BaseCaseEvidence(IParameterSymbol parameter, ComplexityVariable variable)
    {
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));
    }

    internal IParameterSymbol Parameter
    {
        get;
    }

    internal ComplexityVariable Variable
    {
        get;
    }

    public bool Equals(BaseCaseEvidence? other)
    {
        return other is not null
            && SymbolEqualityComparer.Default.Equals(Parameter, other.Parameter)
            && Variable.Equals(other.Variable);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as BaseCaseEvidence);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (SymbolEqualityComparer.Default.GetHashCode(Parameter) * 397) ^ Variable.GetHashCode();
        }
    }
}

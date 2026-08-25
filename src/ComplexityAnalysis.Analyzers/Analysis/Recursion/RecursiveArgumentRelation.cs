using System;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecursiveArgumentRelation : IEquatable<RecursiveArgumentRelation>
{
    internal RecursiveArgumentRelation(
        IParameterSymbol parameter,
        ComplexityVariable variable,
        RecursiveArgumentRelationKind kind,
        RecurrenceReduction? reduction)
    {
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));

        if (!Enum.IsDefined(typeof(RecursiveArgumentRelationKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown recursive argument relation kind.");
        }

        if (kind == RecursiveArgumentRelationKind.Reducing && reduction is null)
        {
            throw new ArgumentException("A reducing recursive argument relation must include a reduction.", nameof(reduction));
        }

        if (kind != RecursiveArgumentRelationKind.Reducing && reduction is not null)
        {
            throw new ArgumentException("Only reducing recursive argument relations can include a reduction.", nameof(reduction));
        }

        Kind = kind;
        Reduction = reduction;
    }

    internal IParameterSymbol Parameter
    {
        get;
    }

    internal ComplexityVariable Variable
    {
        get;
    }

    internal RecursiveArgumentRelationKind Kind
    {
        get;
    }

    internal RecurrenceReduction? Reduction
    {
        get;
    }

    internal bool IsReducing => Kind == RecursiveArgumentRelationKind.Reducing;

    internal static RecursiveArgumentRelation Unknown(
        IParameterSymbol parameter,
        ComplexityVariable variable)
    {
        return new RecursiveArgumentRelation(
            parameter,
            variable,
            RecursiveArgumentRelationKind.Unknown,
            reduction: null);
    }

    internal static RecursiveArgumentRelation Unchanged(
        IParameterSymbol parameter,
        ComplexityVariable variable)
    {
        return new RecursiveArgumentRelation(
            parameter,
            variable,
            RecursiveArgumentRelationKind.Unchanged,
            reduction: null);
    }

    internal static RecursiveArgumentRelation Increasing(
        IParameterSymbol parameter,
        ComplexityVariable variable)
    {
        return new RecursiveArgumentRelation(
            parameter,
            variable,
            RecursiveArgumentRelationKind.Increasing,
            reduction: null);
    }

    internal static RecursiveArgumentRelation Reducing(
        IParameterSymbol parameter,
        ComplexityVariable variable,
        RecurrenceReduction reduction)
    {
        return new RecursiveArgumentRelation(
            parameter,
            variable,
            RecursiveArgumentRelationKind.Reducing,
            reduction);
    }

    public bool Equals(RecursiveArgumentRelation? other)
    {
        return other is not null
            && SymbolEqualityComparer.Default.Equals(Parameter, other.Parameter)
            && Variable.Equals(other.Variable)
            && Kind == other.Kind
            && Equals(Reduction, other.Reduction);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RecursiveArgumentRelation);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = SymbolEqualityComparer.Default.GetHashCode(Parameter);
            hash = (hash * 397) ^ Variable.GetHashCode();
            hash = (hash * 397) ^ Kind.GetHashCode();
            hash = (hash * 397) ^ (Reduction?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

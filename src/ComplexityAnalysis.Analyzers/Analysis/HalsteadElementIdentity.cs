using System;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class HalsteadElementIdentity : IEquatable<HalsteadElementIdentity>
{
    private HalsteadElementIdentity(
        HalsteadElementRole role,
        string kind,
        string canonicalValue)
    {
        Role = role;
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        CanonicalValue = canonicalValue ?? throw new ArgumentNullException(nameof(canonicalValue));
    }

    internal HalsteadElementRole Role
    {
        get;
    }

    internal string Kind
    {
        get;
    }

    internal string CanonicalValue
    {
        get;
    }

    internal static HalsteadElementIdentity ForOperator(HalsteadOperatorKind kind)
    {
        return new HalsteadElementIdentity(
            HalsteadElementRole.Operator,
            kind.ToString(),
            string.Empty);
    }

    internal static HalsteadElementIdentity ForOperand(
        HalsteadOperandKind kind,
        string canonicalValue)
    {
        return string.IsNullOrEmpty(canonicalValue)
            ? throw new ArgumentException("Operand identity must have a canonical value.", nameof(canonicalValue))
            : new HalsteadElementIdentity(
                HalsteadElementRole.Operand,
                kind.ToString(),
                canonicalValue);
    }

    public bool Equals(HalsteadElementIdentity? other)
    {
        return other is not null
            && Role == other.Role
            && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
            && string.Equals(CanonicalValue, other.CanonicalValue, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as HalsteadElementIdentity);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Role;
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Kind);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CanonicalValue);
            return hash;
        }
    }

    public override string ToString()
    {
        return CanonicalValue.Length == 0
            ? Role + ":" + Kind
            : Role + ":" + Kind + ":" + CanonicalValue;
    }
}

using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class PolynomialLogComplexity : ComplexityExpression, IEquatable<PolynomialLogComplexity>
{
    internal PolynomialLogComplexity(ComplexityVariable variable, int polynomialDegree, int logExponent)
        : this(variable, (double)polynomialDegree, logExponent)
    {
    }

    internal PolynomialLogComplexity(ComplexityVariable variable, double polynomialDegree, int logExponent)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));

        if (logExponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logExponent), "Log exponent must be non-negative.");
        }

        double normalizedPolynomialDegree = PolynomialExponentNormalizer.Normalize(polynomialDegree);
        if (IsZero(normalizedPolynomialDegree) && logExponent == 0)
        {
            throw new ArgumentException("Use ConstantComplexity for O(1).", nameof(polynomialDegree));
        }

        PolynomialDegree = normalizedPolynomialDegree;
        LogExponent = logExponent;
    }

    internal ComplexityVariable Variable
    {
        get;
    }

    internal double PolynomialDegree
    {
        get;
    }

    internal int LogExponent
    {
        get;
    }

    public bool Equals(PolynomialLogComplexity? other)
    {
        return other is not null
            && Math.Abs(PolynomialDegree - other.PolynomialDegree) <= PolynomialExponentNormalizer.IntegerTolerance
            && LogExponent == other.LogExponent
            && Variable.Equals(other.Variable);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PolynomialLogComplexity);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Variable.GetHashCode();
            hash = (hash * 397) ^ PolynomialDegree.GetHashCode();
            hash = (hash * 397) ^ LogExponent;
            return hash;
        }
    }

    internal override string ToBigOBody()
    {
        return IsZero(PolynomialDegree)
            ? FormatLogTerm()
            : FormatPolynomialLogBody();
    }

    private string FormatPolynomialLogBody()
    {
        return LogExponent == 0
            ? FormatPolynomialTerm()
            : FormatPolynomialTerm() + " " + FormatLogTerm();
    }

    private string FormatPolynomialTerm()
    {
        return IsEquivalentDegree(1)
            ? Variable.Name
            : IsEquivalentDegree(2)
            ? Variable.Name + "\u00b2"
            : IsEquivalentDegree(3)
            ? Variable.Name + "\u00b3"
            : Variable.Name + "^" + PolynomialExponentNormalizer.Format(PolynomialDegree);
    }

    private string FormatLogTerm()
    {
        return LogExponent == 1
            ? "log " + Variable.Name
            : "log^" + LogExponent.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Variable.Name;
    }

    private bool IsEquivalentDegree(double degree)
    {
        return Math.Abs(PolynomialDegree - degree) <= PolynomialExponentNormalizer.IntegerTolerance;
    }

    private static bool IsZero(double value)
    {
        return Math.Abs(value) <= PolynomialExponentNormalizer.IntegerTolerance;
    }
}

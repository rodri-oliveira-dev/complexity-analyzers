using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class PolynomialLogComplexity : ComplexityExpression, IEquatable<PolynomialLogComplexity>
{
    internal PolynomialLogComplexity(ComplexityVariable variable, int polynomialDegree, int logExponent)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));

        if (polynomialDegree < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(polynomialDegree), "Polynomial degree must be non-negative.");
        }

        if (logExponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logExponent), "Log exponent must be non-negative.");
        }

        if (polynomialDegree == 0 && logExponent == 0)
        {
            throw new ArgumentException("Use ConstantComplexity for O(1).", nameof(polynomialDegree));
        }

        PolynomialDegree = polynomialDegree;
        LogExponent = logExponent;
    }

    internal ComplexityVariable Variable
    {
        get;
    }

    internal int PolynomialDegree
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
            && PolynomialDegree == other.PolynomialDegree
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
            hash = (hash * 397) ^ PolynomialDegree;
            hash = (hash * 397) ^ LogExponent;
            return hash;
        }
    }

    internal override string ToBigONotation()
    {
        return PolynomialDegree == 0
            ? "O(" + FormatLogTerm() + ")"
            : FormatPolynomialLogNotation();
    }

    private string FormatPolynomialLogNotation()
    {
        return LogExponent == 0
            ? "O(" + FormatPolynomialTerm() + ")"
            : "O(" + FormatPolynomialTerm() + " " + FormatLogTerm() + ")";
    }

    private string FormatPolynomialTerm()
    {
        return PolynomialDegree switch
        {
            1 => Variable.Name,
            2 => Variable.Name + "\u00b2",
            3 => Variable.Name + "\u00b3",
            _ => Variable.Name + "^" + PolynomialDegree.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private string FormatLogTerm()
    {
        return LogExponent == 1
            ? "log " + Variable.Name
            : "log^" + LogExponent.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Variable.Name;
    }
}

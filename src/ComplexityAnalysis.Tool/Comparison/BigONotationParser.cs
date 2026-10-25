using System.Globalization;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Tool.Comparison;

internal static class BigONotationParser
{
    internal static bool TryParse(string? notation, out ComplexityExpression expression)
    {
        expression = ComplexityFactory.Unknown();
        if (notation is null || !notation.StartsWith("O(", StringComparison.Ordinal) || !notation.EndsWith(')'))
        {
            return false;
        }

        string body = notation[2..^1];
        if (StringComparer.Ordinal.Equals(body, "1"))
        {
            expression = ComplexityFactory.Constant();
            return true;
        }

        if (body.EndsWith('!'))
        {
            return TryVariable(body[..^1], out ComplexityVariable? variable)
                && TryCreate(() => ComplexityFactory.Factorial(variable), out expression);
        }

        int exponentialSeparator = body.IndexOf('^');
        if (exponentialSeparator > 0
            && double.TryParse(body[..exponentialSeparator], NumberStyles.Float, CultureInfo.InvariantCulture, out double baseValue)
            && double.IsFinite(baseValue)
            && TryVariable(body[(exponentialSeparator + 1)..], out ComplexityVariable? exponentialVariable))
        {
            return TryCreate(
                () => ComplexityFactory.Exponential(exponentialVariable, baseValue),
                out expression);
        }

        if (body.StartsWith("log ", StringComparison.Ordinal))
        {
            return TryVariable(body[4..], out ComplexityVariable? variable)
                && TryCreate(() => ComplexityFactory.LogN(variable), out expression);
        }

        int logSeparator = body.IndexOf(" log ", StringComparison.Ordinal);
        string polynomial = logSeparator < 0 ? body : body[..logSeparator];
        if (!TryPolynomial(polynomial, out ComplexityVariable? polynomialVariable, out double degree))
        {
            return false;
        }

        if (logSeparator >= 0 && !StringComparer.Ordinal.Equals(body[(logSeparator + 5)..], polynomialVariable.Name))
        {
            return false;
        }

        return TryCreate(
            () => logSeparator < 0
                ? ComplexityFactory.Polynomial(polynomialVariable, degree)
                : Math.Abs(degree - 1d) <= double.Epsilon
                    ? ComplexityFactory.NLogN(polynomialVariable)
                    : new PolynomialLogComplexity(polynomialVariable, degree, logExponent: 1),
            out expression);
    }

    private static bool TryPolynomial(string value, out ComplexityVariable variable, out double degree)
    {
        degree = 1;
        string name = value;
        if (value.EndsWith('²'))
        {
            name = value[..^1];
            degree = 2;
        }
        else if (value.EndsWith('³'))
        {
            name = value[..^1];
            degree = 3;
        }
        else
        {
            int separator = value.IndexOf('^');
            if (separator >= 0)
            {
                name = value[..separator];
                if (!double.TryParse(value[(separator + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out degree))
                {
                    variable = null!;
                    return false;
                }

                if (!double.IsFinite(degree))
                {
                    variable = null!;
                    return false;
                }
            }
        }

        return TryVariable(name, out variable);
    }

    private static bool TryVariable(string value, out ComplexityVariable variable)
    {
        try
        {
            variable = new ComplexityVariable(value);
            return true;
        }
        catch (ArgumentException)
        {
            variable = null!;
            return false;
        }
    }

    private static bool TryCreate(
        Func<ComplexityExpression> factory,
        out ComplexityExpression expression)
    {
        try
        {
            expression = factory();
            return true;
        }
        catch (ArgumentException)
        {
            expression = ComplexityFactory.Unknown();
            return false;
        }
    }
}

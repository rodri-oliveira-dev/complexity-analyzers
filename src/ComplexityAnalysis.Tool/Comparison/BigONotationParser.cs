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

        return TryParseFactorial(body, out expression)
            || TryParseExponential(body, out expression)
            || TryParseLogarithm(body, out expression)
            || TryParsePolynomialOrLog(body, out expression);
    }

    private static bool TryParseFactorial(string body, out ComplexityExpression expression)
    {
        expression = ComplexityFactory.Unknown();
        return body.EndsWith('!')
            && TryVariable(body[..^1], out ComplexityVariable? variable)
            && TryCreate(() => ComplexityFactory.Factorial(variable), out expression);
    }

    private static bool TryParseExponential(string body, out ComplexityExpression expression)
    {
        expression = ComplexityFactory.Unknown();
        int separator = body.IndexOf('^');
        return separator > 0
            && double.TryParse(body[..separator], NumberStyles.Float, CultureInfo.InvariantCulture, out double baseValue)
            && double.IsFinite(baseValue)
            && TryVariable(body[(separator + 1)..], out ComplexityVariable? variable)
            && TryCreate(() => ComplexityFactory.Exponential(variable, baseValue), out expression);
    }

    private static bool TryParseLogarithm(string body, out ComplexityExpression expression)
    {
        expression = ComplexityFactory.Unknown();
        return body.StartsWith("log ", StringComparison.Ordinal)
            && TryVariable(body[4..], out ComplexityVariable? variable)
            && TryCreate(() => ComplexityFactory.LogN(variable), out expression);
    }

    private static bool TryParsePolynomialOrLog(string body, out ComplexityExpression expression)
    {
        expression = ComplexityFactory.Unknown();
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

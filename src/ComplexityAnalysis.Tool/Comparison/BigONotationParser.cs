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
                && Set(ComplexityFactory.Factorial(variable), out expression);
        }

        int exponentialSeparator = body.IndexOf('^');
        if (exponentialSeparator > 0
            && double.TryParse(body[..exponentialSeparator], NumberStyles.Float, CultureInfo.InvariantCulture, out double baseValue)
            && TryVariable(body[(exponentialSeparator + 1)..], out ComplexityVariable? exponentialVariable))
        {
            expression = ComplexityFactory.Exponential(exponentialVariable, baseValue);
            return true;
        }

        if (body.StartsWith("log ", StringComparison.Ordinal))
        {
            return TryVariable(body[4..], out ComplexityVariable? variable)
                && Set(ComplexityFactory.LogN(variable), out expression);
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

        expression = logSeparator < 0
            ? ComplexityFactory.Polynomial(polynomialVariable, degree)
            : degree == 1
                ? ComplexityFactory.NLogN(polynomialVariable)
                : new PolynomialLogComplexity(polynomialVariable, degree, logExponent: 1);
        return true;
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

    private static bool Set(ComplexityExpression value, out ComplexityExpression expression)
    {
        expression = value;
        return true;
    }
}

namespace ComplexityAnalysis.Analyzers.Model;

internal static class ComplexityFactory
{
    internal static ComplexityExpression Constant()
    {
        return ConstantComplexity.Instance;
    }

    internal static ComplexityExpression LogN(ComplexityVariable variable)
    {
        return new PolynomialLogComplexity(variable, polynomialDegree: 0, logExponent: 1);
    }

    internal static ComplexityExpression Linear(ComplexityVariable variable)
    {
        return new PolynomialLogComplexity(variable, polynomialDegree: 1, logExponent: 0);
    }

    internal static ComplexityExpression NLogN(ComplexityVariable variable)
    {
        return new PolynomialLogComplexity(variable, polynomialDegree: 1, logExponent: 1);
    }

    internal static ComplexityExpression Polynomial(ComplexityVariable variable, int degree)
    {
        return Polynomial(variable, (double)degree);
    }

    internal static ComplexityExpression Polynomial(ComplexityVariable variable, double degree)
    {
        double normalizedDegree = PolynomialExponentNormalizer.Normalize(degree);
        return normalizedDegree == 0
            ? Constant()
            : new PolynomialLogComplexity(variable, normalizedDegree, logExponent: 0);
    }

    internal static ComplexityExpression Exponential(ComplexityVariable variable, double @base)
    {
        return new ExponentialComplexity(variable, @base);
    }

    internal static ComplexityExpression Factorial(ComplexityVariable variable)
    {
        return new FactorialComplexity(variable);
    }

    internal static ComplexityExpression Unknown()
    {
        return UnknownComplexity.Instance;
    }
}

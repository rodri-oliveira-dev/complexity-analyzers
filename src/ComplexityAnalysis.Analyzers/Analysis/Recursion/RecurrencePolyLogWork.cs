using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrencePolyLogWork
{
    internal RecurrencePolyLogWork(double polynomialDegree, int logExponent)
    {
        PolynomialDegree = polynomialDegree;
        LogExponent = logExponent;
    }

    internal double PolynomialDegree
    {
        get;
    }

    internal int LogExponent
    {
        get;
    }

    internal static bool TryClassify(
        ComplexityExpression localWork,
        ComplexityVariable variable,
        out RecurrencePolyLogWork? polyLogWork)
    {
        polyLogWork = null;

        switch (localWork)
        {
            case ConstantComplexity:
                polyLogWork = new RecurrencePolyLogWork(polynomialDegree: 0, logExponent: 0);
                return true;

            case PolynomialLogComplexity polynomialLog when polynomialLog.Variable.Equals(variable):
                polyLogWork = new RecurrencePolyLogWork(
                    polynomialLog.PolynomialDegree,
                    polynomialLog.LogExponent);
                return true;

            default:
                return false;
        }
    }

    internal static ComplexityExpression CreateSolution(
        ComplexityVariable variable,
        double criticalExponent,
        RecurrencePolyLogWork localWork,
        TollComparisonCase theoremCase)
    {
        return theoremCase switch
        {
            TollComparisonCase.TollSmaller => PolyLog(variable, criticalExponent, logExponent: 0),
            TollComparisonCase.TollMatches => PolyLog(variable, criticalExponent, localWork.LogExponent + 1),
            TollComparisonCase.TollLarger => PolyLog(variable, localWork.PolynomialDegree, localWork.LogExponent),
            _ => ComplexityFactory.Unknown(),
        };
    }

    private static ComplexityExpression PolyLog(
        ComplexityVariable variable,
        double polynomialDegree,
        int logExponent)
    {
        double normalizedPolynomialDegree = PolynomialExponentNormalizer.Normalize(polynomialDegree);
        return normalizedPolynomialDegree <= PolynomialExponentNormalizer.IntegerTolerance
            && logExponent == 0
            ? ComplexityFactory.Constant()
            : new PolynomialLogComplexity(variable, normalizedPolynomialDegree, logExponent);
    }
}

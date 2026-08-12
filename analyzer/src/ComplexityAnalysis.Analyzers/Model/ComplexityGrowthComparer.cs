using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal static class ComplexityGrowthComparer
{
    internal static GrowthComparison Compare(ComplexityExpression left, ComplexityExpression right)
    {
        left = left ?? throw new ArgumentNullException(nameof(left));
        right = right ?? throw new ArgumentNullException(nameof(right));

        return left.Equals(right)
            ? GrowthComparison.Equivalent
            : CompareDifferentExpressions(left, right);
    }

    private static GrowthComparison CompareDifferentExpressions(ComplexityExpression left, ComplexityExpression right)
    {
        return (left, right) switch
        {
            (UnknownComplexity, _) => GrowthComparison.Incomparable,
            (_, UnknownComplexity) => GrowthComparison.Incomparable,
            (ConstantComplexity, _) => GrowthComparison.Less,
            (_, ConstantComplexity) => GrowthComparison.Greater,
            (PolynomialLogComplexity leftPolynomialLog, _) => ComparePolynomialLogWith(leftPolynomialLog, right),
            (ExponentialComplexity leftExponential, _) => CompareExponentialWith(leftExponential, right),
            (FactorialComplexity leftFactorial, _) => CompareFactorialWith(leftFactorial, right),
            _ => GrowthComparison.Incomparable,
        };
    }

    private static GrowthComparison ComparePolynomialLogWith(PolynomialLogComplexity left, ComplexityExpression right)
    {
        switch (right)
        {
            case PolynomialLogComplexity rightPolynomialLog:
                if (!left.Variable.Equals(rightPolynomialLog.Variable))
                {
                    return GrowthComparison.Incomparable;
                }

                int polynomialDegreeComparison = left.PolynomialDegree.CompareTo(rightPolynomialLog.PolynomialDegree);
                if (polynomialDegreeComparison != 0)
                {
                    return ToGrowthComparison(polynomialDegreeComparison);
                }

                return ToGrowthComparison(left.LogExponent.CompareTo(rightPolynomialLog.LogExponent));

            case ExponentialComplexity rightExponential:
                return CompareVariables(left.Variable, rightExponential.Variable, GrowthComparison.Less);

            case FactorialComplexity rightFactorial:
                return CompareVariables(left.Variable, rightFactorial.Variable, GrowthComparison.Less);

            default:
                return GrowthComparison.Incomparable;
        }
    }

    private static GrowthComparison CompareExponentialWith(ExponentialComplexity left, ComplexityExpression right)
    {
        switch (right)
        {
            case PolynomialLogComplexity rightPolynomialLog:
                return CompareVariables(left.Variable, rightPolynomialLog.Variable, GrowthComparison.Greater);

            case ExponentialComplexity rightExponential:
                if (!left.Variable.Equals(rightExponential.Variable))
                {
                    return GrowthComparison.Incomparable;
                }

                return ToGrowthComparison(left.Base.CompareTo(rightExponential.Base));

            case FactorialComplexity rightFactorial:
                return CompareVariables(left.Variable, rightFactorial.Variable, GrowthComparison.Less);

            default:
                return GrowthComparison.Incomparable;
        }
    }

    private static GrowthComparison CompareFactorialWith(FactorialComplexity left, ComplexityExpression right)
    {
        return right switch
        {
            PolynomialLogComplexity rightPolynomialLog => CompareVariables(left.Variable, rightPolynomialLog.Variable, GrowthComparison.Greater),
            ExponentialComplexity rightExponential => CompareVariables(left.Variable, rightExponential.Variable, GrowthComparison.Greater),
            FactorialComplexity rightFactorial => left.Variable.Equals(rightFactorial.Variable)
                ? GrowthComparison.Equivalent
                : GrowthComparison.Incomparable,
            _ => GrowthComparison.Incomparable,
        };
    }

    private static GrowthComparison CompareVariables(
        ComplexityVariable left,
        ComplexityVariable right,
        GrowthComparison sameVariableComparison)
    {
        return left.Equals(right)
            ? sameVariableComparison
            : GrowthComparison.Incomparable;
    }

    private static GrowthComparison ToGrowthComparison(int comparison)
    {
        return comparison < 0
            ? GrowthComparison.Less
            : comparison > 0
            ? GrowthComparison.Greater
            : GrowthComparison.Equivalent;
    }
}

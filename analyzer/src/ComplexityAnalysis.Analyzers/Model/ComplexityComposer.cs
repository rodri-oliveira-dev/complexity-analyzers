using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal static class ComplexityComposer
{
    internal static ComplexityExpression Sequential(ComplexityExpression left, ComplexityExpression right)
    {
        ValidateOperands(left, right);

        return ContainsUnknown(left, right)
            ? ComplexityFactory.Unknown()
            : SelectDominant(left, right)
                ?? new CompositeComplexity(left, ComplexityOperation.Sequential, right);
    }

    internal static ComplexityExpression Nested(ComplexityExpression left, ComplexityExpression right)
    {
        ValidateOperands(left, right);

        return ContainsUnknown(left, right)
            ? ComplexityFactory.Unknown()
            : TryEliminateConstantFactor(left, right)
                ?? TryMultiplySameVariablePolynomialLog(left, right)
                ?? new CompositeComplexity(left, ComplexityOperation.Nested, right);
    }

    internal static ComplexityExpression Branching(ComplexityExpression left, ComplexityExpression right)
    {
        ValidateOperands(left, right);

        return ContainsUnknown(left, right)
            ? ComplexityFactory.Unknown()
            : SelectDominant(left, right)
                ?? new CompositeComplexity(left, ComplexityOperation.Maximum, right);
    }

    private static ComplexityExpression? SelectDominant(ComplexityExpression left, ComplexityExpression right)
    {
        return ComplexityGrowthComparer.Compare(left, right) switch
        {
            GrowthComparison.Less => right,
            GrowthComparison.Equivalent => left,
            GrowthComparison.Greater => left,
            GrowthComparison.Incomparable => null,
            _ => throw new InvalidOperationException("Unknown growth comparison."),
        };
    }

    private static ComplexityExpression PolynomialLog(ComplexityVariable variable, int polynomialDegree, int logExponent)
    {
        return polynomialDegree == 0 && logExponent == 0
            ? ComplexityFactory.Constant()
            : new PolynomialLogComplexity(variable, polynomialDegree, logExponent);
    }

    private static ComplexityExpression? TryEliminateConstantFactor(ComplexityExpression left, ComplexityExpression right)
    {
        return left is ConstantComplexity
            ? right
            : right is ConstantComplexity
            ? left
            : null;
    }

    private static ComplexityExpression? TryMultiplySameVariablePolynomialLog(ComplexityExpression left, ComplexityExpression right)
    {
        return left is PolynomialLogComplexity leftPolynomialLog
            && right is PolynomialLogComplexity rightPolynomialLog
            && leftPolynomialLog.Variable.Equals(rightPolynomialLog.Variable)
            ? PolynomialLog(
                leftPolynomialLog.Variable,
                leftPolynomialLog.PolynomialDegree + rightPolynomialLog.PolynomialDegree,
                leftPolynomialLog.LogExponent + rightPolynomialLog.LogExponent)
            : null;
    }

    private static bool ContainsUnknown(ComplexityExpression left, ComplexityExpression right)
    {
        return left is UnknownComplexity || right is UnknownComplexity;
    }

    private static void ValidateOperands(ComplexityExpression left, ComplexityExpression right)
    {
        _ = left ?? throw new ArgumentNullException(nameof(left));
        _ = right ?? throw new ArgumentNullException(nameof(right));
    }
}

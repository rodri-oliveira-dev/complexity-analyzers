using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal static class TollComparisonClassifier
{
    internal static TollComparisonCase? Classify(double polynomialDegreeDifference)
    {
        return polynomialDegreeDifference < -RecurrenceNumerics.PolynomialGapThreshold
            ? TollComparisonCase.TollSmaller
            : polynomialDegreeDifference > RecurrenceNumerics.PolynomialGapThreshold
            ? TollComparisonCase.TollLarger
            : Math.Abs(polynomialDegreeDifference) <= RecurrenceNumerics.ComparisonEpsilon
            ? TollComparisonCase.TollMatches
            : null;
    }
}

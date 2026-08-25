using System;
using System.Globalization;

namespace ComplexityAnalysis.Analyzers.Model;

internal static class PolynomialExponentNormalizer
{
    internal const double IntegerTolerance = 0.000000001;
    internal const int FractionalPrecision = 3;

    internal static double Normalize(double exponent)
    {
        if (double.IsNaN(exponent) || double.IsInfinity(exponent) || exponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "Polynomial exponent must be finite and non-negative.");
        }

        double nearestInteger = Math.Round(exponent, 0, MidpointRounding.AwayFromZero);
        if (Math.Abs(exponent - nearestInteger) <= IntegerTolerance)
        {
            return nearestInteger;
        }

        return Math.Round(exponent, FractionalPrecision, MidpointRounding.AwayFromZero);
    }

    internal static string Format(double exponent)
    {
        double normalized = Normalize(exponent);
        return normalized.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

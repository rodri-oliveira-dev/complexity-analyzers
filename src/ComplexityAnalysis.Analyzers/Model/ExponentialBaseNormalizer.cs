using System;
using System.Globalization;

namespace ComplexityAnalysis.Analyzers.Model;

internal static class ExponentialBaseNormalizer
{
    internal const double IntegerTolerance = 0.000000001;
    internal const int FractionalPrecision = 3;

    internal static double Normalize(double @base)
    {
        if (double.IsNaN(@base) || double.IsInfinity(@base) || @base <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(@base), "Exponential base must be finite and greater than one.");
        }

        double nearestInteger = Math.Round(@base, 0, MidpointRounding.AwayFromZero);
        if (Math.Abs(@base - nearestInteger) <= IntegerTolerance)
        {
            return nearestInteger;
        }

        double rounded = Math.Round(@base, FractionalPrecision, MidpointRounding.AwayFromZero);
        return rounded > 1 ? rounded : @base;
    }

    internal static string Format(double @base)
    {
        double normalized = Normalize(@base);
        return normalized.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

using System;
using System.Globalization;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal static class HalsteadMetricFormatter
{
    internal const string DoubleFormat = "G17";

    internal static string Format(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value)
            ? throw new ArgumentOutOfRangeException(nameof(value), "Halstead metric values must be finite.")
            : value.ToString(DoubleFormat, CultureInfo.InvariantCulture);
    }
}

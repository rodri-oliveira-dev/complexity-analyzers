using System;
using System.Globalization;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct HalsteadMetrics : IEquatable<HalsteadMetrics>
{
    private const double SecondsPerMentalDiscrimination = 18.0;
    private const double DeliveredBugsVolumeDivisor = 3000.0;
    private const double InverseNaturalLogOfTwo = 1.4426950408889634;

    internal HalsteadMetrics(HalsteadPrimitiveCounts primitiveCounts)
    {
        PrimitiveCounts = primitiveCounts;
        Vocabulary = primitiveCounts.Vocabulary;
        Length = primitiveCounts.Length;
        CalculatedLength =
            VocabularyContribution(primitiveCounts.DistinctOperatorCount)
            + VocabularyContribution(primitiveCounts.DistinctOperandCount);
        Volume = Vocabulary <= 1 || Length == 0
            ? 0.0
            : Length * Log2(Vocabulary);
        Difficulty = primitiveCounts.DistinctOperatorCount == 0 || primitiveCounts.DistinctOperandCount == 0
            ? 0.0
            : primitiveCounts.DistinctOperatorCount / 2.0
                * primitiveCounts.TotalOperandCount / primitiveCounts.DistinctOperandCount;
        Effort = Difficulty * Volume;
        EstimatedImplementationTime = Effort / SecondsPerMentalDiscrimination;
        EstimatedDeliveredBugs = Volume / DeliveredBugsVolumeDivisor;
    }

    internal static HalsteadMetrics FromPrimitiveCounts(
        int distinctOperatorCount,
        int distinctOperandCount,
        int totalOperatorCount,
        int totalOperandCount)
    {
        return new HalsteadMetrics(
            new HalsteadPrimitiveCounts(
                distinctOperatorCount,
                distinctOperandCount,
                totalOperatorCount,
                totalOperandCount));
    }

    internal HalsteadPrimitiveCounts PrimitiveCounts
    {
        get;
    }

    internal long Vocabulary
    {
        get;
    }

    internal long Length
    {
        get;
    }

    internal double CalculatedLength
    {
        get;
    }

    internal double Volume
    {
        get;
    }

    internal double Difficulty
    {
        get;
    }

    internal double Effort
    {
        get;
    }

    internal double EstimatedImplementationTime
    {
        get;
    }

    internal double EstimatedDeliveredBugs
    {
        get;
    }

    public bool Equals(HalsteadMetrics other)
    {
        return PrimitiveCounts.Equals(other.PrimitiveCounts);
    }

    public override bool Equals(object? obj)
    {
        return obj is HalsteadMetrics other && Equals(other);
    }

    public override int GetHashCode()
    {
        return PrimitiveCounts.GetHashCode();
    }

    public override string ToString()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}; vocabulary={1}; length={2}; calculatedLength={3}; volume={4}; difficulty={5}; effort={6}; estimatedImplementationTime={7}; estimatedDeliveredBugs={8}",
            PrimitiveCounts,
            Vocabulary,
            Length,
            HalsteadMetricFormatter.Format(CalculatedLength),
            HalsteadMetricFormatter.Format(Volume),
            HalsteadMetricFormatter.Format(Difficulty),
            HalsteadMetricFormatter.Format(Effort),
            HalsteadMetricFormatter.Format(EstimatedImplementationTime),
            HalsteadMetricFormatter.Format(EstimatedDeliveredBugs));
    }

    private static double VocabularyContribution(int count)
    {
        return count <= 1
            ? 0.0
            : count * Log2(count);
    }

    private static double Log2(long value)
    {
        return Math.Log(value) * InverseNaturalLogOfTwo;
    }

    private static double Log2(int value)
    {
        return Math.Log(value) * InverseNaturalLogOfTwo;
    }
}

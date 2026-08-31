using System;
using System.Globalization;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct HalsteadPrimitiveCounts : IEquatable<HalsteadPrimitiveCounts>
{
    internal HalsteadPrimitiveCounts(
        int distinctOperatorCount,
        int distinctOperandCount,
        int totalOperatorCount,
        int totalOperandCount)
    {
        ValidateCount(distinctOperatorCount, nameof(distinctOperatorCount));
        ValidateCount(distinctOperandCount, nameof(distinctOperandCount));
        ValidateCount(totalOperatorCount, nameof(totalOperatorCount));
        ValidateCount(totalOperandCount, nameof(totalOperandCount));
        ValidateDistinctCount(distinctOperatorCount, totalOperatorCount, nameof(distinctOperatorCount));
        ValidateDistinctCount(distinctOperandCount, totalOperandCount, nameof(distinctOperandCount));

        DistinctOperatorCount = distinctOperatorCount;
        DistinctOperandCount = distinctOperandCount;
        TotalOperatorCount = totalOperatorCount;
        TotalOperandCount = totalOperandCount;
    }

    internal int DistinctOperatorCount
    {
        get;
    }

    internal int DistinctOperandCount
    {
        get;
    }

    internal int TotalOperatorCount
    {
        get;
    }

    internal int TotalOperandCount
    {
        get;
    }

    internal long Vocabulary
        => (long)DistinctOperatorCount + DistinctOperandCount;

    internal long Length
        => (long)TotalOperatorCount + TotalOperandCount;

    public bool Equals(HalsteadPrimitiveCounts other)
    {
        return DistinctOperatorCount == other.DistinctOperatorCount
            && DistinctOperandCount == other.DistinctOperandCount
            && TotalOperatorCount == other.TotalOperatorCount
            && TotalOperandCount == other.TotalOperandCount;
    }

    public override bool Equals(object? obj)
    {
        return obj is HalsteadPrimitiveCounts other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = DistinctOperatorCount;
            hash = (hash * 397) ^ DistinctOperandCount;
            hash = (hash * 397) ^ TotalOperatorCount;
            hash = (hash * 397) ^ TotalOperandCount;
            return hash;
        }
    }

    public override string ToString()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "n1={0}; n2={1}; N1={2}; N2={3}",
            DistinctOperatorCount,
            DistinctOperandCount,
            TotalOperatorCount,
            TotalOperandCount);
    }

    private static void ValidateCount(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Halstead primitive counts must be non-negative.");
        }
    }

    private static void ValidateDistinctCount(
        int distinctCount,
        int totalCount,
        string parameterName)
    {
        if (distinctCount > totalCount)
        {
            throw new ArgumentOutOfRangeException(parameterName, distinctCount, "A distinct Halstead count cannot exceed its total count.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class HalsteadClassificationResult
{
    private HalsteadClassificationResult()
    {
        Elements = ImmutableArray<HalsteadElement>.Empty;
        PrimitiveCounts = default;
        Metrics = new HalsteadMetrics(PrimitiveCounts);
    }

    internal HalsteadClassificationResult(IEnumerable<HalsteadElement> elements)
    {
        _ = elements ?? throw new ArgumentNullException(nameof(elements));

        ImmutableArray<HalsteadElement> immutableElements = ImmutableArray.CreateRange(elements);
        HashSet<HalsteadElementIdentity> operatorIdentities = [];
        HashSet<HalsteadElementIdentity> operandIdentities = [];
        int totalOperators = 0;
        int totalOperands = 0;

        foreach (HalsteadElement element in immutableElements)
        {
            if (element.Role == HalsteadElementRole.Operator)
            {
                totalOperators++;
                _ = operatorIdentities.Add(element.Identity);
            }
            else
            {
                totalOperands++;
                _ = operandIdentities.Add(element.Identity);
            }
        }

        Elements = immutableElements;
        PrimitiveCounts = new HalsteadPrimitiveCounts(
            operatorIdentities.Count,
            operandIdentities.Count,
            totalOperators,
            totalOperands);
        Metrics = new HalsteadMetrics(PrimitiveCounts);
    }

    internal static HalsteadClassificationResult Empty
        => new();

    internal ImmutableArray<HalsteadElement> Elements
    {
        get;
    }

    internal int DistinctOperatorCount
        => PrimitiveCounts.DistinctOperatorCount;

    internal int DistinctOperandCount
        => PrimitiveCounts.DistinctOperandCount;

    internal int TotalOperatorCount
        => PrimitiveCounts.TotalOperatorCount;

    internal int TotalOperandCount
        => PrimitiveCounts.TotalOperandCount;

    internal HalsteadPrimitiveCounts PrimitiveCounts
    {
        get;
    }

    internal HalsteadMetrics Metrics
    {
        get;
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class HalsteadClassificationResult
{
    private HalsteadClassificationResult()
    {
        Elements = ImmutableArray<HalsteadElement>.Empty;
        DistinctOperatorCount = 0;
        DistinctOperandCount = 0;
        TotalOperatorCount = 0;
        TotalOperandCount = 0;
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
        DistinctOperatorCount = operatorIdentities.Count;
        DistinctOperandCount = operandIdentities.Count;
        TotalOperatorCount = totalOperators;
        TotalOperandCount = totalOperands;
    }

    internal static HalsteadClassificationResult Empty
        => new();

    internal ImmutableArray<HalsteadElement> Elements
    {
        get;
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
}

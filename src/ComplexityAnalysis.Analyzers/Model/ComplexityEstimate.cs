using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class ComplexityEstimate : IEquatable<ComplexityEstimate>
{
    private ComplexityEstimate(
        ComplexityExpression complexity,
        ComplexityGuarantee guarantee,
        IEnumerable<ComplexityAssumption> assumptions)
    {
        Complexity = complexity ?? throw new ArgumentNullException(nameof(complexity));
        if (!Enum.IsDefined(typeof(ComplexityGuarantee), guarantee))
        {
            throw new ArgumentOutOfRangeException(nameof(guarantee));
        }

        Assumptions = (assumptions ?? throw new ArgumentNullException(nameof(assumptions)))
            .Select(assumption => assumption ?? throw new ArgumentException(
                "Assumptions must not contain null values.",
                nameof(assumptions)))
            .ToImmutableArray();
        Guarantee = guarantee;

        ValidateContract();
    }

    internal ComplexityExpression Complexity
    {
        get;
    }

    internal ComplexityGuarantee Guarantee
    {
        get;
    }

    internal ImmutableArray<ComplexityAssumption> Assumptions
    {
        get;
    }

    internal static ComplexityEstimate WorstCaseUpperBound(
        ComplexityExpression complexity,
        params ComplexityAssumption[] assumptions)
    {
        return new ComplexityEstimate(
            complexity,
            ComplexityGuarantee.WorstCaseUpperBound,
            assumptions);
    }

    internal static ComplexityEstimate Expected(
        ComplexityExpression complexity,
        params ComplexityAssumption[] assumptions)
    {
        return new ComplexityEstimate(complexity, ComplexityGuarantee.Expected, assumptions);
    }

    internal static ComplexityEstimate Amortized(
        ComplexityExpression complexity,
        params ComplexityAssumption[] assumptions)
    {
        return new ComplexityEstimate(complexity, ComplexityGuarantee.Amortized, assumptions);
    }

    internal static ComplexityEstimate Unknown()
    {
        return new ComplexityEstimate(
            ComplexityFactory.Unknown(),
            ComplexityGuarantee.Unknown,
            []);
    }

    internal ComplexityEstimate WithComplexity(ComplexityExpression complexity)
    {
        _ = complexity ?? throw new ArgumentNullException(nameof(complexity));

        return complexity is UnknownComplexity
            ? Unknown()
            : Guarantee == ComplexityGuarantee.Unknown
            ? WorstCaseUpperBound(complexity)
            : new ComplexityEstimate(complexity, Guarantee, Assumptions);
    }

    public bool Equals(ComplexityEstimate? other)
    {
        return other is not null
            && Complexity.Equals(other.Complexity)
            && Guarantee == other.Guarantee
            && Assumptions.SequenceEqual(other.Assumptions);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComplexityEstimate);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Complexity.GetHashCode();
            hash = (hash * 397) ^ Guarantee.GetHashCode();
            foreach (ComplexityAssumption assumption in Assumptions)
            {
                hash = (hash * 397) ^ assumption.GetHashCode();
            }

            return hash;
        }
    }

    private void ValidateContract()
    {
        if (Guarantee == ComplexityGuarantee.Unknown)
        {
            if (Complexity is not UnknownComplexity || Assumptions.Length != 0)
            {
                throw new ArgumentException(
                    "Unknown estimates must use Unknown complexity and cannot declare assumptions.");
            }

            return;
        }

        if (Complexity is UnknownComplexity)
        {
            throw new ArgumentException(
                "Known guarantees require a known complexity expression.",
                nameof(Complexity));
        }

        if (Guarantee == ComplexityGuarantee.Expected
            && !Assumptions.Any(IsDistributionAssumption))
        {
            throw new ArgumentException(
                "Expected estimates require an explicit distribution assumption.",
                nameof(Assumptions));
        }

        if (Guarantee == ComplexityGuarantee.Amortized
            && !Assumptions.Any(assumption => assumption.Kind == ComplexityAssumptionKind.OperationSequence))
        {
            throw new ArgumentException(
                "Amortized estimates require an explicit operation-sequence assumption.",
                nameof(Assumptions));
        }
    }

    private static bool IsDistributionAssumption(ComplexityAssumption assumption)
    {
        return assumption.Kind is ComplexityAssumptionKind.InputDistribution
            or ComplexityAssumptionKind.HashDistribution;
    }
}

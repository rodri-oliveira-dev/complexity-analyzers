using System;

using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityEstimateTests
{
    [Fact]
    public void Worst_case_upper_bound_preserves_known_complexity_without_assumptions()
    {
        ComplexityEstimate estimate = ComplexityEstimate.WorstCaseUpperBound(
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal(ComplexityGuarantee.WorstCaseUpperBound, estimate.Guarantee);
        Assert.Equal("O(n)", estimate.Complexity.ToBigONotation());
        Assert.Empty(estimate.Assumptions);
    }

    [Fact]
    public void Expected_cost_requires_and_preserves_distribution_assumption()
    {
        ComplexityAssumption distribution = new(
            ComplexityAssumptionKind.HashDistribution,
            "Hash codes are sufficiently distributed across buckets.");
        ComplexityAssumption operationCost = new(
            ComplexityAssumptionKind.ElementOperationCost,
            "Hashing and equality checks have constant cost.");

        ComplexityEstimate estimate = ComplexityEstimate.Expected(
            ComplexityFactory.Constant(),
            distribution,
            operationCost);

        Assert.Equal(ComplexityGuarantee.Expected, estimate.Guarantee);
        Assert.Equal("O(1)", estimate.Complexity.ToBigONotation());
        Assert.Equal(2, estimate.Assumptions.Length);
        Assert.Equal(distribution, estimate.Assumptions[0]);
        Assert.Equal(operationCost, estimate.Assumptions[1]);
    }

    [Fact]
    public void Expected_cost_rejects_missing_distribution_assumption()
    {
        ComplexityAssumption operationCost = new(
            ComplexityAssumptionKind.ElementOperationCost,
            "Equality checks have constant cost.");

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            ComplexityEstimate.Expected(ComplexityFactory.Constant(), operationCost));

        Assert.Contains("distribution assumption", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Amortized_cost_requires_operation_sequence_assumption()
    {
        ComplexityAssumption sequence = new(
            ComplexityAssumptionKind.OperationSequence,
            "The bound applies across a sequence of append operations.");

        ComplexityEstimate estimate = ComplexityEstimate.Amortized(
            ComplexityFactory.Constant(),
            sequence);

        Assert.Equal(ComplexityGuarantee.Amortized, estimate.Guarantee);
        _ = Assert.Single(estimate.Assumptions);
        Assert.Equal(sequence, estimate.Assumptions[0]);
        _ = Assert.Throws<ArgumentException>(() =>
            ComplexityEstimate.Amortized(ComplexityFactory.Constant()));
    }

    [Fact]
    public void Unknown_estimate_is_explicit_and_cannot_be_used_as_known_guarantee()
    {
        ComplexityEstimate estimate = ComplexityEstimate.Unknown();

        Assert.Equal(ComplexityGuarantee.Unknown, estimate.Guarantee);
        Assert.Equal("Unknown", estimate.Complexity.ToBigONotation());
        Assert.Empty(estimate.Assumptions);
        _ = Assert.Throws<ArgumentException>(() =>
            ComplexityEstimate.WorstCaseUpperBound(ComplexityFactory.Unknown()));
    }

    [Fact]
    public void Assumption_rejects_invalid_values()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ComplexityAssumption((ComplexityAssumptionKind)int.MaxValue, "Invalid kind."));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new ComplexityAssumption(ComplexityAssumptionKind.InputDistribution, null!));
        _ = Assert.Throws<ArgumentException>(() =>
            new ComplexityAssumption(ComplexityAssumptionKind.InputDistribution, string.Empty));
    }

    [Fact]
    public void Estimate_equality_uses_assumption_values()
    {
        ComplexityEstimate first = ComplexityEstimate.Expected(
            ComplexityFactory.Constant(),
            new ComplexityAssumption(
                ComplexityAssumptionKind.InputDistribution,
                "Inputs are uniformly distributed."));
        ComplexityEstimate equivalent = ComplexityEstimate.Expected(
            ComplexityFactory.Constant(),
            new ComplexityAssumption(
                ComplexityAssumptionKind.InputDistribution,
                "Inputs are uniformly distributed."));

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
    }
}

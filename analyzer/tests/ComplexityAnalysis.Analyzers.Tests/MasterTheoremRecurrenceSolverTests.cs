using System;
using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class MasterTheoremRecurrenceSolverTests
{
    [Theory]
    [InlineData(1, 0.5, "constant toll binary search", "O(log n)", 0)]
    [InlineData(2, 0.5, "constant toll balanced split", "O(n)", 1)]
    [InlineData(4, 0.5, "linear toll smaller than leaves", "O(n\u00b2)", 2)]
    public void Solves_required_master_theorem_examples(
        int multiplicity,
        double scale,
        string scenario,
        string expectedBigO,
        double expectedCriticalExponent)
    {
        RecurrenceSolution solution = Solve(Relation(
            scenario == "linear toll smaller than leaves"
                ? ComplexityFactory.Linear(ComplexityVariable.N)
                : ComplexityFactory.Constant(),
            Scale(multiplicity, scale)));

        AssertSolved(solution, expectedBigO, expectedCriticalExponent);
    }

    [Theory]
    [InlineData(2, 0.5, "O(n log n)", 1)]
    [InlineData(4, 0.5, "O(n\u00b2 log n)", 2)]
    public void Solves_case_two_when_polynomial_degree_matches_critical_exponent(
        int multiplicity,
        double scale,
        string expectedBigO,
        double expectedCriticalExponent)
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Polynomial(ComplexityVariable.N, expectedCriticalExponent),
            Scale(multiplicity, scale)));

        AssertSolved(solution, expectedBigO, expectedCriticalExponent);
    }

    [Fact]
    public void Solves_case_two_by_incrementing_existing_log_exponent()
    {
        RecurrenceSolution solution = Solve(Relation(
            new PolynomialLogComplexity(ComplexityVariable.N, polynomialDegree: 1, logExponent: 2),
            Scale(multiplicity: 2, scale: 0.5)));

        AssertSolved(solution, "O(n log^3 n)", computedExponent: 1);
    }

    [Theory]
    [InlineData(2, 0.5, 2, 0, "O(n\u00b2)", 1)]
    [InlineData(2, 0.5, 2, 1, "O(n\u00b2 log n)", 1)]
    public void Solves_case_three_when_supported_polylog_toll_dominates(
        int multiplicity,
        double scale,
        double polynomialDegree,
        int logExponent,
        string expectedBigO,
        double expectedCriticalExponent)
    {
        RecurrenceSolution solution = Solve(Relation(
            new PolynomialLogComplexity(ComplexityVariable.N, polynomialDegree, logExponent),
            Scale(multiplicity, scale)));

        AssertSolved(solution, expectedBigO, expectedCriticalExponent);
    }

    [Fact]
    public void Solves_fractional_critical_exponent_with_deterministic_formatting()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Linear(ComplexityVariable.N),
            Scale(multiplicity: 3, scale: 0.5)));

        AssertSolved(solution, "O(n^1.585)", computedExponent: 1.585);
    }

    [Fact]
    public void Normalizes_near_integer_critical_exponent()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Constant(),
            Scale(multiplicity: 4, scale: 0.5)));

        AssertSolved(solution, "O(n\u00b2)", computedExponent: 2);
    }

    [Fact]
    public void Unsupported_shapes_return_structured_unsupported_result()
    {
        RecurrenceRelation[] unsupported =
        [
            Relation(ComplexityFactory.Constant(), Scale(1, 0.5), Scale(1, 0.5)),
            Relation(ComplexityFactory.Constant(), new RecurrenceTerm(1, RecurrenceReduction.SubtractConstant(1))),
            Relation(ComplexityFactory.Unknown(), Scale(1, 0.5)),
            Relation(ComplexityFactory.Exponential(ComplexityVariable.N, 2), Scale(2, 0.5)),
            Relation(ComplexityFactory.Factorial(ComplexityVariable.N), Scale(2, 0.5)),
            Relation(ComplexityFactory.Linear(ComplexityVariable.M), Scale(2, 0.5)),
        ];

        var solver = new MasterTheoremRecurrenceSolver();
        foreach (RecurrenceRelation relation in unsupported)
        {
            Assert.False(solver.IsApplicable(relation));

            RecurrenceSolution solution = solver.Solve(relation);

            Assert.Equal(RecurrenceSolutionKind.Unsupported, solution.Kind);
            Assert.Null(solution.Complexity);
            Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1)]
    [InlineData(1.5)]
    public void Invalid_scale_reductions_are_rejected_by_the_recurrence_model(double scale)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => RecurrenceReduction.Scale(scale));
    }

    [Fact]
    public void Boundary_gap_without_polynomial_separation_is_numerically_inconclusive()
    {
        var solver = new MasterTheoremRecurrenceSolver();
        RecurrenceRelation relation = Relation(
            ComplexityFactory.Polynomial(
                ComplexityVariable.N,
                1 + (RecurrenceNumerics.PolynomialGapThreshold / 2)),
            Scale(multiplicity: 2, scale: 0.5));

        Assert.False(solver.IsApplicable(relation));

        RecurrenceSolution solution = solver.Solve(relation);

        Assert.Equal(RecurrenceSolutionKind.NumericallyInconclusive, solution.Kind);
        Assert.Null(solution.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
    }

    [Fact]
    public void Boundary_values_within_comparison_epsilon_use_case_two()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Polynomial(ComplexityVariable.N, 1 + (RecurrenceNumerics.ComparisonEpsilon / 2)),
            Scale(multiplicity: 2, scale: 0.5)));

        AssertSolved(solution, "O(n log n)", computedExponent: 1);
    }

    [Fact]
    public void Null_relation_returns_structured_invalid_result()
    {
        var solver = new MasterTheoremRecurrenceSolver();

        RecurrenceSolution solution = solver.Solve(null);

        Assert.False(solver.IsApplicable(null));
        Assert.Equal(RecurrenceSolutionKind.Invalid, solution.Kind);
        Assert.Null(solution.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
    }

    private static RecurrenceSolution Solve(RecurrenceRelation relation)
    {
        var solver = new MasterTheoremRecurrenceSolver();

        Assert.True(solver.IsApplicable(relation));
        return solver.Solve(relation);
    }

    private static RecurrenceRelation Relation(
        ComplexityExpression nonRecursiveWork,
        params RecurrenceTerm[] terms)
    {
        return new RecurrenceRelation(
            ComplexityVariable.N,
            ImmutableArray.Create(terms),
            nonRecursiveWork);
    }

    private static RecurrenceTerm Scale(int multiplicity, double scale)
    {
        return new RecurrenceTerm(multiplicity, RecurrenceReduction.Scale(scale));
    }

    private static void AssertSolved(
        RecurrenceSolution solution,
        string expectedBigO,
        double computedExponent)
    {
        Assert.Equal(RecurrenceSolutionKind.Solved, solution.Kind);
        Assert.Equal(RecurrenceSolverKind.MasterTheorem, solution.SolverKind);
        Assert.NotNull(solution.Complexity);
        Assert.Equal(expectedBigO, solution.Complexity.ToBigONotation());
        Assert.Equal(computedExponent, solution.ComputedExponent);
    }
}

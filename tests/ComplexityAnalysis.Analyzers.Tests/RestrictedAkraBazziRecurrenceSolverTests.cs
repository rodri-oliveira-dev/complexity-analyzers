using System;
using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class RestrictedAkraBazziRecurrenceSolverTests
{
    [Fact]
    public void Solves_unequal_split_with_linear_toll_as_n_log_n()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Linear(ComplexityVariable.N),
            Scale(1, 1.0 / 3.0),
            Scale(1, 2.0 / 3.0)));

        AssertSolved(solution, "O(n log n)", computedExponent: 1);
    }

    [Fact]
    public void Solves_unequal_split_with_constant_toll_as_linear()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Constant(),
            Scale(1, 1.0 / 3.0),
            Scale(1, 2.0 / 3.0)));

        AssertSolved(solution, "O(n)", computedExponent: 1);
    }

    [Fact]
    public void Solves_fractional_critical_exponent_deterministically()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Constant(),
            Scale(1, 0.25),
            Scale(1, 0.5)));

        AssertSolved(solution, "O(n^0.694)", computedExponent: 0.694);
    }

    [Fact]
    public void Solves_toll_larger_than_critical_exponent()
    {
        RecurrenceSolution solution = Solve(Relation(
            new PolynomialLogComplexity(ComplexityVariable.N, polynomialDegree: 2, logExponent: 1),
            Scale(1, 1.0 / 3.0),
            Scale(1, 2.0 / 3.0)));

        AssertSolved(solution, "O(n\u00b2 log n)", computedExponent: 1);
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
    public void Unsupported_shapes_return_structured_unsupported_result()
    {
        RecurrenceRelation[] unsupported =
        [
            Relation(ComplexityFactory.Constant(), Scale(1, 0.5)),
            Relation(ComplexityFactory.Constant(), Scale(2, 0.5), Scale(1, 0.5)),
            Relation(ComplexityFactory.Constant(), new RecurrenceTerm(1, RecurrenceReduction.SubtractConstant(1)), Scale(1, 0.5)),
            Relation(ComplexityFactory.Unknown(), Scale(1, 1.0 / 3.0), Scale(1, 2.0 / 3.0)),
            Relation(ComplexityFactory.Exponential(ComplexityVariable.N, 2), Scale(1, 1.0 / 3.0), Scale(1, 2.0 / 3.0)),
            Relation(ComplexityFactory.Factorial(ComplexityVariable.N), Scale(1, 1.0 / 3.0), Scale(1, 2.0 / 3.0)),
            Relation(ComplexityFactory.Linear(ComplexityVariable.M), Scale(1, 1.0 / 3.0), Scale(1, 2.0 / 3.0)),
        ];

        var solver = new RestrictedAkraBazziRecurrenceSolver();
        foreach (RecurrenceRelation relation in unsupported)
        {
            Assert.False(solver.IsApplicable(relation));

            RecurrenceSolution solution = solver.Solve(relation);

            Assert.Equal(RecurrenceSolutionKind.Unsupported, solution.Kind);
            Assert.Null(solution.Complexity);
            Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
        }
    }

    [Fact]
    public void No_bracket_within_bounded_search_returns_numerically_inconclusive()
    {
        var solver = new RestrictedAkraBazziRecurrenceSolver();
        RecurrenceRelation relation = Relation(
            ComplexityFactory.Constant(),
            Scale(1, 0.99999999),
            Scale(1, 0.99999998));

        Assert.False(solver.IsApplicable(relation));

        RecurrenceSolution solution = solver.Solve(relation);

        Assert.Equal(RecurrenceSolutionKind.NumericallyInconclusive, solution.Kind);
        Assert.Null(solution.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
    }

    [Fact]
    public void Iteration_cap_returns_numerically_inconclusive_when_not_converged()
    {
        var solver = new RestrictedAkraBazziRecurrenceSolver(
            maxBracketExpansions: RecurrenceNumerics.AkraBazziMaxBracketExpansions,
            maxBisectionIterations: 0,
            maxHighExponent: RecurrenceNumerics.AkraBazziMaxHighExponent);
        RecurrenceRelation relation = Relation(
            ComplexityFactory.Constant(),
            Scale(1, 0.25),
            Scale(1, 0.5));

        Assert.False(solver.IsApplicable(relation));

        RecurrenceSolution solution = solver.Solve(relation);

        Assert.Equal(RecurrenceSolutionKind.NumericallyInconclusive, solution.Kind);
    }

    [Fact]
    public void Repeated_solves_are_deterministic()
    {
        var solver = new RestrictedAkraBazziRecurrenceSolver();
        RecurrenceRelation relation = Relation(
            ComplexityFactory.Constant(),
            Scale(1, 0.25),
            Scale(1, 0.5));

        RecurrenceSolution first = solver.Solve(relation);
        for (int index = 0; index < 5; index++)
        {
            RecurrenceSolution next = solver.Solve(relation);

            Assert.Equal(first, next);
            Assert.Equal(first.Complexity!.ToBigONotation(), next.Complexity!.ToBigONotation());
            Assert.Equal(first.ComputedExponent, next.ComputedExponent);
        }
    }

    [Fact]
    public void Null_relation_returns_structured_invalid_result()
    {
        var solver = new RestrictedAkraBazziRecurrenceSolver();

        RecurrenceSolution solution = solver.Solve(null);

        Assert.False(solver.IsApplicable(null));
        Assert.Equal(RecurrenceSolutionKind.Invalid, solution.Kind);
        Assert.Null(solution.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
    }

    private static RecurrenceSolution Solve(RecurrenceRelation relation)
    {
        var solver = new RestrictedAkraBazziRecurrenceSolver();

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
        Assert.Equal(RecurrenceSolverKind.RestrictedAkraBazzi, solution.SolverKind);
        Assert.NotNull(solution.Complexity);
        Assert.Equal(expectedBigO, solution.Complexity.ToBigONotation());
        Assert.Equal(computedExponent, solution.ComputedExponent);
    }
}

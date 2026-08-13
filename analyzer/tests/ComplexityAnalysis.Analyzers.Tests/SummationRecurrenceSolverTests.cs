using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class SummationRecurrenceSolverTests
{
    [Fact]
    public void Solves_unit_decrement_with_constant_work_as_linear()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Constant(),
            Decrement(1)));

        AssertSolved(solution, "O(n)", computedExponent: 1);
    }

    [Fact]
    public void Solves_larger_constant_decrement_with_same_big_o_class()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Constant(),
            Decrement(2)));

        AssertSolved(solution, "O(n)", computedExponent: 1);
    }

    [Fact]
    public void Solves_unit_decrement_with_logarithmic_work_as_n_log_n()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.LogN(ComplexityVariable.N),
            Decrement(1)));

        AssertSolved(solution, "O(n log n)", computedExponent: 1);
    }

    [Fact]
    public void Solves_unit_decrement_with_linear_work_as_quadratic()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Linear(ComplexityVariable.N),
            Decrement(1)));

        AssertSolved(solution, "O(n\u00b2)", computedExponent: 2);
    }

    [Fact]
    public void Solves_unit_decrement_with_n_log_n_work_as_quadratic_log()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.NLogN(ComplexityVariable.N),
            Decrement(1)));

        AssertSolved(solution, "O(n\u00b2 log n)", computedExponent: 2);
    }

    [Fact]
    public void Solves_unit_decrement_with_quadratic_work_as_cubic()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Polynomial(ComplexityVariable.N, 2),
            Decrement(1)));

        AssertSolved(solution, "O(n\u00b3)", computedExponent: 3);
    }

    [Fact]
    public void Unsupported_cases_return_structured_unsupported_result()
    {
        RecurrenceRelation[] unsupported =
        [
            Relation(ComplexityFactory.Constant(), Decrement(1), Decrement(2)),
            Relation(ComplexityFactory.Constant(), new RecurrenceTerm(2, RecurrenceReduction.SubtractConstant(1))),
            Relation(ComplexityFactory.Constant(), new RecurrenceTerm(1, RecurrenceReduction.Scale(0.5))),
            Relation(ComplexityFactory.Unknown(), Decrement(1)),
            Relation(ComplexityFactory.Exponential(ComplexityVariable.N, 2), Decrement(1)),
            Relation(ComplexityFactory.Factorial(ComplexityVariable.N), Decrement(1)),
            Relation(ComplexityFactory.Linear(ComplexityVariable.M), Decrement(1)),
        ];

        var solver = new SummationRecurrenceSolver();
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
    public void Null_relation_returns_structured_invalid_result()
    {
        var solver = new SummationRecurrenceSolver();

        RecurrenceSolution solution = solver.Solve(null);

        Assert.False(solver.IsApplicable(null));
        Assert.Equal(RecurrenceSolutionKind.Invalid, solution.Kind);
        Assert.Null(solution.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
    }

    private static RecurrenceSolution Solve(RecurrenceRelation relation)
    {
        var solver = new SummationRecurrenceSolver();

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

    private static RecurrenceTerm Decrement(double decrement)
    {
        return new RecurrenceTerm(1, RecurrenceReduction.SubtractConstant(decrement));
    }

    private static void AssertSolved(
        RecurrenceSolution solution,
        string expectedBigO,
        double computedExponent)
    {
        Assert.Equal(RecurrenceSolutionKind.Solved, solution.Kind);
        Assert.Equal(RecurrenceSolverKind.Summation, solution.SolverKind);
        Assert.NotNull(solution.Complexity);
        Assert.Equal(expectedBigO, solution.Complexity.ToBigONotation());
        Assert.Equal(computedExponent, solution.ComputedExponent);
    }
}

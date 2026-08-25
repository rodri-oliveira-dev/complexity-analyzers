using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class RecurrenceSolverTests
{
    [Fact]
    public void Solves_summation_recurrence_first()
    {
        AssertSolved(
            Relation(
                [new RecurrenceTerm(1, RecurrenceReduction.SubtractConstant(1))],
                ComplexityFactory.Constant()),
            RecurrenceSolverKind.Summation,
            "O(n)");
    }

    [Fact]
    public void Solves_constant_coefficient_recurrence_after_summation()
    {
        AssertSolved(
            Relation(
                [new RecurrenceTerm(2, RecurrenceReduction.SubtractConstant(1))],
                ComplexityFactory.Constant()),
            RecurrenceSolverKind.ConstantCoefficient,
            "O(2^n)");
    }

    [Fact]
    public void Solves_master_theorem_recurrence_after_decrement_solvers()
    {
        AssertSolved(
            Relation(
                [new RecurrenceTerm(2, RecurrenceReduction.Scale(0.5))],
                ComplexityFactory.Linear(ComplexityVariable.N)),
            RecurrenceSolverKind.MasterTheorem,
            "O(n log n)");
    }

    [Fact]
    public void Solves_restricted_akra_bazzi_recurrence_after_master_theorem()
    {
        AssertSolved(
            Relation(
                [
                    new RecurrenceTerm(1, RecurrenceReduction.Scale(1.0 / 3.0)),
                    new RecurrenceTerm(1, RecurrenceReduction.Scale(2.0 / 3.0)),
                ],
                ComplexityFactory.Linear(ComplexityVariable.N)),
            RecurrenceSolverKind.RestrictedAkraBazzi,
            "O(n log n)");
    }

    [Fact]
    public void Returns_unsupported_when_no_solver_supports_the_recurrence()
    {
        RecurrenceRelation relation = Relation(
            [new RecurrenceTerm(1, RecurrenceReduction.Scale(0.5))],
            ComplexityFactory.Exponential(ComplexityVariable.N, 2));

        RecurrenceSolution solution = new RecurrenceSolver().Solve(relation);

        Assert.Equal(RecurrenceSolutionKind.Unsupported, solution.Kind);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
        Assert.Null(solution.Complexity);
    }

    [Fact]
    public void Returns_invalid_for_null_recurrence()
    {
        RecurrenceSolution solution = new RecurrenceSolver().Solve(null);

        Assert.Equal(RecurrenceSolutionKind.Invalid, solution.Kind);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
        Assert.Null(solution.Complexity);
    }

    private static void AssertSolved(
        RecurrenceRelation relation,
        RecurrenceSolverKind expectedSolverKind,
        string expectedComplexity)
    {
        RecurrenceSolution solution = new RecurrenceSolver().Solve(relation);

        Assert.Equal(RecurrenceSolutionKind.Solved, solution.Kind);
        Assert.Equal(expectedSolverKind, solution.SolverKind);
        ComplexityExpression complexity = Assert.IsAssignableFrom<ComplexityExpression>(solution.Complexity);
        Assert.Equal(expectedComplexity, complexity.ToBigONotation());
    }

    private static RecurrenceRelation Relation(
        ImmutableArray<RecurrenceTerm> terms,
        ComplexityExpression localWork)
    {
        return new RecurrenceRelation(
            ComplexityVariable.N,
            terms,
            localWork);
    }
}

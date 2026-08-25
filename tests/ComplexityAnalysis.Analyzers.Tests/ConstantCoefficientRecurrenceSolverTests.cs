using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ConstantCoefficientRecurrenceSolverTests
{
    [Theory]
    [InlineData(2, 1, "O(2^n)", 2)]
    [InlineData(3, 1, "O(3^n)", 3)]
    [InlineData(4, 2, "O(2^n)", 2)]
    public void Solves_same_decrement_multiplicity_as_exponential_base(
        int multiplicity,
        int decrement,
        string expectedBigO,
        double expectedBase)
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Constant(),
            new RecurrenceTerm(multiplicity, RecurrenceReduction.SubtractConstant(decrement))));

        AssertSolved(solution, expectedBigO, expectedBase);
    }

    [Fact]
    public void Solves_fibonacci_second_order_recurrence()
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Constant(),
            Decrement(1),
            Decrement(2)));

        AssertSolved(solution, "O(1.618^n)", 1.618);
    }

    [Theory]
    [InlineData(2, 1, "O(2.414^n)", 2.414)]
    [InlineData(1, 2, "O(2^n)", 2)]
    [InlineData(3, 2, "O(3.562^n)", 3.562)]
    public void Solves_custom_positive_second_order_recurrences(
        int firstMultiplicity,
        int secondMultiplicity,
        string expectedBigO,
        double expectedBase)
    {
        RecurrenceSolution solution = Solve(Relation(
            ComplexityFactory.Linear(ComplexityVariable.N),
            new RecurrenceTerm(firstMultiplicity, RecurrenceReduction.SubtractConstant(1)),
            new RecurrenceTerm(secondMultiplicity, RecurrenceReduction.SubtractConstant(2))));

        AssertSolved(solution, expectedBigO, expectedBase);
    }

    [Fact]
    public void Unsupported_cases_return_structured_unsupported_result()
    {
        RecurrenceRelation[] unsupported =
        [
            Relation(ComplexityFactory.Constant(), Decrement(1)),
            Relation(ComplexityFactory.Constant(), Decrement(1), Decrement(2), Decrement(3)),
            Relation(ComplexityFactory.Constant(), new RecurrenceTerm(1, RecurrenceReduction.Scale(0.5))),
            Relation(ComplexityFactory.Unknown(), new RecurrenceTerm(2, RecurrenceReduction.SubtractConstant(1))),
            Relation(ComplexityFactory.Exponential(ComplexityVariable.N, 2), Decrement(1), Decrement(2)),
            Relation(ComplexityFactory.Linear(ComplexityVariable.M), Decrement(1), Decrement(2)),
        ];

        var solver = new ConstantCoefficientRecurrenceSolver();
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
        var solver = new ConstantCoefficientRecurrenceSolver();

        RecurrenceSolution solution = solver.Solve(null);

        Assert.False(solver.IsApplicable(null));
        Assert.Equal(RecurrenceSolutionKind.Invalid, solution.Kind);
        Assert.Null(solution.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, solution.SolverKind);
    }

    private static RecurrenceSolution Solve(RecurrenceRelation relation)
    {
        var solver = new ConstantCoefficientRecurrenceSolver();

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
        double expectedBase)
    {
        Assert.Equal(RecurrenceSolutionKind.Solved, solution.Kind);
        Assert.Equal(RecurrenceSolverKind.ConstantCoefficient, solution.SolverKind);
        ExponentialComplexity complexity = Assert.IsType<ExponentialComplexity>(solution.Complexity);
        Assert.Equal(expectedBase, complexity.Base);
        Assert.Equal(expectedBigO, complexity.ToBigONotation());
        Assert.Equal(expectedBase, solution.ComputedExponent);
    }
}

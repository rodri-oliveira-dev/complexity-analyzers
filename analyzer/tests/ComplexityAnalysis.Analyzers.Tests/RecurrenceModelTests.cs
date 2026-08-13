using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class RecurrenceModelTests
{
    [Fact]
    public void Recurrence_term_represents_subtract_constant_reduction()
    {
        var term = new RecurrenceTerm(
            multiplicity: 1,
            reduction: RecurrenceReduction.SubtractConstant(1));

        Assert.Equal(1, term.Multiplicity);
        Assert.Equal(RecurrenceReductionKind.SubtractConstant, term.Reduction.Kind);
        Assert.Equal(1, term.Reduction.Value);
    }

    [Fact]
    public void Recurrence_term_represents_scale_reduction_with_multiplicity()
    {
        var term = new RecurrenceTerm(
            multiplicity: 2,
            reduction: RecurrenceReduction.Scale(0.5));

        Assert.Equal(2, term.Multiplicity);
        Assert.Equal(RecurrenceReductionKind.Scale, term.Reduction.Kind);
        Assert.Equal(0.5, term.Reduction.Value);
    }

    [Fact]
    public void Recurrence_relation_stores_variable_terms_and_non_recursive_work()
    {
        var term = new RecurrenceTerm(
            multiplicity: 2,
            reduction: RecurrenceReduction.Scale(0.5));
        var relation = new RecurrenceRelation(
            ComplexityVariable.N,
            ImmutableArray.Create(term),
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal(ComplexityVariable.N, relation.ComplexityVariable);
        RecurrenceTerm storedTerm = Assert.Single(relation.RecursiveTerms);
        Assert.Equal(term, storedTerm);
        Assert.Equal(ComplexityFactory.Linear(ComplexityVariable.N), relation.NonRecursiveWork);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Recurrence_term_rejects_invalid_multiplicity(int multiplicity)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => new RecurrenceTerm(multiplicity, RecurrenceReduction.SubtractConstant(1)));
    }

    [Fact]
    public void Recurrence_term_rejects_null_reduction()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new RecurrenceTerm(1, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Recurrence_reduction_rejects_invalid_decrement(double decrement)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => RecurrenceReduction.SubtractConstant(decrement));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Recurrence_reduction_rejects_invalid_scale(double scale)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(
            () => RecurrenceReduction.Scale(scale));
    }

    [Fact]
    public void Recurrence_relation_rejects_invalid_invariants()
    {
        ImmutableArray<RecurrenceTerm> terms =
            ImmutableArray.Create(new RecurrenceTerm(1, RecurrenceReduction.SubtractConstant(1)));

        _ = Assert.Throws<ArgumentNullException>(
            () => new RecurrenceRelation(null!, terms, ComplexityFactory.Constant()));
        _ = Assert.Throws<ArgumentNullException>(
            () => new RecurrenceRelation(ComplexityVariable.N, terms, null!));
        _ = Assert.Throws<ArgumentException>(
            () => new RecurrenceRelation(ComplexityVariable.N, default, ComplexityFactory.Constant()));
        _ = Assert.Throws<ArgumentException>(
            () => new RecurrenceRelation(ComplexityVariable.N, ImmutableArray<RecurrenceTerm>.Empty, ComplexityFactory.Constant()));
        _ = Assert.Throws<ArgumentException>(
            () => new RecurrenceRelation(
                ComplexityVariable.N,
                ImmutableArray.CreateRange(new RecurrenceTerm[] { null! }),
                ComplexityFactory.Constant()));
    }

    [Fact]
    public void Recurrence_model_values_compare_by_value()
    {
        var decrement = RecurrenceReduction.SubtractConstant(1);
        var sameDecrement = RecurrenceReduction.SubtractConstant(1);
        var scaled = RecurrenceReduction.Scale(0.5);

        Assert.Equal(decrement, sameDecrement);
        Assert.NotEqual(decrement, scaled);
        Assert.Equal(
            new RecurrenceTerm(1, decrement),
            new RecurrenceTerm(1, sameDecrement));
        Assert.NotEqual(
            new RecurrenceTerm(1, decrement),
            new RecurrenceTerm(2, decrement));

        var first = new RecurrenceRelation(
            ComplexityVariable.N,
            ImmutableArray.Create(new RecurrenceTerm(1, decrement)),
            ComplexityFactory.Constant());
        var second = new RecurrenceRelation(
            new ComplexityVariable("n"),
            ImmutableArray.Create(new RecurrenceTerm(1, sameDecrement)),
            ComplexityFactory.Constant());

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Recurrence_solution_represents_future_solver_outcomes()
    {
        RecurrenceSolution solved = RecurrenceSolution.Solved(
            ComplexityFactory.Polynomial(ComplexityVariable.N, 1.585),
            RecurrenceSolverKind.MasterTheorem,
            computedExponent: 1.585);

        Assert.Equal(RecurrenceSolutionKind.Solved, solved.Kind);
        Assert.Equal("O(n^1.585)", solved.Complexity!.ToBigONotation());
        Assert.Equal(RecurrenceSolverKind.MasterTheorem, solved.SolverKind);
        Assert.Equal(1.585, solved.ComputedExponent);
        Assert.Equal(RecurrenceSolutionKind.Unsupported, RecurrenceSolution.Unsupported().Kind);
        Assert.Equal(RecurrenceSolutionKind.Invalid, RecurrenceSolution.Invalid().Kind);
        Assert.Equal(RecurrenceSolutionKind.NumericallyInconclusive, RecurrenceSolution.NumericallyInconclusive().Kind);
    }

    [Fact]
    public void Recurrence_model_types_are_immutable_by_observable_api()
    {
        Type[] types =
        [
            typeof(RecurrenceReduction),
            typeof(RecurrenceTerm),
            typeof(RecurrenceRelation),
            typeof(RecurrenceSolution),
        ];

        foreach (Type type in types)
        {
            Assert.True(type.IsSealed, type.Name + " should remain sealed for value-oriented recurrence semantics.");

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                Assert.Null(property.SetMethod);
            }

            Assert.DoesNotContain(
                type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
                field => !field.IsInitOnly);
        }
    }
}

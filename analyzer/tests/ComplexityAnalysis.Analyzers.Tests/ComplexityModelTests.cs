using ComplexityAnalysis.Analyzers.Model;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityModelTests
{
    [Fact]
    public void ComplexityVariable_compares_by_name_value()
    {
        var first = new ComplexityVariable("n");
        var second = new ComplexityVariable("n");
        var different = new ComplexityVariable("m");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, different);
        Assert.Equal("n", first.ToString());
    }

    [Fact]
    public void ComplexityVariable_provides_small_known_variable_set()
    {
        Assert.Equal(new ComplexityVariable("n"), ComplexityVariable.N);
        Assert.Equal(new ComplexityVariable("m"), ComplexityVariable.M);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" n")]
    [InlineData("n ")]
    [InlineData("1n")]
    [InlineData("n-m")]
    [InlineData("n_m")]
    public void ComplexityVariable_rejects_invalid_names(string name)
    {
        _ = Assert.Throws<ArgumentException>(() => new ComplexityVariable(name));
    }

    [Fact]
    public void ComplexityVariable_rejects_null_name()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ComplexityVariable(null!));
    }

    [Theory]
    [InlineData("n")]
    [InlineData("m")]
    [InlineData("V")]
    [InlineData("E")]
    [InlineData("n2")]
    public void ComplexityVariable_accepts_valid_names(string name)
    {
        var variable = new ComplexityVariable(name);

        Assert.Equal(name, variable.Name);
    }

    [Fact]
    public void Atomic_complexity_forms_have_deterministic_big_o_formatting()
    {
        (ComplexityExpression Expression, string Expected)[] cases =
        [
            (ComplexityFactory.Constant(), "O(1)"),
            (ComplexityFactory.LogN(ComplexityVariable.N), "O(log n)"),
            (ComplexityFactory.Linear(ComplexityVariable.N), "O(n)"),
            (ComplexityFactory.NLogN(ComplexityVariable.N), "O(n log n)"),
            (ComplexityFactory.Polynomial(ComplexityVariable.N, 2), "O(n\u00b2)"),
            (ComplexityFactory.Polynomial(ComplexityVariable.N, 3), "O(n\u00b3)"),
            (ComplexityFactory.Polynomial(ComplexityVariable.N, 4), "O(n^4)"),
            (ComplexityFactory.Exponential(ComplexityVariable.N, 2), "O(2^n)"),
            (ComplexityFactory.Factorial(ComplexityVariable.N), "O(n!)"),
            (ComplexityFactory.Unknown(), "Unknown"),
        ];

        foreach ((ComplexityExpression expression, string expected) in cases)
        {
            Assert.Equal(expected, expression.ToBigONotation());
            Assert.Equal(expected, expression.ToString());
        }
    }

    [Fact]
    public void Atomic_complexity_forms_compare_by_value()
    {
        ComplexityVariable n = ComplexityVariable.N;
        ComplexityVariable m = ComplexityVariable.M;

        Assert.Equal(ComplexityFactory.Constant(), ComplexityFactory.Constant());
        Assert.Equal(ComplexityFactory.Unknown(), ComplexityFactory.Unknown());
        Assert.Equal(ComplexityFactory.Linear(n), ComplexityFactory.Linear(new ComplexityVariable("n")));
        Assert.NotEqual(ComplexityFactory.Linear(n), ComplexityFactory.Linear(m));
        Assert.Equal(ComplexityFactory.NLogN(n), new PolynomialLogComplexity(n, polynomialDegree: 1, logExponent: 1));
        Assert.Equal(ComplexityFactory.Exponential(n, 2), ComplexityFactory.Exponential(new ComplexityVariable("n"), 2));
        Assert.Equal(ComplexityFactory.Factorial(n), ComplexityFactory.Factorial(new ComplexityVariable("n")));
    }

    [Fact]
    public void ComplexityFactory_creates_expected_atomic_expression_types()
    {
        ComplexityVariable n = ComplexityVariable.N;

        _ = Assert.IsType<ConstantComplexity>(ComplexityFactory.Constant());
        _ = Assert.IsType<PolynomialLogComplexity>(ComplexityFactory.LogN(n));
        _ = Assert.IsType<PolynomialLogComplexity>(ComplexityFactory.Linear(n));
        _ = Assert.IsType<PolynomialLogComplexity>(ComplexityFactory.NLogN(n));
        _ = Assert.IsType<PolynomialLogComplexity>(ComplexityFactory.Polynomial(n, 4));
        _ = Assert.IsType<ExponentialComplexity>(ComplexityFactory.Exponential(n, 2));
        _ = Assert.IsType<FactorialComplexity>(ComplexityFactory.Factorial(n));
        _ = Assert.IsType<UnknownComplexity>(ComplexityFactory.Unknown());
    }

    [Fact]
    public void Polynomial_factory_normalizes_zero_degree_to_constant()
    {
        ComplexityExpression expression = ComplexityFactory.Polynomial(ComplexityVariable.N, degree: 0);

        _ = Assert.IsType<ConstantComplexity>(expression);
        Assert.Equal("O(1)", expression.ToBigONotation());
    }

    [Fact]
    public void PolynomialLogComplexity_rejects_invalid_invariants()
    {
        ComplexityVariable n = ComplexityVariable.N;

        _ = Assert.Throws<ArgumentNullException>(() => new PolynomialLogComplexity(null!, polynomialDegree: 1, logExponent: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PolynomialLogComplexity(n, polynomialDegree: -1, logExponent: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PolynomialLogComplexity(n, polynomialDegree: 1, logExponent: -1));
        _ = Assert.Throws<ArgumentException>(() => new PolynomialLogComplexity(n, polynomialDegree: 0, logExponent: 0));
    }

    [Fact]
    public void ExponentialComplexity_rejects_invalid_invariants()
    {
        ComplexityVariable n = ComplexityVariable.N;

        _ = Assert.Throws<ArgumentNullException>(() => new ExponentialComplexity(null!, 2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialComplexity(n, 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialComplexity(n, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialComplexity(n, -2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialComplexity(n, double.NaN));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialComplexity(n, double.PositiveInfinity));
    }

    [Fact]
    public void FactorialComplexity_rejects_null_variable()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new FactorialComplexity(null!));
    }

    [Fact]
    public void Unknown_represents_undetermined_complexity_not_constant()
    {
        ComplexityExpression unknown = ComplexityFactory.Unknown();

        Assert.Equal("Unknown", unknown.ToBigONotation());
        Assert.NotEqual(ComplexityFactory.Constant(), unknown);
    }

    [Fact]
    public void Growth_comparer_orders_required_same_variable_forms()
    {
        ComplexityVariable n = ComplexityVariable.N;

        (ComplexityExpression Smaller, ComplexityExpression Larger)[] orderedPairs =
        [
            (ComplexityFactory.Constant(), ComplexityFactory.LogN(n)),
            (ComplexityFactory.LogN(n), ComplexityFactory.Linear(n)),
            (ComplexityFactory.Linear(n), ComplexityFactory.NLogN(n)),
            (ComplexityFactory.NLogN(n), ComplexityFactory.Polynomial(n, 2)),
            (ComplexityFactory.Polynomial(n, 2), ComplexityFactory.Polynomial(n, 3)),
            (ComplexityFactory.Polynomial(n, 3), ComplexityFactory.Exponential(n, 2)),
            (ComplexityFactory.Exponential(n, 2), ComplexityFactory.Factorial(n)),
        ];

        foreach ((ComplexityExpression smaller, ComplexityExpression larger) in orderedPairs)
        {
            AssertComparison(smaller, larger, GrowthComparison.Less);
        }
    }

    [Fact]
    public void Growth_comparer_compares_polylog_by_polynomial_degree_then_log_exponent()
    {
        ComplexityVariable n = ComplexityVariable.N;

        AssertComparison(ComplexityFactory.Linear(n), ComplexityFactory.NLogN(n), GrowthComparison.Less);
        AssertComparison(ComplexityFactory.NLogN(n), ComplexityFactory.Polynomial(n, 2), GrowthComparison.Less);
        AssertComparison(ComplexityFactory.Polynomial(n, 2), new PolynomialLogComplexity(n, polynomialDegree: 2, logExponent: 1), GrowthComparison.Less);
        AssertComparison(new PolynomialLogComplexity(n, polynomialDegree: 2, logExponent: 1), new PolynomialLogComplexity(n, polynomialDegree: 2, logExponent: 3), GrowthComparison.Less);
        AssertComparison(new PolynomialLogComplexity(n, polynomialDegree: 3, logExponent: 0), new PolynomialLogComplexity(n, polynomialDegree: 2, logExponent: 10), GrowthComparison.Greater);
    }

    [Fact]
    public void Growth_comparer_orders_arbitrary_polynomial_degrees()
    {
        ComplexityVariable n = ComplexityVariable.N;

        AssertComparison(ComplexityFactory.Polynomial(n, 4), ComplexityFactory.Polynomial(n, 5), GrowthComparison.Less);
        AssertComparison(ComplexityFactory.Polynomial(n, 12), ComplexityFactory.Polynomial(n, 9), GrowthComparison.Greater);
    }

    [Fact]
    public void Growth_comparer_orders_polynomial_exponential_and_factorial_for_same_variable()
    {
        ComplexityVariable n = ComplexityVariable.N;

        AssertComparison(ComplexityFactory.Polynomial(n, 10), ComplexityFactory.Exponential(n, 2), GrowthComparison.Less);
        AssertComparison(ComplexityFactory.Exponential(n, 2), ComplexityFactory.Factorial(n), GrowthComparison.Less);
    }

    [Fact]
    public void Growth_comparer_orders_safe_exponential_bases_for_same_variable()
    {
        ComplexityVariable n = ComplexityVariable.N;

        AssertComparison(ComplexityFactory.Exponential(n, 2), ComplexityFactory.Exponential(n, 3), GrowthComparison.Less);
        AssertComparison(ComplexityFactory.Exponential(n, 10), ComplexityFactory.Exponential(n, 3), GrowthComparison.Greater);
    }

    [Fact]
    public void Growth_comparer_reports_asymptotic_equivalence_for_equal_normalized_forms()
    {
        ComplexityVariable n = ComplexityVariable.N;

        AssertComparison(ComplexityFactory.Constant(), ComplexityFactory.Constant(), GrowthComparison.Equivalent);
        AssertComparison(ComplexityFactory.Linear(n), ComplexityFactory.Linear(new ComplexityVariable("n")), GrowthComparison.Equivalent);
        AssertComparison(ComplexityFactory.NLogN(n), new PolynomialLogComplexity(new ComplexityVariable("n"), polynomialDegree: 1, logExponent: 1), GrowthComparison.Equivalent);
        AssertComparison(ComplexityFactory.Exponential(n, 2), ComplexityFactory.Exponential(new ComplexityVariable("n"), 2), GrowthComparison.Equivalent);
        AssertComparison(ComplexityFactory.Factorial(n), ComplexityFactory.Factorial(new ComplexityVariable("n")), GrowthComparison.Equivalent);
    }

    [Fact]
    public void Growth_comparer_does_not_order_independent_variables()
    {
        ComplexityVariable n = ComplexityVariable.N;
        ComplexityVariable m = ComplexityVariable.M;

        (ComplexityExpression Left, ComplexityExpression Right)[] incomparablePairs =
        [
            (ComplexityFactory.Linear(n), ComplexityFactory.Linear(m)),
            (ComplexityFactory.LogN(n), ComplexityFactory.LogN(m)),
            (ComplexityFactory.NLogN(n), ComplexityFactory.NLogN(m)),
            (ComplexityFactory.Polynomial(n, 2), ComplexityFactory.Polynomial(m, 2)),
            (ComplexityFactory.Polynomial(n, 2), ComplexityFactory.Exponential(m, 2)),
            (ComplexityFactory.Exponential(n, 2), ComplexityFactory.Exponential(m, 3)),
            (ComplexityFactory.Exponential(n, 2), ComplexityFactory.Factorial(m)),
            (ComplexityFactory.Factorial(n), ComplexityFactory.Factorial(m)),
        ];

        foreach ((ComplexityExpression left, ComplexityExpression right) in incomparablePairs)
        {
            AssertComparison(left, right, GrowthComparison.Incomparable);
        }
    }

    [Fact]
    public void Growth_comparer_orders_constant_against_known_variable_dependent_forms()
    {
        ComplexityVariable n = ComplexityVariable.N;
        ComplexityVariable m = ComplexityVariable.M;

        AssertComparison(ComplexityFactory.Constant(), ComplexityFactory.Linear(n), GrowthComparison.Less);
        AssertComparison(ComplexityFactory.Constant(), ComplexityFactory.Exponential(m, 2), GrowthComparison.Less);
        AssertComparison(ComplexityFactory.Constant(), ComplexityFactory.Factorial(n), GrowthComparison.Less);
    }

    [Fact]
    public void Growth_comparer_keeps_unknown_incomparable_except_same_unknown_value()
    {
        ComplexityExpression unknown = ComplexityFactory.Unknown();

        AssertComparison(unknown, ComplexityFactory.Constant(), GrowthComparison.Incomparable);
        AssertComparison(unknown, ComplexityFactory.Linear(ComplexityVariable.N), GrowthComparison.Incomparable);
        AssertComparison(unknown, ComplexityFactory.Unknown(), GrowthComparison.Equivalent);
    }

    [Fact]
    public void Growth_comparer_rejects_null_operands()
    {
        _ = Assert.Throws<ArgumentNullException>(() => ComplexityGrowthComparer.Compare(null!, ComplexityFactory.Constant()));
        _ = Assert.Throws<ArgumentNullException>(() => ComplexityGrowthComparer.Compare(ComplexityFactory.Constant(), null!));
    }

    private static void AssertComparison(ComplexityExpression left, ComplexityExpression right, GrowthComparison expected)
    {
        Assert.Equal(expected, ComplexityGrowthComparer.Compare(left, right));
        Assert.Equal(Invert(expected), ComplexityGrowthComparer.Compare(right, left));
    }

    private static GrowthComparison Invert(GrowthComparison comparison)
    {
        return comparison switch
        {
            GrowthComparison.Less => GrowthComparison.Greater,
            GrowthComparison.Greater => GrowthComparison.Less,
            GrowthComparison.Equivalent => GrowthComparison.Equivalent,
            GrowthComparison.Incomparable => GrowthComparison.Incomparable,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }
}

using System.Collections.Immutable;
using System.Reflection;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class MethodComplexityExtractorTests
{
    [Fact]
    public void Empty_block_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Expression_bodied_literal_method_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M() => 42;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Expression_bodied_arithmetic_method_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int a, int b) => a + b;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Return_literal_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M()
                {
                    return 42;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Primitive_comparison_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                bool M(int a, int b) => a < b;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Local_declaration_and_assignment_are_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M()
                {
                    int value = 1;
                    value = 2;
                    return value;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Increment_and_decrement_are_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M()
                {
                    int value = 1;
                    value++;
                    --value;
                    return value;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Sequential_constant_operations_remain_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int a, int b)
                {
                    int value = a + b;
                    value = value * 2;
                    value++;
                    return value;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Array_length_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int[] values) => values.Length;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void String_length_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(string text) => text.Length;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Array_element_access_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int[] values) => values[0];
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Custom_property_is_not_assumed_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Holder
            {
                public int Length => 1;
            }

            public sealed class Sample
            {
                int M(Holder holder) => holder.Length;
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Invocation_is_unknown_even_when_it_is_linq_count()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(List<int> values) => values.Count();
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Custom_indexer_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Indexed
            {
                public int this[int index] => index;
            }

            public sealed class Sample
            {
                int M(Indexed indexed) => indexed[0];
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Object_creation_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                object M() => new object();
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Array_creation_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int[] M(int length) => new int[length];
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Foreach_over_array_input_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    foreach (var item in items)
                    {
                        var x = item + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Foreach_over_string_input_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(string text)
                {
                    foreach (var ch in text)
                    {
                        var x = ch;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Foreach_over_collection_input_is_linear()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(IReadOnlyCollection<int> values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Foreach_over_unknown_origin_is_unknown()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M()
                {
                    foreach (var value in GetValues())
                    {
                        var x = value + 1;
                    }
                }

                IEnumerable<int> GetValues() => null;
            }
            """,
            "Unknown");
    }

    [Fact]
    public void For_from_zero_to_integral_bound_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_from_zero_to_length_bound_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        var x = values[i];
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_from_bound_down_to_zero_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = count; i > 0; i--)
                    {
                        var x = i - 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_with_constant_additive_step_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 0; i <= count; i += 2)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_with_multiplicative_step_by_two_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 1; i < count; i *= 2)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void For_with_multiplicative_step_by_three_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 1; i < count; i *= 3)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void For_with_divisive_step_by_two_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = count; i > 1; i /= 2)
                    {
                        var x = i - 1;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void Constant_bound_for_is_constant_with_constant_body()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                    var x = 0;
                    for (var i = 0; i < 10; i++)
                    {
                        x++;
                    }
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Nested_loops_over_same_input_are_quadratic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    foreach (var outer in items)
                    {
                        foreach (var inner in items)
                        {
                            var x = outer + inner;
                        }
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Nested_loops_over_independent_inputs_preserve_product()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] left, int[] right)
                {
                    foreach (var l in left)
                    {
                        foreach (var r in right)
                        {
                            var x = l + r;
                        }
                    }
                }
            }
            """,
            "O(n \u00b7 m)");
    }

    [Fact]
    public void While_with_increment_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 0;
                    while (i < count)
                    {
                        i++;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void While_with_constant_add_assignment_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 0;
                    while (i < count)
                    {
                        i += 2;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void While_with_multiplicative_step_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 1;
                    while (i < count)
                    {
                        i *= 2;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void While_with_divisive_step_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = count;
                    while (i > 1)
                    {
                        i /= 2;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void While_without_provable_bound_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool condition)
                {
                    while (condition)
                    {
                        var x = 1;
                    }
                }
            }
            """,
            "Unknown");
    }

    [Theory]
    [InlineData("factor-one")]
    [InlineData("variable-factor")]
    [InlineData("condition-without-control-variable")]
    [InlineData("multiple-control-mutations")]
    public void Unsupported_while_patterns_are_unknown(string scenario)
    {
        string source = scenario switch
        {
            "factor-one" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 1;
                        while (i < count)
                        {
                            i *= 1;
                        }
                    }
                }
                """,
            "variable-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count, int factor)
                    {
                        var i = 1;
                        while (i < count)
                        {
                            i *= factor;
                        }
                    }
                }
                """,
            "condition-without-control-variable" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 1;
                        while (count > 1)
                        {
                            i *= 2;
                        }
                    }
                }
                """,
            "multiple-control-mutations" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 0;
                        while (i < count)
                        {
                            i++;
                            i *= 2;
                        }
                    }
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        AssertMethodComplexity(source, "Unknown");
    }

    [Fact]
    public void Do_while_with_increment_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 0;
                    do
                    {
                        i++;
                    }
                    while (i < count);
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Do_while_with_multiplicative_step_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 1;
                    do
                    {
                        i *= 2;
                    }
                    while (i < count);
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void Do_while_without_provable_bound_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool condition)
                {
                    do
                    {
                        var x = 1;
                    }
                    while (condition);
                }
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Nested_linear_outer_and_logarithmic_inner_compose_to_n_log_n()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        for (var j = 1; j < count; j *= 2)
                        {
                            var x = i + j;
                        }
                    }
                }
            }
            """,
            "O(n log n)");
    }

    [Fact]
    public void Nested_logarithmic_loops_compose_to_squared_log()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 1; i < count; i *= 2)
                    {
                        for (var j = 1; j < count; j *= 2)
                        {
                            var x = i + j;
                        }
                    }
                }
            }
            """,
            "O(log^2 n)");
    }

    [Fact]
    public void Simple_if_without_else_uses_true_branch_as_worst_case()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void If_else_uses_worst_case_branch()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        var x = 1;
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Else_if_chain_uses_worst_case_alternative()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool fast, bool slow, int[] items)
                {
                    if (fast)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                    else if (slow)
                    {
                        foreach (var outer in items)
                        {
                            foreach (var inner in items)
                            {
                                var x = outer + inner;
                            }
                        }
                    }
                    else
                    {
                        var x = 1;
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Branching_selects_quadratic_over_linear_branch()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                    else
                    {
                        foreach (var outer in items)
                        {
                            foreach (var inner in items)
                            {
                                var x = outer + inner;
                            }
                        }
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Branching_selects_n_log_n_over_linear_branch()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            for (var i = 1; i < items.Length; i *= 2)
                            {
                                var x = item + i;
                            }
                        }
                    }
                }
            }
            """,
            "O(n log n)");
    }

    [Fact]
    public void Switch_uses_worst_case_across_cases()
    {
        AssertMethodComplexity(
            """
            public enum Mode
            {
                Fast,
                Slow
            }

            public sealed class Sample
            {
                void M(Mode mode, int[] items)
                {
                    switch (mode)
                    {
                        case Mode.Fast:
                            var y = 1;
                            break;
                        case Mode.Slow:
                            foreach (var item in items)
                            {
                                var x = item + 1;
                            }
                            break;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Switch_default_participates_in_worst_case()
    {
        AssertMethodComplexity(
            """
            public enum Mode
            {
                Fast,
                Slow
            }

            public sealed class Sample
            {
                void M(Mode mode, int[] items)
                {
                    switch (mode)
                    {
                        case Mode.Fast:
                            foreach (var item in items)
                            {
                                var x = item + 1;
                            }
                            break;
                        default:
                            foreach (var outer in items)
                            {
                                foreach (var inner in items)
                                {
                                    var x = outer + inner;
                                }
                            }
                            break;
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Nested_branching_uses_worst_case_inner_branch()
    {
        AssertMethodComplexity(
            """
            public enum Mode
            {
                Fast,
                Slow
            }

            public sealed class Sample
            {
                void M(bool enabled, Mode mode, int[] items)
                {
                    if (enabled)
                    {
                        switch (mode)
                        {
                            case Mode.Fast:
                                foreach (var item in items)
                                {
                                    var x = item + 1;
                                }
                                break;
                            default:
                                foreach (var outer in items)
                                {
                                    foreach (var inner in items)
                                    {
                                        var x = outer + inner;
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        var x = 1;
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Branch_containing_unresolved_invocation_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        Visit();
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                }

                void Visit()
                {
                }
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Condition_containing_unresolved_invocation_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                    if (IsEnabled())
                    {
                        var x = 1;
                    }
                }

                bool IsEnabled() => true;
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Switch_expression_remains_out_of_scope()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int value) => value switch
                {
                    0 => 1,
                    _ => 2
                };
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Switch_pattern_labels_remain_out_of_scope()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(object value)
                {
                    switch (value)
                    {
                        case int number when IsPositive(number):
                            var x = number + 1;
                            break;
                        default:
                            var y = 1;
                            break;
                    }
                }

                bool IsPositive(int value) => value > 0;
            }
            """,
            "Unknown");
    }

    [Theory]
    [InlineData("missing-condition")]
    [InlineData("wrong-increment-variable")]
    [InlineData("inconsistent-progression")]
    [InlineData("invocation-bound")]
    [InlineData("invalid-multiplicative-factor")]
    [InlineData("invalid-divisive-factor")]
    [InlineData("variable-multiplicative-factor")]
    [InlineData("unrelated-condition")]
    public void Unsupported_for_patterns_are_unknown(string scenario)
    {
        string source = scenario switch
        {
            "missing-condition" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 0; ; i++)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "wrong-increment-variable" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var j = 0;
                        for (var i = 0; i < count; j++)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "inconsistent-progression" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = count; i > 0; i++)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "invocation-bound" =>
                """
                public sealed class Sample
                {
                    void M()
                    {
                        for (var i = 0; i < GetLimit(); i++)
                        {
                            var x = i + 1;
                        }
                    }

                    int GetLimit() => 10;
                }
                """,
            "invalid-multiplicative-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 1; i < count; i *= 1)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "invalid-divisive-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = count; i > 1; i /= 1)
                        {
                            var x = i - 1;
                        }
                    }
                }
                """,
            "variable-multiplicative-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count, int factor)
                    {
                        for (var i = 1; i < count; i *= factor)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "unrelated-condition" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 1; count > 1; i *= 2)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        AssertMethodComplexity(source, "Unknown");
    }

    [Fact]
    public void Already_cancelled_token_is_respected_by_method_extractor()
    {
        MethodFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                int M() => 42;
            }
            """);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var extractor = new MethodComplexityExtractor();

        _ = Assert.Throws<OperationCanceledException>(() =>
            extractor.AnalyzeMethod(
                facts.MethodDeclaration,
                facts.SemanticModel,
                cancellationTokenSource.Token));
    }

    private static void AssertMethodComplexity(string source, string expected)
    {
        MethodFacts facts = CreateFacts(source);
        var extractor = new MethodComplexityExtractor();

        string actual = extractor
            .AnalyzeMethod(facts.MethodDeclaration, facts.SemanticModel, CancellationToken.None)
            .ToBigONotation();

        Assert.Equal(expected, actual);
    }

    private static MethodFacts CreateFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "MethodComplexityExtractorTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        MethodDeclarationSyntax methodDeclaration = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "M");

        return new MethodFacts(semanticModel, methodDeclaration);
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } =
        [
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CancellationToken).GetTypeInfo().Assembly.Location)
        ];

    private sealed record MethodFacts(
        SemanticModel SemanticModel,
        MethodDeclarationSyntax MethodDeclaration);
}

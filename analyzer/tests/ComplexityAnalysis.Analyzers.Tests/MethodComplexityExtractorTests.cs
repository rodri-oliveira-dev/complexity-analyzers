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

    [Theory]
    [InlineData("missing-condition")]
    [InlineData("wrong-increment-variable")]
    [InlineData("inconsistent-progression")]
    [InlineData("invocation-bound")]
    [InlineData("multiplicative-progression")]
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
            "multiplicative-progression" =>
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

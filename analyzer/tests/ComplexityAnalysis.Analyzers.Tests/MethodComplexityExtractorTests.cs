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

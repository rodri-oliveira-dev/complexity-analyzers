using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class RecurrenceExtractorTests
{
    [Fact]
    public void Extracts_single_decrement_with_constant_local_work()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                int M(int n)
                {
                    if (n <= 1)
                    {
                        return 1;
                    }

                    return M(n - 1);
                }
            }
            """);

        Assert.Equal("n", relation.ComplexityVariable.Name);
        Assert.Equal("O(1)", relation.NonRecursiveWork.ToBigONotation());
        RecurrenceTerm term = Assert.Single(relation.RecursiveTerms);
        AssertTerm(term, 1, RecurrenceReductionKind.SubtractConstant, 1);
    }

    [Fact]
    public void Extracts_single_decrement_with_linear_local_work()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    for (var i = 0; i < n; i++)
                    {
                        var value = i + 1;
                    }

                    M(n - 1);
                }
            }
            """);

        Assert.Equal("O(n)", relation.NonRecursiveWork.ToBigONotation());
        RecurrenceTerm term = Assert.Single(relation.RecursiveTerms);
        AssertTerm(term, 1, RecurrenceReductionKind.SubtractConstant, 1);
    }

    [Fact]
    public void Extracts_two_equal_sequential_decrements_as_multiplicity()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    M(n - 1);
                    M(n - 1);
                }
            }
            """);

        RecurrenceTerm term = Assert.Single(relation.RecursiveTerms);
        AssertTerm(term, 2, RecurrenceReductionKind.SubtractConstant, 1);
        Assert.Equal("O(1)", relation.NonRecursiveWork.ToBigONotation());
    }

    [Fact]
    public void Extracts_fibonacci_like_decrement_terms()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                int M(int n)
                {
                    if (n <= 1)
                    {
                        return n;
                    }

                    return M(n - 1) + M(n - 2);
                }
            }
            """);

        Assert.Equal(2, relation.RecursiveTerms.Length);
        AssertTerm(relation.RecursiveTerms[0], 1, RecurrenceReductionKind.SubtractConstant, 1);
        AssertTerm(relation.RecursiveTerms[1], 1, RecurrenceReductionKind.SubtractConstant, 2);
        Assert.Equal("O(1)", relation.NonRecursiveWork.ToBigONotation());
    }

    [Fact]
    public void Extracts_single_halving_term()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                int M(int n)
                {
                    if (n <= 1)
                    {
                        return 0;
                    }

                    return M(n / 2);
                }
            }
            """);

        RecurrenceTerm term = Assert.Single(relation.RecursiveTerms);
        AssertTerm(term, 1, RecurrenceReductionKind.Scale, 0.5);
        Assert.Equal("O(1)", relation.NonRecursiveWork.ToBigONotation());
    }

    [Fact]
    public void Extracts_two_halving_terms_with_linear_local_work()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    M(n / 2);
                    M(n / 2);

                    for (var i = 0; i < n; i++)
                    {
                        var value = i + 1;
                    }
                }
            }
            """);

        RecurrenceTerm term = Assert.Single(relation.RecursiveTerms);
        AssertTerm(term, 2, RecurrenceReductionKind.Scale, 0.5);
        Assert.Equal("O(n)", relation.NonRecursiveWork.ToBigONotation());
    }

    [Fact]
    public void Extracts_unequal_split_terms_with_linear_local_work()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                void M(double n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    M(n / 3);
                    M(n * (2.0 / 3.0));

                    for (var i = 0; i < n; i++)
                    {
                        var value = i + 1;
                    }
                }
            }
            """);

        Assert.Equal(2, relation.RecursiveTerms.Length);
        AssertTerm(relation.RecursiveTerms[0], 1, RecurrenceReductionKind.Scale, 1.0 / 3.0);
        AssertTerm(relation.RecursiveTerms[1], 1, RecurrenceReductionKind.Scale, 2.0 / 3.0);
        Assert.Equal("O(n)", relation.NonRecursiveWork.ToBigONotation());
    }

    [Fact]
    public void Exclusive_branch_equal_recursive_calls_do_not_add_multiplicity()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                void M(bool left, int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    if (left)
                    {
                        M(false, n / 2);
                    }
                    else
                    {
                        M(false, n / 2);
                    }
                }
            }
            """);

        RecurrenceTerm term = Assert.Single(relation.RecursiveTerms);
        AssertTerm(term, 1, RecurrenceReductionKind.Scale, 0.5);
    }

    [Fact]
    public void Exclusive_branch_different_recursive_calls_select_the_worst_case_path()
    {
        RecurrenceRelation relation = AssertExtracted(
            """
            public sealed class Sample
            {
                void M(bool left, int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    if (left)
                    {
                        M(false, n - 1);
                    }
                    else
                    {
                        M(false, n - 2);
                    }
                }
            }
            """);

        RecurrenceTerm term = Assert.Single(relation.RecursiveTerms);
        AssertTerm(term, 1, RecurrenceReductionKind.SubtractConstant, 1);
    }

    [Fact]
    public void Missing_base_case_is_unsupported()
    {
        RecurrenceExtractionResult result = Extract(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    M(n - 1);
                }
            }
            """);

        AssertUnsupportedReason(result, "base case");
    }

    [Theory]
    [InlineData("M(n)")]
    [InlineData("M(n + 1)")]
    public void Non_reducing_recursive_argument_is_unsupported(string invocation)
    {
        RecurrenceExtractionResult result = Extract(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

            """ + invocation + """
                    ;
                }
            }
            """);

        AssertUnsupportedReason(result, "not reducing");
    }

    [Fact]
    public void Different_recursive_dimensions_are_unsupported()
    {
        RecurrenceExtractionResult result = Extract(
            """
            public sealed class Sample
            {
                void M(int n, int m)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    M(n - 1, m - 1);
                }
            }
            """);

        AssertUnsupportedReason(result, "dimensions");
    }

    [Fact]
    public void Complex_recursive_argument_is_unsupported()
    {
        RecurrenceExtractionResult result = Extract(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    M(Next(n));
                }

                int Next(int value) => value - 1;
            }
            """);

        AssertUnsupportedReason(result, "not reducing");
    }

    [Fact]
    public void Mutual_recursion_is_not_extracted_as_direct_recurrence()
    {
        RecurrenceExtractionResult result = Extract(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    Other(n - 1);
                }

                void Other(int n)
                {
                    M(n - 1);
                }
            }
            """,
            methodName: "M");

        AssertUnsupportedReason(result, "direct recursive");
    }

    [Fact]
    public void Unknown_local_work_keeps_extraction_unknown()
    {
        RecurrenceExtractionResult result = Extract(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    System.Console.WriteLine(n);
                    M(n - 1);
                }
            }
            """);

        Assert.Equal(RecurrenceExtractionResultKind.Unknown, result.Kind);
        Assert.NotNull(result.Reason);
        Assert.Contains("local work", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static RecurrenceRelation AssertExtracted(string source)
    {
        RecurrenceExtractionResult result = Extract(source);

        Assert.Equal(RecurrenceExtractionResultKind.Extracted, result.Kind);
        Assert.NotNull(result.Relation);
        return result.Relation;
    }

    private static void AssertUnsupportedReason(
        RecurrenceExtractionResult result,
        string expectedReasonFragment)
    {
        Assert.Equal(RecurrenceExtractionResultKind.Unsupported, result.Kind);
        Assert.NotNull(result.Reason);
        Assert.Contains(expectedReasonFragment, result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertTerm(
        RecurrenceTerm term,
        int multiplicity,
        RecurrenceReductionKind kind,
        double value)
    {
        Assert.Equal(multiplicity, term.Multiplicity);
        Assert.Equal(kind, term.Reduction.Kind);
        Assert.Equal(value, term.Reduction.Value, precision: 12);
    }

    private static RecurrenceExtractionResult Extract(
        string source,
        string methodName = "M")
    {
        MethodFacts facts = CreateFacts(source, methodName);

        return new RecurrenceExtractor().Extract(
            facts.MethodDeclaration,
            facts.SemanticModel,
            CancellationToken.None);
    }

    private static MethodFacts CreateFacts(string source, string methodName)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "RecurrenceExtractorTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        MethodDeclarationSyntax methodDeclaration = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName));

        return new MethodFacts(semanticModel, methodDeclaration);
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } = CreateTrustedPlatformReferences();

    private static ImmutableArray<MetadataReference> CreateTrustedPlatformReferences()
    {
        string trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? string.Empty;

        return
        [
            .. trustedPlatformAssemblies.Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record MethodFacts(
        SemanticModel SemanticModel,
        MethodDeclarationSyntax MethodDeclaration);
}

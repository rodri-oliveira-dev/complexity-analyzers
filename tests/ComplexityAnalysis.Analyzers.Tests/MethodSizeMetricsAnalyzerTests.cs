using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class MethodSizeMetricsAnalyzerTests
{
    [Fact]
    public void Block_body_reports_documented_size_metrics()
    {
        MethodSizeMetricsResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    var value = left + right;
                    value *= 2;
                    return value;
                }
            }
            """);

        Assert.Equal(3, result.Nloc);
        Assert.Equal(3, result.StatementCount);
        Assert.Equal(16, result.TokenCount);
    }

    [Fact]
    public void Comment_only_and_blank_lines_do_not_affect_nloc()
    {
        MethodSizeMetricsResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    // This comment does not count.
                    var value = left + right;

                    /*
                     * This block comment does not count either.
                     */
                    value *= 2; // Trailing comments do not add lines.

                    return value;
                }
            }
            """);

        Assert.Equal(3, result.Nloc);
        Assert.Equal(3, result.StatementCount);
        Assert.Equal(16, result.TokenCount);
    }

    [Fact]
    public void Statement_and_token_counts_are_stable_across_whitespace_only_layout_changes()
    {
        MethodSizeMetricsResult compact = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    var value = left + right;
                    value *= 2;
                    return value;
                }
            }
            """);
        MethodSizeMetricsResult multiline = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    var value =
                        left
                        + right;

                    value
                        *=
                        2;

                    return
                        value;
                }
            }
            """);

        Assert.Equal(compact.StatementCount, multiline.StatementCount);
        Assert.Equal(compact.TokenCount, multiline.TokenCount);
    }

    [Fact]
    public void Expression_bodied_member_counts_expression_as_one_statement()
    {
        MethodSizeMetricsResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int value) => value + 1;
            }
            """);

        Assert.Equal(1, result.Nloc);
        Assert.Equal(1, result.StatementCount);
        Assert.Equal(3, result.TokenCount);
    }

    [Fact]
    public void Nested_blocks_do_not_count_as_structural_statements()
    {
        MethodSizeMetricsResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                void M()
                {
                    {
                        {
                            var value = 1;
                        }
                    }
                }
            }
            """);

        Assert.Equal(1, result.Nloc);
        Assert.Equal(1, result.StatementCount);
        Assert.Equal(11, result.TokenCount);
    }

    [Fact]
    public void Nested_executable_members_do_not_inflate_parent_metrics()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M()
                {
                    var outer = 0;
                    void Local(int value)
                    {
                        if (value > 0)
                        {
                            outer++;
                        }
                    }

                    System.Action action = () =>
                    {
                        if (outer > 0)
                        {
                            outer++;
                        }
                    };

                    outer++;
                }
            }
            """);
        ExecutableMember member = CreateMethodMember(facts, "M");

        MethodSizeMetricsResult result = Analyze(member);

        Assert.Equal(4, result.Nloc);
        Assert.Equal(4, result.StatementCount);
        Assert.Equal(22, result.TokenCount);
    }

    [Fact]
    public void Local_function_root_is_measured_independently()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M()
                {
                    var outer = 0;
                    void Local(int value)
                    {
                        if (value > 0)
                        {
                            outer++;
                        }
                    }
                }
            }
            """);
        LocalFunctionStatementSyntax localFunction = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Single();
        Assert.True(ExecutableMember.TryCreate(
            localFunction,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member));
        Assert.NotNull(member);

        MethodSizeMetricsResult result = Analyze(member);

        Assert.Equal(2, result.Nloc);
        Assert.Equal(2, result.StatementCount);
        Assert.Equal(13, result.TokenCount);
    }

    [Fact]
    public void Lambda_root_is_measured_independently()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M()
                {
                    System.Func<int, int> lambda = value =>
                    {
                        if (value > 0)
                        {
                            return value;
                        }

                        return -value;
                    };
                }
            }
            """);
        LambdaExpressionSyntax lambda = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .Single();
        Assert.True(ExecutableMember.TryCreate(
            lambda,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member));
        Assert.NotNull(member);

        MethodSizeMetricsResult result = Analyze(member);

        Assert.Equal(3, result.Nloc);
        Assert.Equal(3, result.StatementCount);
        Assert.Equal(17, result.TokenCount);
    }

    [Fact]
    public void Bodyless_method_does_not_produce_a_metric_result()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public partial class Sample
            {
                partial void M();
            }
            """);
        ExecutableMember member = CreateMethodMember(facts, "M");

        bool analyzed = new MethodSizeMetricsAnalyzer().TryAnalyze(
            member,
            CancellationToken.None,
            out _);

        Assert.False(analyzed);
    }

    [Fact]
    public void Already_canceled_token_stops_analysis()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M()
                {
                    var value = 1;
                }
            }
            """);
        ExecutableMember member = CreateMethodMember(facts, "M");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            new MethodSizeMetricsAnalyzer().TryAnalyze(
                member,
                cancellation.Token,
                out _));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static MethodSizeMetricsResult AnalyzeMethod(string source)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMethodMember(facts, "M");

        return Analyze(member);
    }

    private static MethodSizeMetricsResult Analyze(ExecutableMember member)
    {
        bool analyzed = new MethodSizeMetricsAnalyzer().TryAnalyze(
            member,
            CancellationToken.None,
            out MethodSizeMetricsResult result);

        Assert.True(analyzed);
        return result;
    }

    private static ExecutableMember CreateMethodMember(
        CompilationFacts facts,
        string methodName)
    {
        MethodDeclarationSyntax method = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == methodName);

        bool created = ExecutableMember.TryCreateOrdinaryMethod(
            method,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        return member;
    }

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Parse, SourceCodeKind.Regular));
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "MethodSizeMetricsAnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        return new CompilationFacts(syntaxTree, compilation.GetSemanticModel(syntaxTree));
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
            .. trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record CompilationFacts(
        SyntaxTree SyntaxTree,
        SemanticModel SemanticModel);
}

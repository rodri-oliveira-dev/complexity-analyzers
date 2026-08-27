using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis.Recursion;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class RecursiveCallAnalyzerTests
{
    [Fact]
    public void Direct_self_call_is_identified_semantically()
    {
        RecursiveCallAnalysisResult result = Analyze(
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

        RecursiveCallShape call = AssertSingleCall(result);
        RecursiveArgumentRelation relation = Assert.Single(call.ArgumentRelations);
        Assert.Equal(RecursiveArgumentRelationKind.Reducing, relation.Kind);
        Assert.Equal(RecurrenceReductionKind.SubtractConstant, relation.Reduction!.Kind);
        Assert.Equal(1, relation.Reduction.Value);
    }

    [Fact]
    public void Same_name_overload_is_not_direct_recursion_without_matching_symbol_identity()
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int M(int n)
                {
                    if (n <= 1)
                    {
                        return 1;
                    }

                    return M("value");
                }

                int M(string value) => value.Length;
            }
            """,
            methodPredicate: method => method.Parameters[0].Type.SpecialType == SpecialType.System_Int32);

        Assert.True(result.IsSupported);
        Assert.False(result.HasDirectRecursiveCalls);
    }

    [Theory]
    [InlineData("n - 1", (int)RecurrenceReductionKind.SubtractConstant, 1)]
    [InlineData("n - 2", (int)RecurrenceReductionKind.SubtractConstant, 2)]
    [InlineData("n + (-2)", (int)RecurrenceReductionKind.SubtractConstant, 2)]
    [InlineData("n / 2", (int)RecurrenceReductionKind.Scale, 0.5)]
    [InlineData("n / 3", (int)RecurrenceReductionKind.Scale, 0.33333333333333331)]
    [InlineData("n * 0.5", (int)RecurrenceReductionKind.Scale, 0.5)]
    [InlineData("0.5 * n", (int)RecurrenceReductionKind.Scale, 0.5)]
    [InlineData("(-2) + n", (int)RecurrenceReductionKind.SubtractConstant, 2)]
    public void Reducing_argument_forms_are_identified(
        string argument,
        int expectedKind,
        double expectedValue)
    {
        RecursiveArgumentRelation relation = AnalyzeSingleArgumentRelation(
            "double",
            argument);

        Assert.Equal(RecursiveArgumentRelationKind.Reducing, relation.Kind);
        Assert.Equal((RecurrenceReductionKind)expectedKind, relation.Reduction!.Kind);
        Assert.Equal(expectedValue, relation.Reduction.Value, precision: 12);
    }

    [Theory]
    [InlineData("n", (int)RecursiveArgumentRelationKind.Unchanged)]
    [InlineData("n + 1", (int)RecursiveArgumentRelationKind.Increasing)]
    [InlineData("1 + n", (int)RecursiveArgumentRelationKind.Increasing)]
    [InlineData("n - 0", (int)RecursiveArgumentRelationKind.Increasing)]
    [InlineData("n * 1", (int)RecursiveArgumentRelationKind.Unchanged)]
    [InlineData("n / 1", (int)RecursiveArgumentRelationKind.Unchanged)]
    [InlineData("n % 2", (int)RecursiveArgumentRelationKind.Unknown)]
    [InlineData("1 - n", (int)RecursiveArgumentRelationKind.Unknown)]
    public void Non_reducing_argument_forms_are_not_marked_as_reducing(
        string argument,
        int expectedKind)
    {
        RecursiveArgumentRelation relation = AnalyzeSingleArgumentRelation(
            "double",
            argument);

        Assert.Equal((RecursiveArgumentRelationKind)expectedKind, relation.Kind);
        Assert.False(relation.IsReducing);
        Assert.Null(relation.Reduction);
    }

    [Fact]
    public void Recursive_relation_uses_the_matching_input_parameter_dimension()
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int Foo(bool flag, int n)
                {
                    if (n == 0)
                    {
                        return 0;
                    }

                    return Foo(false, n - 1);
                }
            }
            """,
            methodName: "Foo");

        RecursiveArgumentRelation relation = Assert.Single(AssertSingleCall(result).ArgumentRelations);
        Assert.Equal("n", relation.Variable.Name);
        Assert.Equal("n", relation.Parameter.Name);
        Assert.Equal(RecursiveArgumentRelationKind.Reducing, relation.Kind);
    }

    [Fact]
    public void Recognized_base_case_is_extracted()
    {
        RecursiveCallAnalysisResult result = Analyze(
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

        BaseCaseEvidence evidence = Assert.Single(result.BaseCaseEvidence);
        Assert.Equal("n", evidence.Parameter.Name);
        Assert.Equal("n", evidence.Variable.Name);
    }

    [Fact]
    public void Missing_base_case_leaves_no_base_case_evidence()
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int M(int n)
                {
                    return M(n - 1);
                }
            }
            """);

        Assert.True(result.HasDirectRecursiveCalls);
        Assert.False(result.HasBaseCaseEvidence);
    }

    [Fact]
    public void Two_sequential_recursive_calls_are_kept_on_the_same_execution_path()
    {
        RecursiveCallAnalysisResult result = Analyze(
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
                    M(n - 2);
                }
            }
            """);

        RecursiveExecutionPath path = Assert.Single(result.ExecutionPaths);
        Assert.Equal(2, path.RecursiveCallCount);
        Assert.Equal(1, path.RecursiveCalls[0].ArgumentRelations.Single().Reduction!.Value);
        Assert.Equal(2, path.RecursiveCalls[1].ArgumentRelations.Single().Reduction!.Value);
    }

    [Fact]
    public void Exclusive_branch_recursive_calls_are_kept_on_separate_execution_paths()
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int M(bool takeLeft, int n)
                {
                    if (n <= 1)
                    {
                        return 1;
                    }

                    if (takeLeft)
                    {
                        return M(false, n - 1);
                    }
                    else
                    {
                        return M(false, n - 2);
                    }
                }
            }
            """);

        Assert.Equal(2, result.ExecutionPaths.Length);
        Assert.All(result.ExecutionPaths, path => Assert.Equal(1, path.RecursiveCallCount));
    }

    [Fact]
    public void Binary_search_shape_has_one_halving_call_per_worst_case_path()
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int Search(bool goLeft, int n)
                {
                    if (n <= 1)
                    {
                        return 0;
                    }

                    var leftHalf = n / 2;
                    var rightHalf = n / 2;
                    if (goLeft)
                    {
                        return Search(false, leftHalf);
                    }
                    else
                    {
                        return Search(false, rightHalf);
                    }
                }
            }
            """,
            methodName: "Search");

        Assert.Equal(2, result.ExecutionPaths.Length);
        Assert.All(result.ExecutionPaths, path =>
        {
            RecursiveArgumentRelation relation = path.RecursiveCalls.Single().ArgumentRelations.Single();

            Assert.Equal(RecursiveArgumentRelationKind.Reducing, relation.Kind);
            Assert.Equal(RecurrenceReductionKind.Scale, relation.Reduction!.Kind);
            Assert.Equal(0.5, relation.Reduction.Value);
        });
    }

    [Fact]
    public void Fibonacci_shape_has_two_sequential_decrement_terms_on_one_path()
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int Fib(int n)
                {
                    if (n <= 1)
                    {
                        return n;
                    }

                    return Fib(n - 1) + Fib(n - 2);
                }
            }
            """,
            methodName: "Fib");

        RecursiveExecutionPath path = Assert.Single(result.ExecutionPaths);
        Assert.Equal(2, path.RecursiveCallCount);
        Assert.Equal(1, path.RecursiveCalls[0].ArgumentRelations.Single().Reduction!.Value);
        Assert.Equal(2, path.RecursiveCalls[1].ArgumentRelations.Single().Reduction!.Value);
    }

    [Fact]
    public void Already_cancelled_token_is_respected_by_recursive_call_analyzer()
    {
        MethodFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                int M(int n) => M(n - 1);
            }
            """,
            "M");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            new RecursiveCallAnalyzer().Analyze(
                facts.MethodDeclaration,
                facts.SemanticModel,
                cancellationTokenSource.Token));
    }

    private static RecursiveArgumentRelation AnalyzeSingleArgumentRelation(
        string parameterType,
        string argument)
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int M(
            """ + parameterType + """
             n)
                {
                    if (n <= 1)
                    {
                        return 1;
                    }

                    return M(
            """ + argument + """
                    );
                }
            }
            """);

        return Assert.Single(AssertSingleCall(result).ArgumentRelations);
    }

    private static RecursiveCallShape AssertSingleCall(RecursiveCallAnalysisResult result)
    {
        Assert.True(result.IsSupported);
        RecursiveExecutionPath path = Assert.Single(result.ExecutionPaths);
        return Assert.Single(path.RecursiveCalls);
    }

    private static RecursiveCallAnalysisResult Analyze(
        string source,
        string methodName = "M",
        Func<IMethodSymbol, bool>? methodPredicate = null)
    {
        MethodFacts facts = CreateFacts(source, methodName, methodPredicate);

        return new RecursiveCallAnalyzer().Analyze(
            facts.MethodDeclaration,
            facts.SemanticModel,
            CancellationToken.None);
    }

    private static MethodFacts CreateFacts(
        string source,
        string methodName,
        Func<IMethodSymbol, bool>? methodPredicate = null)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "RecursiveCallAnalyzerTests",
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
            .Where(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName))
            .Select(method => new
            {
                Declaration = method,
                Symbol = semanticModel.GetDeclaredSymbol(method, CancellationToken.None)
            })
            .Where(method => method.Symbol is not null)
            .Single(method => methodPredicate is null || methodPredicate(method.Symbol!))
            .Declaration;

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

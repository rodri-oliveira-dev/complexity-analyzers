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

    [Fact]
    public void Method_without_direct_recursive_call_is_not_split_into_execution_paths()
    {
        MethodFacts facts = CreateFacts(
            CreateSequentialBranchSource(branchCount: 64, recursiveTail: false),
            "M");

        RecursiveCallAnalysisResult result = new RecursiveCallAnalyzer().Analyze(
            facts.MethodDeclaration,
            facts.SemanticModel,
            CancellationToken.None);

        Assert.True(result.IsSupported);
        Assert.False(result.HasDirectRecursiveCalls);
        Assert.Empty(result.ExecutionPaths);
    }

    [Fact]
    public void Recursive_method_with_execution_paths_at_the_limit_is_supported()
    {
        int branchCount = (int)Math.Log(RecursiveCallAnalyzer.MaximumExecutionPaths, 2);

        RecursiveCallAnalysisResult result = Analyze(
            CreateSequentialBranchSource(branchCount, recursiveTail: true));

        Assert.True(result.IsSupported);
        Assert.Equal(RecursiveCallAnalyzer.MaximumExecutionPaths, result.ExecutionPaths.Length);
    }

    [Fact]
    public void Recursive_method_with_more_execution_paths_than_the_limit_is_unsupported()
    {
        int branchCount = (int)Math.Log(RecursiveCallAnalyzer.MaximumExecutionPaths, 2) + 1;

        RecursiveCallAnalysisResult result = Analyze(
            CreateSequentialBranchSource(branchCount, recursiveTail: true));

        Assert.False(result.IsSupported);
        Assert.Contains("execution paths", result.UnsupportedReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Recursive_method_with_one_execution_path_below_the_limit_is_supported()
    {
        // 1 terminated path + 2 * 3 * 3 * 7 = 126 active paths = 127 paths.
        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            ActivePathThenRecursiveReturn,
            AlternativeChain(2),
            AlternativeChain(3),
            AlternativeChain(3),
            AlternativeChain(7)));

        Assert.True(result.IsSupported);
        Assert.Equal(127, result.ExecutionPaths.Length);
        Assert.Equal(1, CountPathsWithSingleReduction(result, 2));
        Assert.Equal(126, CountPathsWithSingleReduction(result, 1));
    }

    [Fact]
    public void Recursive_method_with_terminated_paths_appended_at_the_limit_is_supported()
    {
        // 2 terminated paths + 2 * 3 * 3 * 7 = 126 active paths = 128 paths. The terminated paths are
        // appended after the active paths, so the last append reaches the limit exactly.
        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            ActivePathThenRecursiveReturn,
            ActivePathThenRecursiveReturn,
            AlternativeChain(2),
            AlternativeChain(3),
            AlternativeChain(3),
            AlternativeChain(7)));

        Assert.True(result.IsSupported);
        Assert.Equal(RecursiveCallAnalyzer.MaximumExecutionPaths, result.ExecutionPaths.Length);
        Assert.Equal(2, CountPathsWithSingleReduction(result, 2));
    }

    [Fact]
    public void Recursive_method_with_one_execution_path_above_the_limit_is_unsupported()
    {
        // 1 terminated path first + 2^7 = 128 active paths = 129 paths.
        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            [RecursiveReturnThenActivePath, .. SequentialBranches(7)]));

        AssertTooManyExecutionPaths(result);
    }

    [Fact]
    public void Terminated_path_appended_after_the_expanding_paths_counts_toward_the_limit()
    {
        // 2^7 = 128 active paths first + 1 terminated path last = 129 paths. The terminated path is
        // carried after the last expansion of each statement.
        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            [ActivePathThenRecursiveReturn, .. SequentialBranches(7)]));

        AssertTooManyExecutionPaths(result);
    }

    [Fact]
    public void Nested_conditionals_within_the_limit_keep_every_execution_path()
    {
        // (2^3 + 2^2 + 2^1) = 14 nested paths, then 2^3 sequential paths = 112 paths.
        string nestedConditional =
            $"if (n > 100) {Block($"if (n > 200) {Block(SequentialBranches(3))} else {Block(SequentialBranches(2))}")}"
            + $" else {Block(SequentialBranches(1))}";

        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            [nestedConditional, .. SequentialBranches(3)]));

        Assert.True(result.IsSupported);
        Assert.Equal(112, result.ExecutionPaths.Length);
        Assert.All(result.ExecutionPaths, path => Assert.Equal(1, path.RecursiveCallCount));
    }

    [Fact]
    public void Nested_conditional_branches_that_merge_at_the_limit_are_supported()
    {
        // 2^6 + 2^6 = 128 paths.
        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            $"if (n > 100) {Block(SequentialBranches(6))} else {Block(SequentialBranches(6))}"));

        Assert.True(result.IsSupported);
        Assert.Equal(RecursiveCallAnalyzer.MaximumExecutionPaths, result.ExecutionPaths.Length);
    }

    [Fact]
    public void Nested_conditional_branches_that_merge_above_the_limit_are_unsupported()
    {
        // 2^6 + (2^6 active + 1 terminated) = 129 paths.
        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            $"if (n > 100) {Block(SequentialBranches(6))}"
            + $" else {Block([ActivePathThenRecursiveReturn, .. SequentialBranches(6)])}"));

        AssertTooManyExecutionPaths(result);
    }

    [Fact]
    public void Nested_conditional_branches_far_above_the_limit_are_unsupported()
    {
        // 2^7 + 2^7 = 256 paths.
        RecursiveCallAnalysisResult result = Analyze(CreateRecursiveMethodSource(
            $"if (n > 100) {Block(SequentialBranches(7))} else {Block(SequentialBranches(7))}"));

        AssertTooManyExecutionPaths(result);
    }

    [Fact]
    public void Mixed_terminating_and_continuing_paths_keep_their_recursive_calls_in_order()
    {
        RecursiveCallAnalysisResult result = Analyze(
            """
            public sealed class Sample
            {
                int M(int n)
                {
                    if (n <= 0)
                    {
                        return 0;
                    }

                    if (n > 10)
                    {
                        M(n - 3);
                    }

                    if (n == 5)
                    {
                        return M(n - 2);
                    }

                    return M(n - 1);
                }
            }
            """);

        // 5 paths: the base case path has no recursive call and is not an execution path.
        Assert.True(result.IsSupported);
        Assert.True(result.HasBaseCaseEvidence);
        string[] expectedReductions = ["3,2", "3,1", "2", "1"];
        Assert.Equal(
            expectedReductions,
            result.ExecutionPaths.Select(path => string.Join(
                ",",
                path.RecursiveCalls.Select(call => call.ArgumentRelations.Single().Reduction!.Value))));
    }

    private const string ActivePathThenRecursiveReturn =
        "if (n > 0) { total++; } else { return M(n - 2); }";

    private const string RecursiveReturnThenActivePath =
        "if (n <= 0) { return M(n - 2); }";

    private static string TooManyExecutionPathsReason
    {
        get;
    } = $"The method has more than {RecursiveCallAnalyzer.MaximumExecutionPaths} execution paths.";

    private static void AssertTooManyExecutionPaths(RecursiveCallAnalysisResult result)
    {
        Assert.False(result.IsSupported);
        Assert.Equal(TooManyExecutionPathsReason, result.UnsupportedReason);
        Assert.Empty(result.ExecutionPaths);
    }

    private static int CountPathsWithSingleReduction(RecursiveCallAnalysisResult result, double reduction)
    {
        return result.ExecutionPaths.Count(path =>
            path.RecursiveCallCount == 1
            && path.RecursiveCalls[0].ArgumentRelations.Single().Reduction!.Value == reduction);
    }

    private static string[] SequentialBranches(int branchCount)
    {
        return
        [
            .. Enumerable.Range(0, branchCount)
                .Select(index => $"if (n > {index}) {{ total++; }}")
        ];
    }

    private static string AlternativeChain(int alternativeCount)
    {
        string conditions = string.Join(
            " else ",
            Enumerable.Range(0, alternativeCount - 1)
                .Select(index => $"if (n == {index}) {{ total += {index}; }}"));

        return $"{conditions} else {{ total += {alternativeCount - 1}; }}";
    }

    private static string Block(params string[] statements)
    {
        return "{ " + string.Join(" ", statements) + " }";
    }

    private static string CreateRecursiveMethodSource(params string[] statements)
    {
        string body = string.Join(
            Environment.NewLine,
            statements.Select(statement => "        " + statement));

        return $$"""
            public sealed class Sample
            {
                int M(int n)
                {
                    int total = 0;
            {{body}}
                    return M(n - 1) + total;
                }
            }
            """;
    }

    private static string CreateSequentialBranchSource(int branchCount, bool recursiveTail)
    {
        string branches = string.Join(
            Environment.NewLine,
            SequentialBranches(branchCount).Select(branch => "        " + branch));
        string tail = recursiveTail ? "return M(n - 1) + total;" : "return total;";

        return $$"""
            public sealed class Sample
            {
                int M(int n)
                {
                    int total = 0;
            {{branches}}
                    {{tail}}
                }
            }
            """;
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

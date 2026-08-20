using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class PhaseSixRecurrenceContractTests
{
    [Fact]
    public void Characterization_matrix_solves_supported_direct_recurrences()
    {
        CharacterizationCase[] cases =
        [
            new(
                "T(n)=T(n-1)+1",
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
                    }
                }
                """,
                "M",
                "O(n)"),
            new(
                "T(n)=T(n-1)+n",
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
                """,
                "M",
                "O(n\u00b2)"),
            new(
                "T(n)=T(n-1)+log n",
                """
                public sealed class Sample
                {
                    void M(int n)
                    {
                        if (n <= 1)
                        {
                            return;
                        }

                        for (var i = 1; i < n; i *= 2)
                        {
                            var value = i + 1;
                        }

                        M(n - 1);
                    }
                }
                """,
                "M",
                "O(n log n)"),
            new(
                "T(n)=2T(n-1)+1",
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
                """,
                "M",
                "O(2^n)"),
            new(
                "Fibonacci",
                """
                public sealed class Sample
                {
                    int Fibonacci(int n)
                    {
                        if (n <= 1)
                        {
                            return n;
                        }

                        return Fibonacci(n - 1) + Fibonacci(n - 2);
                    }
                }
                """,
                "Fibonacci",
                "O(1.618^n)"),
            new(
                "T(n)=T(n/2)+1",
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
                    }
                }
                """,
                "M",
                "O(log n)"),
            new(
                "T(n)=2T(n/2)+1",
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
                    }
                }
                """,
                "M",
                "O(n)"),
            new(
                "T(n)=2T(n/2)+n",
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
                """,
                "M",
                "O(n log n)"),
            new(
                "T(n)=2T(n/2)+n^2",
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
                            for (var j = 0; j < n; j++)
                            {
                                var value = i + j;
                            }
                        }
                    }
                }
                """,
                "M",
                "O(n\u00b2)"),
            new(
                "T(n)=3T(n/2)+n",
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
                        M(n / 2);

                        for (var i = 0; i < n; i++)
                        {
                            var value = i + 1;
                        }
                    }
                }
                """,
                "M",
                "O(n^1.585)"),
            new(
                "T(n)=T(n/3)+T(2n/3)+n",
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
                """,
                "M",
                "O(n log n)")
        ];

        foreach (CharacterizationCase testCase in cases)
        {
            ComplexityExpression complexity = AnalyzeMethod(testCase.Source, testCase.MethodName, out _);

            Assert.Equal(testCase.ExpectedComplexity, complexity.ToBigONotation());
        }
    }

    [Fact]
    public void Binary_search_branch_regression_keeps_exclusive_calls_path_sensitive()
    {
        ComplexityExpression complexity = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int BinarySearch(int n, bool takeLeft)
                {
                    if (n <= 1)
                    {
                        return -1;
                    }

                    if (takeLeft)
                    {
                        return BinarySearch(n / 2, false);
                    }
                    else
                    {
                        return BinarySearch(n / 2, false);
                    }
                }
            }
            """,
            "BinarySearch",
            out _);

        Assert.Equal("O(log n)", complexity.ToBigONotation());
        Assert.NotEqual("O(n)", complexity.ToBigONotation());
    }

    [Fact]
    public void Unknown_matrix_keeps_unsupported_or_unsafe_recursion_conservative()
    {
        UnknownCase[] cases =
        [
            new(
                "missing base case",
                """
                public sealed class Sample
                {
                    void M(int n)
                    {
                        M(n - 1);
                    }
                }
                """,
                "M"),
            new(
                "non-reducing argument",
                """
                public sealed class Sample
                {
                    void M(int n)
                    {
                        if (n <= 1)
                        {
                            return;
                        }

                        M(n);
                    }
                }
                """,
                "M"),
            new(
                "recursive n+1",
                """
                public sealed class Sample
                {
                    void M(int n)
                    {
                        if (n <= 1)
                        {
                            return;
                        }

                        M(n + 1);
                    }
                }
                """,
                "M"),
            new(
                "unknown local work",
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
                """,
                "M"),
            new(
                "unsupported characteristic recurrence",
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
                        M(n - 3);
                    }
                }
                """,
                "M"),
            new(
                "mutual recursion",
                """
                public sealed class Sample
                {
                    void A(int n)
                    {
                        if (n <= 1)
                        {
                            return;
                        }

                        B(n - 1);
                    }

                    void B(int n)
                    {
                        A(n - 1);
                    }
                }
                """,
                "A")
        ];

        foreach (UnknownCase testCase in cases)
        {
            ComplexityExpression complexity = AnalyzeMethod(testCase.Source, testCase.MethodName, out _);

            Assert.Equal("Unknown", complexity.ToBigONotation());
        }
    }

    [Fact]
    public void Unsupported_and_numerically_inconclusive_solver_results_do_not_become_known_complexity()
    {
        RecurrenceSolution unsupportedAkraBazzi = new RestrictedAkraBazziRecurrenceSolver().Solve(Relation(
            ComplexityFactory.Constant(),
            Scale(1, 0.5),
            new RecurrenceTerm(1, RecurrenceReduction.SubtractConstant(1))));
        RecurrenceSolution numericallyInconclusive = new RecurrenceSolver().Solve(Relation(
            ComplexityFactory.Constant(),
            Scale(1, 0.99999999),
            Scale(1, 0.99999998)));

        Assert.Equal(RecurrenceSolutionKind.Unsupported, unsupportedAkraBazzi.Kind);
        Assert.Null(unsupportedAkraBazzi.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, unsupportedAkraBazzi.SolverKind);

        Assert.Equal(RecurrenceSolutionKind.NumericallyInconclusive, numericallyInconclusive.Kind);
        Assert.Null(numericallyInconclusive.Complexity);
        Assert.Equal(RecurrenceSolverKind.None, numericallyInconclusive.SolverKind);
    }

    [Fact]
    public void Solved_recursive_source_method_is_cached_once_and_substituted_per_caller_argument()
    {
        ComplexityExpression complexity = AnalyzeMethod(
            """
            public sealed class Sample
            {
                void M(int left, int right)
                {
                    CountDown(left);
                    CountDown(right);
                }

                void CountDown(int value)
                {
                    if (value <= 1)
                    {
                        return;
                    }

                    CountDown(value - 1);
                }
            }
            """,
            "M",
            out InterproceduralAnalysisContext context);

        Assert.Equal("O(n + m)", complexity.ToBigONotation());
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public void Abandoned_cache_reservation_can_be_reused_without_poisoning_future_analysis()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """);
        IMethodSymbol method = GetMethod(facts, "M");
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        Assert.True(context.TemplateCache.TryReserveAnalysis(
            method,
            CancellationToken.None,
            out InterproceduralAnalysisResult? completed));
        Assert.Null(completed);
        Assert.False(context.TemplateCache.TryReserveAnalysis(method, CancellationToken.None, out completed));
        Assert.Null(completed);

        Assert.True(context.TemplateCache.AbandonAnalysis(method, CancellationToken.None));
        Assert.Equal(0, context.TemplateCache.Count);
        Assert.True(context.TemplateCache.TryReserveAnalysis(method, CancellationToken.None, out completed));
        Assert.Null(completed);
    }

    private static ComplexityExpression AnalyzeMethod(
        string source,
        string methodName,
        out InterproceduralAnalysisContext interproceduralContext)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        MethodDeclarationSyntax methodDeclaration = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName));
        SemanticModel semanticModel = facts.Compilation.GetSemanticModel(methodDeclaration.SyntaxTree);
        interproceduralContext = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        return MethodComplexityExtractor.AnalyzeMethod(
            methodDeclaration,
            semanticModel,
            interproceduralContext,
            CancellationToken.None);
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

    private static RecurrenceTerm Scale(int multiplicity, double scale)
    {
        return new RecurrenceTerm(multiplicity, RecurrenceReduction.Scale(scale));
    }

    private static IMethodSymbol GetMethod(CompilationFacts facts, string name)
    {
        return facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(method => facts.SemanticModel.GetDeclaredSymbol(method, CancellationToken.None))
            .Where(method => method is not null)
            .Cast<IMethodSymbol>()
            .Single(method => StringComparer.Ordinal.Equals(method.Name, name));
    }

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "PhaseSixRecurrenceContractTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        return new CompilationFacts(
            compilation,
            syntaxTree,
            compilation.GetSemanticModel(syntaxTree));
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

    private sealed record CharacterizationCase(
        string Scenario,
        string Source,
        string MethodName,
        string ExpectedComplexity);

    private sealed record UnknownCase(
        string Scenario,
        string Source,
        string MethodName);

    private sealed record CompilationFacts(
        CSharpCompilation Compilation,
        SyntaxTree SyntaxTree,
        SemanticModel SemanticModel);
}

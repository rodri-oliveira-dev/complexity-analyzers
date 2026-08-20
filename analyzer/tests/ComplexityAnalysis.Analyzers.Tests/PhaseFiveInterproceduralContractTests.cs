using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class PhaseFiveInterproceduralContractTests
{
    [Fact]
    public void Characterization_matrix_covers_supported_source_call_shapes()
    {
        CharacterizationCase[] cases =
        [
            new(
                "A -> B O(n) propagates to caller",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] items)
                        {
                            B(items);
                        }

                        private void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(n)",
                1),
            new(
                "A -> B -> C O(log n) propagates through chain",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int count)
                        {
                            B(count);
                        }

                        private void B(int size)
                        {
                            C(size);
                        }

                        private void C(int size)
                        {
                            for (var i = 1; i < size; i *= 2)
                            {
                                var x = i + 1;
                            }
                        }
                    }
                    """
                ],
                "O(log n)",
                2),
            new(
                "callee in another syntax tree uses callee semantic model",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] items)
                        {
                            Helpers.B(items);
                        }
                    }
                    """,
                    """
                    public static class Helpers
                    {
                        public static void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(n)",
                1),
            new(
                "same callee multiple call sites reuse one caller-independent template",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] items)
                        {
                            B(items);
                            B(items);
                        }

                        private void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(n)",
                1),
            new(
                "same callee with different bindings substitutes per call site",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] left, int[] right)
                        {
                            B(left);
                            B(right);
                        }

                        private void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(n + m)",
                1),
            new(
                "n -> n substitution preserves primary input",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] items)
                        {
                            B(items);
                        }

                        private void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(n)",
                1),
            new(
                "n -> m substitution preserves secondary input",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] left, int[] right)
                        {
                            B(right);
                        }

                        private void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(m)",
                1),
            new(
                "n,m substitutions preserve independent product",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] left, int[] right)
                        {
                            Compare(left, right);
                        }

                        private void Compare(int[] first, int[] second)
                        {
                            foreach (var outer in first)
                            {
                                foreach (var inner in second)
                                {
                                    var x = outer + inner;
                                }
                            }
                        }
                    }
                    """
                ],
                "O(n \u00b7 m)",
                1),
            new(
                "constant argument reduces source callee cost",
                [
                    """
                    public sealed class Sample
                    {
                        void M()
                        {
                            B(10);
                        }

                        private void B(int count)
                        {
                            for (var i = 0; i < count; i++)
                            {
                                var x = i + 1;
                            }
                        }
                    }
                    """
                ],
                "O(1)",
                1),
            new(
                "source callee inside same-input loop composes to quadratic",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] items)
                        {
                            foreach (var item in items)
                            {
                                B(items);
                            }
                        }

                        private void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(n\u00b2)",
                1),
            new(
                "independent source callee inside loop composes to product",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] left, int[] right)
                        {
                            foreach (var item in left)
                            {
                                B(right);
                            }
                        }

                        private void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }
                    """
                ],
                "O(n \u00b7 m)",
                1)
        ];

        foreach (CharacterizationCase testCase in cases)
        {
            ComplexityExpression result = AnalyzeMethod(
                testCase.Sources,
                "M",
                AnalysisBudget.Default,
                out InterproceduralAnalysisContext context);

            Assert.Equal(testCase.ExpectedComplexity, result.ToBigONotation());
            Assert.Equal(testCase.ExpectedCacheCount, context.TemplateCache.Count);
        }
    }

    [Fact]
    public void Precedence_and_unknown_matrix_covers_conservative_boundaries()
    {
        CharacterizationCase[] cases =
        [
            new(
                "BCL precedence keeps List<T>.Contains out of source cache",
                [
                    """
                    using System.Collections.Generic;

                    public sealed class Sample
                    {
                        bool M(List<int> values) => values.Contains(42);
                    }
                    """
                ],
                "O(n)",
                0),
            new(
                "LINQ precedence keeps Enumerable.Count out of source cache",
                [
                    """
                    using System.Collections.Generic;
                    using System.Linq;

                    public sealed class Sample
                    {
                        int M(IEnumerable<int> values) => values.Count();
                    }
                    """
                ],
                "O(n)",
                0),
            new(
                "external unknown remains Unknown",
                [
                    """
                    public sealed class Sample
                    {
                        void M()
                        {
                            System.Console.WriteLine("value");
                        }
                    }
                    """
                ],
                "Unknown",
                0),
            new(
                "unsafe virtual dispatch remains Unknown",
                [
                    """
                    public class Worker
                    {
                        public virtual void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }

                    public sealed class Sample
                    {
                        void M(Worker worker, int[] items)
                        {
                            worker.B(items);
                        }
                    }
                    """
                ],
                "Unknown",
                0),
            new(
                "unsafe interface dispatch remains Unknown",
                [
                    """
                    public interface IWorker
                    {
                        void B(int[] values);
                    }

                    public sealed class Worker : IWorker
                    {
                        public void B(int[] values)
                        {
                            foreach (var value in values)
                            {
                                var x = value + 1;
                            }
                        }
                    }

                    public sealed class Sample
                    {
                        void M(IWorker worker, int[] items)
                        {
                            worker.B(items);
                        }
                    }
                    """
                ],
                "Unknown",
                0),
            new(
                "direct recursion is detected but unresolved",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] items)
                        {
                            M(items);
                        }
                    }
                    """
                ],
                "Unknown",
                0),
            new(
                "mutual cycle is detected but unresolved",
                [
                    """
                    public sealed class Sample
                    {
                        void M(int[] items)
                        {
                            A(items);
                        }

                        private void A(int[] values)
                        {
                            B(values);
                        }

                        private void B(int[] values)
                        {
                            A(values);
                        }
                    }
                    """
                ],
                "Unknown",
                0)
        ];

        foreach (CharacterizationCase testCase in cases)
        {
            ComplexityExpression result = AnalyzeMethod(
                testCase.Sources,
                "M",
                AnalysisBudget.Default,
                out InterproceduralAnalysisContext context);

            Assert.Equal(testCase.ExpectedComplexity, result.ToBigONotation());
            Assert.Equal(testCase.ExpectedCacheCount, context.TemplateCache.Count);
        }
    }

    [Fact]
    public void Cache_reuse_prevents_repeated_callee_expansion_under_method_budget()
    {
        ComplexityExpression result = AnalyzeMethod(
            [
                """
                public sealed class Sample
                {
                    void M(int[] items)
                    {
                        B(items);
                        B(items);
                        B(items);
                    }

                    private void B(int[] values)
                    {
                        foreach (var value in values)
                        {
                            var x = value + 1;
                        }
                    }
                }
                """
            ],
            "M",
            new AnalysisBudget(maximumCallDepth: 5, maximumMethodsPerRootAnalysis: 1),
            out InterproceduralAnalysisContext context);

        Assert.Equal("O(n)", result.ToBigONotation());
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public void Demand_driven_traversal_does_not_visit_unreachable_source_methods()
    {
        string[] sources =
        [
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    B(items);
                }

                private void B(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            .. Enumerable.Range(0, 64).Select(index =>
                $$"""
                public sealed class Unreachable{{index}}
                {
                    void Bomb()
                    {
                        System.Console.WriteLine("unreachable");
                    }
                }
                """)
        ];

        ComplexityExpression result = AnalyzeMethod(
            sources,
            "M",
            AnalysisBudget.Default,
            out InterproceduralAnalysisContext context);

        Assert.Equal("O(n)", result.ToBigONotation());
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public void Method_budget_limits_adversarial_call_fanout()
    {
        ComplexityExpression result = AnalyzeMethod(
            [
                """
                public sealed class Sample
                {
                    void M(int[] items)
                    {
                        A(items);
                        B(items);
                    }

                    private void A(int[] values)
                    {
                    }

                    private void B(int[] values)
                    {
                        foreach (var value in values)
                        {
                            var x = value + 1;
                        }
                    }
                }
                """
            ],
            "M",
            new AnalysisBudget(maximumCallDepth: 5, maximumMethodsPerRootAnalysis: 1),
            out InterproceduralAnalysisContext context);

        Assert.Equal("Unknown", result.ToBigONotation());
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public void Default_method_budget_allows_boundary_and_stops_boundary_plus_one()
    {
        ComplexityExpression boundary = AnalyzeMethod(
            [CreateFanoutSource(callCount: 32)],
            "M",
            AnalysisBudget.Default,
            out InterproceduralAnalysisContext boundaryContext);
        ComplexityExpression boundaryPlusOne = AnalyzeMethod(
            [CreateFanoutSource(callCount: 33)],
            "M",
            AnalysisBudget.Default,
            out InterproceduralAnalysisContext boundaryPlusOneContext);

        Assert.Equal("O(1)", boundary.ToBigONotation());
        Assert.Equal(32, boundaryContext.TemplateCache.Count);
        Assert.Equal("Unknown", boundaryPlusOne.ToBigONotation());
        Assert.Equal(32, boundaryPlusOneContext.TemplateCache.Count);
    }

    [Fact]
    public void Depth_budget_limits_adversarial_call_chain()
    {
        ComplexityExpression result = AnalyzeMethod(
            [
                """
                public sealed class Sample
                {
                    void M(int[] items)
                    {
                        A(items);
                    }

                    private void A(int[] values) => B(values);
                    private void B(int[] values) => C(values);

                    private void C(int[] values)
                    {
                        foreach (var value in values)
                        {
                            var x = value + 1;
                        }
                    }
                }
                """
            ],
            "M",
            new AnalysisBudget(maximumCallDepth: 2, maximumMethodsPerRootAnalysis: 32),
            out InterproceduralAnalysisContext context);

        Assert.Equal("Unknown", result.ToBigONotation());
        Assert.Equal(0, context.TemplateCache.Count);
    }

    [Fact]
    public void Moderate_compilation_analysis_is_not_forced_to_cache_unreachable_methods()
    {
        string[] sources =
        [
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    Helper.Visit(items);
                }
            }
            """,
            """
            public static class Helper
            {
                public static void Visit(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            .. Enumerable.Range(0, 128).Select(index =>
                $$"""
                public static class UnreachableHelper{{index}}
                {
                    public static void Visit(int[] values)
                    {
                        foreach (var value in values)
                        {
                            System.Console.WriteLine(value);
                        }
                    }
                }
                """)
        ];

        ComplexityExpression result = AnalyzeMethod(
            sources,
            "M",
            AnalysisBudget.Default,
            out InterproceduralAnalysisContext context);

        Assert.Equal("O(n)", result.ToBigONotation());
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public async Task Cycles_terminate_without_cache_deadlock()
    {
        Task<string> analysis = Task.Run(() => AnalyzeMethod(
            [
                """
                public sealed class Sample
                {
                    void M(int[] items)
                    {
                        A(items);
                    }

                    private void A(int[] values)
                    {
                        B(values);
                    }

                    private void B(int[] values)
                    {
                        A(values);
                    }
                }
                """
            ],
            "M",
            AnalysisBudget.Default,
            out _).ToBigONotation());

        Task completed = await Task.WhenAny(analysis, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(analysis, completed);
        Assert.Equal("Unknown", await analysis);
    }

    private static string CreateFanoutSource(int callCount)
    {
        string calls = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, callCount).Select(index => "        Helper" + index + "(items);"));
        string helpers = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, callCount).Select(index => "    private void Helper" + index + "(int[] values) { }"));

        return
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
            """
            + Environment.NewLine
            + calls
            + Environment.NewLine
            + """
                }

            """
            + helpers
            + Environment.NewLine
            + """
            }
            """;
    }

    private static ComplexityExpression AnalyzeMethod(
        string[] sources,
        string methodName,
        AnalysisBudget budget,
        out InterproceduralAnalysisContext interproceduralContext)
    {
        CompilationFacts facts = CreateCompilationFacts(sources);
        MethodDeclarationSyntax methodDeclaration = facts.SyntaxTrees
            .SelectMany(syntaxTree => syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName));
        SemanticModel semanticModel = facts.Compilation.GetSemanticModel(methodDeclaration.SyntaxTree);
        interproceduralContext = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            budget,
            CancellationToken.None);

        return MethodComplexityExtractor.AnalyzeMethod(
            methodDeclaration,
            semanticModel,
            interproceduralContext,
            CancellationToken.None);
    }

    private static CompilationFacts CreateCompilationFacts(string[] sources)
    {
        SyntaxTree[] syntaxTrees =
        [
            .. sources.Select(source => CSharpSyntaxTree.ParseText(source))
        ];
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "PhaseFiveInterproceduralContractTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        return new CompilationFacts(compilation, [.. syntaxTrees]);
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
        string[] Sources,
        string ExpectedComplexity,
        int ExpectedCacheCount);

    private sealed record CompilationFacts(
        CSharpCompilation Compilation,
        ImmutableArray<SyntaxTree> SyntaxTrees);
}

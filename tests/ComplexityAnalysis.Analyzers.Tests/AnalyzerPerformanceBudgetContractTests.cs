using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class AnalyzerPerformanceBudgetContractTests
{
    [Fact]
    public void Budget_defaults_and_hard_maximums_match_the_implementation_contract()
    {
        Assert.Equal(5, AnalysisBudget.DefaultMaximumCallDepth);
        Assert.Equal(32, AnalysisBudget.DefaultMaximumMethodsPerRootAnalysis);
        Assert.Equal(AnalysisBudget.DefaultMaximumCallDepth, ComplexityAnalyzerOptions.DefaultMaxCallDepth);
        Assert.Equal(AnalysisBudget.DefaultMaximumMethodsPerRootAnalysis, ComplexityAnalyzerOptions.DefaultMaxMethodsPerRoot);
        Assert.Equal(16, ComplexityAnalyzerOptions.MaximumMaxCallDepth);
        Assert.Equal(128, ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot);

        SyntaxTree syntaxTree = Parse("public sealed class Sample { void M() { } }");
        ComplexityAnalyzerOptions maximumOptions = ComplexityAnalyzerOptionsReader.Read(
            new TestAnalyzerConfigOptionsProvider(
                Options(
                    (ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "16"),
                    (ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "128"))),
            syntaxTree);
        ComplexityAnalyzerOptions invalidOptions = ComplexityAnalyzerOptionsReader.Read(
            new TestAnalyzerConfigOptionsProvider(
                Options(
                    (ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "17"),
                    (ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "129"))),
            syntaxTree);

        Assert.Equal(16, maximumOptions.MaxCallDepth);
        Assert.Equal(128, maximumOptions.MaxMethodsPerRoot);
        Assert.Equal(ComplexityAnalyzerOptions.DefaultMaxCallDepth, invalidOptions.MaxCallDepth);
        Assert.Equal(ComplexityAnalyzerOptions.DefaultMaxMethodsPerRoot, invalidOptions.MaxMethodsPerRoot);
    }

    [Fact]
    public void Hard_maximum_method_budget_allows_boundary_and_stops_boundary_plus_one()
    {
        ComplexityAnalyzerOptions hardMaximumOptions = new(
            interproceduralAnalysisEnabled: true,
            recursionAnalysisEnabled: true,
            maxCallDepth: ComplexityAnalyzerOptions.MaximumMaxCallDepth,
            maxMethodsPerRoot: ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot,
            ComplexityThreshold.None);

        ComplexityExpression boundary = AnalyzeMethod(
            CreateFanoutSource(ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot),
            "Root",
            hardMaximumOptions,
            CancellationToken.None,
            out InterproceduralAnalysisContext boundaryContext);
        ComplexityExpression boundaryPlusOne = AnalyzeMethod(
            CreateFanoutSource(ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot + 1),
            "Root",
            hardMaximumOptions,
            CancellationToken.None,
            out InterproceduralAnalysisContext boundaryPlusOneContext);

        Assert.Equal("O(1)", boundary.ToBigONotation());
        Assert.Equal(ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot, boundaryContext.TemplateCache.Count);
        _ = Assert.IsType<UnknownComplexity>(boundaryPlusOne);
        Assert.Equal(ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot, boundaryPlusOneContext.TemplateCache.Count);
    }

    [Fact]
    public void Repeated_callee_calls_reuse_one_template_even_with_method_budget_one()
    {
        ComplexityAnalyzerOptions budgetOneOptions = new(
            interproceduralAnalysisEnabled: true,
            recursionAnalysisEnabled: true,
            maxCallDepth: ComplexityAnalyzerOptions.MaximumMaxCallDepth,
            maxMethodsPerRoot: 1,
            ComplexityThreshold.None);
        string repeatedCalls = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 96).Select(_ => "        Shared(values);"));

        ComplexityExpression complexity = AnalyzeMethod(
            """
            public sealed class Sample
            {
                void Root(int[] values)
                {
            """
            + Environment.NewLine
            + repeatedCalls
            + Environment.NewLine
            + """
                }

                private void Shared(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "Root",
            budgetOneOptions,
            CancellationToken.None,
            out InterproceduralAnalysisContext context);

        Assert.Equal("O(n)", complexity.ToBigONotation());
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public void Hard_maximum_call_depth_stops_boundary_plus_one_chain()
    {
        ComplexityAnalyzerOptions hardMaximumOptions = new(
            interproceduralAnalysisEnabled: true,
            recursionAnalysisEnabled: true,
            maxCallDepth: ComplexityAnalyzerOptions.MaximumMaxCallDepth,
            maxMethodsPerRoot: ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot,
            ComplexityThreshold.None);

        ComplexityExpression complexity = AnalyzeMethod(
            CreateCallChainSource(ComplexityAnalyzerOptions.MaximumMaxCallDepth + 1),
            "Root",
            hardMaximumOptions,
            CancellationToken.None,
            out InterproceduralAnalysisContext context);

        _ = Assert.IsType<UnknownComplexity>(complexity);
        Assert.True(
            context.TemplateCache.Count <= ComplexityAnalyzerOptions.MaximumMaxCallDepth,
            "Depth budget exhaustion should not expand more completed templates than the configured hard maximum.");
    }

    [Fact]
    public void Zero_budgets_conservatively_disable_source_expansion()
    {
        ComplexityAnalyzerOptions zeroBudgetOptions = new(
            interproceduralAnalysisEnabled: true,
            recursionAnalysisEnabled: true,
            maxCallDepth: 0,
            maxMethodsPerRoot: 0,
            ComplexityThreshold.None);

        ComplexityExpression complexity = AnalyzeMethod(
            """
            public sealed class Sample
            {
                void Root(int[] values)
                {
                    Shared(values);
                }

                private void Shared(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "Root",
            zeroBudgetOptions,
            CancellationToken.None,
            out InterproceduralAnalysisContext context);

        _ = Assert.IsType<UnknownComplexity>(complexity);
        Assert.Equal(0, context.TemplateCache.Count);
    }

    [Fact]
    public void Akra_bazzi_numerical_solver_returns_inconclusive_when_iteration_budget_is_exhausted()
    {
        RecurrenceRelation relation = new(
            ComplexityVariable.N,
            ImmutableArray.Create(
                new RecurrenceTerm(1, RecurrenceReduction.Scale(1.0 / 3.0)),
                new RecurrenceTerm(1, RecurrenceReduction.Scale(1.0 / 2.0))),
            ComplexityFactory.Constant());
        RestrictedAkraBazziRecurrenceSolver exhaustedSolver = new(
            maxBracketExpansions: RecurrenceNumerics.AkraBazziMaxBracketExpansions,
            maxBisectionIterations: 0,
            maxHighExponent: RecurrenceNumerics.AkraBazziMaxHighExponent);

        RecurrenceSolution exhausted = exhaustedSolver.Solve(relation);
        RecurrenceSolution solved = new RestrictedAkraBazziRecurrenceSolver().Solve(relation);

        Assert.Equal(RecurrenceSolutionKind.NumericallyInconclusive, exhausted.Kind);
        Assert.Null(exhausted.Complexity);
        Assert.Equal(RecurrenceSolutionKind.Solved, solved.Kind);
        Assert.Equal(RecurrenceSolverKind.RestrictedAkraBazzi, solved.SolverKind);
        Assert.NotNull(solved.Complexity);
    }

    [Fact]
    public void Already_canceled_interprocedural_analysis_does_not_grow_caches()
    {
        CompilationFacts facts = CreateCompilationFacts(CreateFanoutSource(callCount: 8));
        MethodDeclarationSyntax method = GetMethodDeclaration(facts, "Root");
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            MethodComplexityExtractor.AnalyzeMethod(
                method,
                facts.SemanticModel,
                context,
                ComplexityAnalyzerOptions.Default,
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, context.TemplateCache.Count);
    }

    [Fact]
    public void Already_canceled_recurrence_analysis_does_not_store_direct_recurrence_solution()
    {
        CompilationFacts facts = CreateCompilationFacts(
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
            """);
        MethodDeclarationSyntax method = GetMethodDeclaration(facts, "Fibonacci");
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            MethodComplexityExtractor.AnalyzeMethod(
                method,
                facts.SemanticModel,
                context,
                ComplexityAnalyzerOptions.Default,
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, context.DirectRecurrenceCacheCount);
    }

    [Fact]
    public void Production_analyzer_source_remains_free_of_forbidden_hot_path_api_patterns()
    {
        string sourceRoot = Path.Combine(FindRepositoryRoot(), "src", "ComplexityAnalysis.Analyzers");
        string[] productionFiles = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string[] forbiddenPatterns =
        [
            "System.IO.",
            "System.Net.",
            "HttpClient",
            "WebRequest",
            "Process.Start",
            "File.",
            "Directory.",
            "Telemetry",
            "EventSource",
            "Trace."
        ];

        Assert.NotEmpty(productionFiles);
        foreach (string file in productionFiles)
        {
            string source = File.ReadAllText(file);
            foreach (string pattern in forbiddenPatterns)
            {
                Assert.DoesNotContain(pattern, source, StringComparison.Ordinal);
            }
        }
    }

    private static ComplexityExpression AnalyzeMethod(
        string source,
        string methodName,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken,
        out InterproceduralAnalysisContext interproceduralContext)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        MethodDeclarationSyntax methodDeclaration = GetMethodDeclaration(facts, methodName);
        interproceduralContext = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        return MethodComplexityExtractor.AnalyzeMethod(
            methodDeclaration,
            facts.SemanticModel,
            interproceduralContext,
            options,
            cancellationToken);
    }

    private static string CreateFanoutSource(int callCount)
    {
        string calls = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, callCount).Select(index =>
                string.Create(CultureInfo.InvariantCulture, $"        Helper{index:D3}();")));
        string helpers = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, callCount).Select(index =>
                string.Create(CultureInfo.InvariantCulture, $"    private void Helper{index:D3}() {{ }}")));

        return
            """
            public sealed class Sample
            {
                void Root()
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

    private static string CreateCallChainSource(int edgeCount)
    {
        string chain = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, edgeCount).Select(index =>
            {
                string current = index == 1 ? "Root" : string.Create(CultureInfo.InvariantCulture, $"Chain{index - 1:D2}");
                string next = index == edgeCount
                    ? "Leaf"
                    : string.Create(CultureInfo.InvariantCulture, $"Chain{index:D2}");
                return string.Create(CultureInfo.InvariantCulture, $"    private void {current}(int[] values) => {next}(values);");
            }));

        return
            """
            public sealed class Sample
            {
            """
            + Environment.NewLine
            + chain
            + Environment.NewLine
            + """

                private void Leaf(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """;
    }

    private static MethodDeclarationSyntax GetMethodDeclaration(
        CompilationFacts facts,
        string methodName)
    {
        return facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName));
    }

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = Parse(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerPerformanceBudgetContractTests",
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

    private static SyntaxTree Parse(string source)
    {
        return CSharpSyntaxTree.ParseText(source);
    }

    private static TestAnalyzerConfigOptions Options(params (string Key, string Value)[] options)
    {
        return new TestAnalyzerConfigOptions(options);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ComplexityAnalysis.Analyzers.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
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

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions();

        internal TestAnalyzerConfigOptionsProvider(AnalyzerConfigOptions globalOptions)
        {
            GlobalOptions = globalOptions;
        }

        public override AnalyzerConfigOptions GlobalOptions
        {
            get;
        }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return EmptyOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return EmptyOptions;
        }
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> values;

        internal TestAnalyzerConfigOptions(params (string Key, string Value)[] values)
        {
            ImmutableDictionary<string, string>.Builder builder =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

            foreach ((string key, string value) in values)
            {
                builder[key] = value;
            }

            this.values = builder.ToImmutable();
        }

        public override bool TryGetValue(string key, out string value)
        {
            return values.TryGetValue(key, out value!);
        }
    }

    private sealed record CompilationFacts(
        CSharpCompilation Compilation,
        SyntaxTree SyntaxTree,
        SemanticModel SemanticModel);
}

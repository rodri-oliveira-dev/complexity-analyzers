using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;
using Xunit.Abstractions;

namespace ComplexityAnalysis.Analyzers.Tests;

#pragma warning disable IDE0058 // StringBuilder.AppendLine return values are irrelevant in deterministic source builders.
#pragma warning disable IDE0290 // Keep constructor style consistent with the existing xUnit test suite.

public sealed class PerformanceSyntheticCorpusTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string ExponentialRecursiveGrowthId = "BIG1005";
    private const string AnalyzerExecutionProbeId = "BIG9000";

    private readonly ITestOutputHelper output;

    public PerformanceSyntheticCorpusTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Synthetic_corpus_builder_is_deterministic()
    {
        string first = SyntheticCorpusBuilder.CreateFullCorpus();
        string second = SyntheticCorpusBuilder.CreateFullCorpus();

        Assert.Equal(first, second);
        Assert.Equal(SyntheticCorpusBuilder.ExpectedFullCorpusMethodCount, CountMethods(first));
        Assert.True(CountMethods(first) >= 500);
    }

    [Fact]
    public async Task Synthetic_corpus_completes_under_analyzer_without_timing_gate()
    {
        SyntaxTree syntaxTree = Parse(SyntheticCorpusBuilder.CreateFullCorpus(), "SyntheticPerformanceCorpus.cs");
        CSharpCompilation compilation = CreateCompilation([syntaxTree]);
        AssertCompilationHasNoErrors(compilation);
        AnalyzerOptions analyzerOptions = new(
            [],
            new TestAnalyzerConfigOptionsProvider(
                Options(
                    (ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "16"),
                    (ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "128")),
                []));

        Stopwatch stopwatch = Stopwatch.StartNew();
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers([new ComplexityAnalyzer()], analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
        stopwatch.Stop();

        output.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Synthetic corpus analyzer elapsed: {stopwatch.Elapsed.TotalMilliseconds:F1} ms"));

        Assert.True(
            diagnostics.Count(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
                >= SyntheticCorpusBuilder.TrivialMethodCount,
            "The analyzer should process the synthetic corpus and report at least the trivial method estimates.");
        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);
        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
    }

    [Fact]
    public void Shared_callee_template_is_analyzed_once_and_reused()
    {
        CompilationFacts facts = CreateFacts(SyntheticCorpusBuilder.CreateSharedCalleeGraph(rootCount: 64));
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        IMethodSymbol sharedCallee = GetMethod(facts, "SharedCallee");

        foreach (MethodDeclarationSyntax root in GetMethods(facts, method => method.Identifier.ValueText.StartsWith("SharedRoot", StringComparison.Ordinal)))
        {
            ComplexityExpression complexity = AnalyzeMethod(facts, root, context, ComplexityAnalyzerOptions.Default);

            Assert.Equal("O(n)", complexity.ToBigONotation());
        }

        Assert.Equal(1, context.TemplateCache.Count);
        Assert.True(context.TemplateCache.TryGetCompleted(
            sharedCallee,
            CancellationToken.None,
            out InterproceduralAnalysisResult cachedResult));
        Assert.Equal(InterproceduralAnalysisResultKind.Known, cachedResult.Kind);
    }

    [Fact]
    public void Unreachable_methods_are_not_expanded_into_the_template_cache()
    {
        CompilationFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Root(int[] values)
                {
                    SharedCallee(values);
                }

                private void SharedCallee(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }

                private void Unreachable(int[] values)
                {
                    foreach (var outer in values)
                    {
                        foreach (var inner in values)
                        {
                            var x = outer + inner;
                        }
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        ComplexityExpression complexity = AnalyzeMethod(
            facts,
            GetMethodDeclaration(facts, "Root"),
            context,
            ComplexityAnalyzerOptions.Default);

        Assert.Equal("O(n)", complexity.ToBigONotation());
        Assert.Equal(1, context.TemplateCache.Count);
        Assert.False(context.TemplateCache.TryGetCompleted(
            GetMethod(facts, "Unreachable"),
            CancellationToken.None,
            out _));
    }

    [Fact]
    public void Interprocedural_traversal_stops_at_configured_depth()
    {
        CompilationFacts facts = CreateFacts(SyntheticCorpusBuilder.CreateDeepCallChain(depth: 8));
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        ComplexityAnalyzerOptions options = new(
            interproceduralAnalysisEnabled: true,
            recursionAnalysisEnabled: true,
            maxCallDepth: 3,
            maxMethodsPerRoot: 128,
            ComplexityThreshold.None);

        ComplexityExpression complexity = AnalyzeMethod(
            facts,
            GetMethodDeclaration(facts, "DeepRoot"),
            context,
            options);

        Assert.IsType<UnknownComplexity>(complexity);
    }

    [Fact]
    public void Interprocedural_traversal_stops_at_configured_method_budget()
    {
        CompilationFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Root(int[] values)
                {
                    First(values);
                    Second(values);
                }

                private void First(int[] values)
                {
                }

                private void Second(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        ComplexityAnalyzerOptions options = new(
            interproceduralAnalysisEnabled: true,
            recursionAnalysisEnabled: true,
            maxCallDepth: 16,
            maxMethodsPerRoot: 1,
            ComplexityThreshold.None);

        ComplexityExpression complexity = AnalyzeMethod(
            facts,
            GetMethodDeclaration(facts, "Root"),
            context,
            options);

        Assert.IsType<UnknownComplexity>(complexity);
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public void Options_are_parsed_once_per_syntax_tree_by_the_analysis_context_cache()
    {
        SyntaxTree syntaxTree = Parse(
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """);
        CSharpCompilation compilation = CreateCompilation([syntaxTree]);
        var provider = new CountingOptionsProvider(
            Options((ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "7")));
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            compilation,
            provider,
            CancellationToken.None);

        ComplexityAnalyzerOptions first = context.GetOptions(syntaxTree, CancellationToken.None);
        ComplexityAnalyzerOptions second = context.GetOptions(syntaxTree, CancellationToken.None);

        Assert.Equal(7, first.MaxCallDepth);
        Assert.Same(first, second);
        Assert.Equal(1, provider.TreeOptionsReadCount);
    }

    private static ComplexityExpression AnalyzeMethod(
        CompilationFacts facts,
        MethodDeclarationSyntax methodDeclaration,
        InterproceduralAnalysisContext context,
        ComplexityAnalyzerOptions options)
    {
        return new MethodComplexityExtractor().AnalyzeMethod(
            methodDeclaration,
            facts.Compilation.GetSemanticModel(methodDeclaration.SyntaxTree),
            context,
            options,
            CancellationToken.None);
    }

    private static MethodDeclarationSyntax GetMethodDeclaration(
        CompilationFacts facts,
        string methodName)
    {
        return GetMethods(facts, method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName))
            .Single();
    }

    private static IMethodSymbol GetMethod(
        CompilationFacts facts,
        string methodName)
    {
        MethodDeclarationSyntax methodDeclaration = GetMethodDeclaration(facts, methodName);
        return facts.Compilation
            .GetSemanticModel(methodDeclaration.SyntaxTree)
            .GetDeclaredSymbol(methodDeclaration, CancellationToken.None)
            ?? throw new InvalidOperationException("Expected method declaration to resolve to a method symbol.");
    }

    private static ImmutableArray<MethodDeclarationSyntax> GetMethods(
        CompilationFacts facts,
        Func<MethodDeclarationSyntax, bool> predicate)
    {
        return
        [
            .. facts.SyntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(predicate)
        ];
    }

    private static CompilationFacts CreateFacts(string source)
    {
        SyntaxTree syntaxTree = Parse(source);
        CSharpCompilation compilation = CreateCompilation([syntaxTree]);
        AssertCompilationHasNoErrors(compilation);

        return new CompilationFacts(compilation, syntaxTree);
    }

    private static void AssertCompilationHasNoErrors(Compilation compilation)
    {
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);
    }

    private static int CountMethods(string source)
    {
        return CSharpSyntaxTree
            .ParseText(source)
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Count();
    }

    private static CSharpCompilation CreateCompilation(ImmutableArray<SyntaxTree> syntaxTrees)
    {
        ImmutableDictionary<string, ReportDiagnostic> specificDiagnosticOptions =
            ImmutableDictionary<string, ReportDiagnostic>.Empty
                .Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info)
                .Add(AnalyzerExecutionProbeId, ReportDiagnostic.Info);

        return CSharpCompilation.Create(
            assemblyName: "PerformanceSyntheticCorpusTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: specificDiagnosticOptions));
    }

    private static SyntaxTree Parse(string source, string path = "Sample.cs")
    {
        return CSharpSyntaxTree.ParseText(source, path: path);
    }

    private static TestAnalyzerConfigOptions Options(params (string Key, string Value)[] options)
    {
        return new TestAnalyzerConfigOptions(options);
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

    private class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions();
        private readonly ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions> treeOptions;

        internal TestAnalyzerConfigOptionsProvider(
            AnalyzerConfigOptions globalOptions,
            ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions> treeOptions)
        {
            GlobalOptions = globalOptions;
            this.treeOptions = treeOptions;
        }

        public override AnalyzerConfigOptions GlobalOptions
        {
            get;
        }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return treeOptions.TryGetValue(tree, out AnalyzerConfigOptions? options)
                ? options
                : EmptyOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return EmptyOptions;
        }
    }

    private sealed class CountingOptionsProvider : TestAnalyzerConfigOptionsProvider
    {
        internal CountingOptionsProvider(AnalyzerConfigOptions globalOptions)
            : base(globalOptions, [])
        {
        }

        internal int TreeOptionsReadCount
        {
            get;
            private set;
        }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            TreeOptionsReadCount++;
            return base.GetOptions(tree);
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

    private static class SyntheticCorpusBuilder
    {
        internal const int TrivialMethodCount = 520;
        private const int LoopHeavyMethodCount = 24;
        private const int LinqHeavyMethodCount = 24;
        private const int SharedRootCount = 64;
        private const int DeepCallChainDepth = 12;

        internal const int ExpectedFullCorpusMethodCount =
            TrivialMethodCount
            + LoopHeavyMethodCount
            + LinqHeavyMethodCount
            + SharedRootCount
            + 1
            + 1
            + DeepCallChainDepth
            + 3;

        internal static string CreateFullCorpus()
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine();
            builder.AppendLine("public sealed class SyntheticPerformanceCorpus");
            builder.AppendLine("{");
            AppendTrivialMethods(builder, TrivialMethodCount);
            AppendLoopHeavyMethods(builder, LoopHeavyMethodCount);
            AppendLinqHeavyMethods(builder, LinqHeavyMethodCount);
            AppendSharedCalleeGraphMembers(builder, SharedRootCount);
            AppendDeepCallChainMembers(builder, DeepCallChainDepth);
            AppendRecursiveMembers(builder);
            builder.AppendLine("}");
            return builder.ToString();
        }

        internal static string CreateSharedCalleeGraph(int rootCount)
        {
            var builder = new StringBuilder();
            builder.AppendLine("public sealed class SharedCalleeGraph");
            builder.AppendLine("{");
            AppendSharedCalleeGraphMembers(builder, rootCount);
            builder.AppendLine("}");
            return builder.ToString();
        }

        internal static string CreateDeepCallChain(int depth)
        {
            var builder = new StringBuilder();
            builder.AppendLine("public sealed class DeepCallChain");
            builder.AppendLine("{");
            AppendDeepCallChainMembers(builder, depth);
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendTrivialMethods(StringBuilder builder, int count)
        {
            for (int index = 0; index < count; index++)
            {
                builder.AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"    public int Trivial{index:D4}() => {index};"));
            }
        }

        private static void AppendLoopHeavyMethods(StringBuilder builder, int count)
        {
            for (int index = 0; index < count; index++)
            {
                builder.AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"    public int LoopHeavy{index:D2}(int[] values)"));
                builder.AppendLine("    {");
                builder.AppendLine("        var total = 0;");
                builder.AppendLine("        foreach (var outer in values)");
                builder.AppendLine("        {");
                builder.AppendLine("            for (var inner = 0; inner < values.Length; inner++)");
                builder.AppendLine("            {");
                builder.AppendLine("                total += outer + values[inner];");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine("        return total;");
                builder.AppendLine("    }");
            }
        }

        private static void AppendLinqHeavyMethods(StringBuilder builder, int count)
        {
            for (int index = 0; index < count; index++)
            {
                builder.AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"    public int LinqHeavy{index:D2}(IEnumerable<int> values)"));
                builder.AppendLine("    {");
                builder.AppendLine("        return values");
                builder.AppendLine("            .Where(value => value >= 0)");
                builder.AppendLine("            .OrderBy(value => value)");
                builder.AppendLine("            .Select(value => value + 1)");
                builder.AppendLine("            .ToList()");
                builder.AppendLine("            .Count;");
                builder.AppendLine("    }");
            }
        }

        private static void AppendSharedCalleeGraphMembers(StringBuilder builder, int rootCount)
        {
            for (int index = 0; index < rootCount; index++)
            {
                builder.AppendLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"    public void SharedRoot{index:D2}(int[] values) => SharedCallee(values);"));
            }

            builder.AppendLine("    private void SharedCallee(int[] values)");
            builder.AppendLine("    {");
            builder.AppendLine("        foreach (var value in values)");
            builder.AppendLine("        {");
            builder.AppendLine("            var x = value + 1;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
        }

        private static void AppendDeepCallChainMembers(StringBuilder builder, int depth)
        {
            builder.AppendLine("    public void DeepRoot(int[] values) => Deep01(values);");
            for (int index = 1; index <= depth; index++)
            {
                string methodName = string.Create(CultureInfo.InvariantCulture, $"Deep{index:D2}");
                if (index == depth)
                {
                    builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"    private void {methodName}(int[] values)"));
                    builder.AppendLine("    {");
                    builder.AppendLine("        foreach (var value in values)");
                    builder.AppendLine("        {");
                    builder.AppendLine("            var x = value + 1;");
                    builder.AppendLine("        }");
                    builder.AppendLine("    }");
                }
                else
                {
                    builder.AppendLine(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"    private void {methodName}(int[] values) => Deep{index + 1:D2}(values);"));
                }
            }
        }

        private static void AppendRecursiveMembers(StringBuilder builder)
        {
            builder.AppendLine("    public int SupportedRecursive(int n)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (n <= 1)");
            builder.AppendLine("        {");
            builder.AppendLine("            return n;");
            builder.AppendLine("        }");
            builder.AppendLine("        return SupportedRecursive(n - 1) + SupportedRecursive(n - 2);");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    public int UnsupportedRecursive(int n)");
            builder.AppendLine("    {");
            builder.AppendLine("        return UnsupportedRecursive(n - 1);");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    private void UnreachableRecursive(int n)");
            builder.AppendLine("    {");
            builder.AppendLine("        UnreachableRecursive(n - 1);");
            builder.AppendLine("    }");
        }
    }

    private sealed record CompilationFacts(
        CSharpCompilation Compilation,
        SyntaxTree SyntaxTree);
}

#pragma warning restore IDE0290
#pragma warning restore IDE0058

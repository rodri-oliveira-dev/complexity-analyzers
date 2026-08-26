using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Diagnostics;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityAnalyzerConfigurationPipelineTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string InputDependentCallInsideIterationId = "BIG1004";
    private const string ExponentialRecursiveGrowthId = "BIG1005";
    private const string MethodComplexityExceedsConfiguredThresholdId = "BIG1006";

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task Interprocedural_analysis_option_controls_source_expansion(
        string optionValue,
        bool expectsRootComplexity)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    void M(int[] items)
                    {
                        Helper(items);
                    }

                    private void Helper(int[] values)
                    {
                        foreach (var value in values)
                        {
                            var x = value + 1;
                        }
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, optionValue)));

        if (expectsRootComplexity)
        {
            AssertComplexity(diagnostics, "M", "Estimated algorithmic complexity for 'M' is O(n)");
        }
        else
        {
            Assert.DoesNotContain(diagnostics, diagnostic =>
                diagnostic.Id == EstimatedAlgorithmicComplexityId
                && GetDiagnosticText(diagnostic) == "M");
        }

        AssertComplexity(diagnostics, "Helper", "Estimated algorithmic complexity for 'Helper' is O(n)");
    }

    [Fact]
    public async Task Interprocedural_analysis_off_preserves_known_bcl_operations()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                using System.Collections.Generic;

                public sealed class Sample
                {
                    bool M(List<int> values) => values.Contains(42);
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, "false")));

        AssertComplexity(diagnostics, "M", "Estimated algorithmic complexity for 'M' is O(n)");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task Recursion_analysis_option_controls_direct_recurrence_solving(
        string optionValue,
        bool expectsRecursiveComplexity)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
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
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.RecursionAnalysisKey, optionValue)));

        if (expectsRecursiveComplexity)
        {
            AssertComplexity(diagnostics, "M", "Estimated algorithmic complexity for 'M' is O(n)");
        }
        else
        {
            Assert.DoesNotContain(diagnostics, diagnostic =>
                diagnostic.Id == EstimatedAlgorithmicComplexityId
                && GetDiagnosticText(diagnostic) == "M");
        }
    }

    [Fact]
    public async Task Recursion_analysis_off_suppresses_exponential_recursion_diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
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
                """),
            enableEstimatedComplexity: false,
            globalOptions: Options((ComplexityAnalyzerOptionsReader.RecursionAnalysisKey, "false")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);
    }

    [Fact]
    public async Task Recursion_analysis_on_reports_existing_exponential_recursion_diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
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
                """),
            enableEstimatedComplexity: false,
            globalOptions: Options((ComplexityAnalyzerOptionsReader.RecursionAnalysisKey, "true")));

        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);
    }

    [Fact]
    public async Task Max_call_depth_custom_low_returns_unknown_at_source_boundary()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(CreateCallChainSource(helperCount: 2)),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "1")));

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == EstimatedAlgorithmicComplexityId
            && GetDiagnosticText(diagnostic) == "M");
    }

    [Fact]
    public async Task Max_call_depth_default_preserves_existing_depth_boundary()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(Parse(CreateCallChainSource(helperCount: 5)));

        AssertComplexity(diagnostics, "M", "Estimated algorithmic complexity for 'M' is O(n)");
    }

    [Fact]
    public async Task Max_call_depth_accepts_hard_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(CreateCallChainSource(helperCount: 6)),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "16")));

        AssertComplexity(diagnostics, "M", "Estimated algorithmic complexity for 'M' is O(n)");
    }

    [Fact]
    public async Task Max_methods_per_root_custom_budget_returns_unknown_without_poisoning_diagnostics()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    void M(int[] items)
                    {
                        First(items);
                        Second(items);
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
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "1")));

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == EstimatedAlgorithmicComplexityId
            && GetDiagnosticText(diagnostic) == "M");
    }

    [Fact]
    public async Task Existing_diagnostics_follow_interprocedural_results()
    {
        ImmutableArray<Diagnostic> enabled = await GetAnalyzerDiagnosticsAsync(
            Parse(SourceCallInsideLoop()),
            enableEstimatedComplexity: false,
            globalOptions: Options((ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, "true")));
        ImmutableArray<Diagnostic> disabled = await GetAnalyzerDiagnosticsAsync(
            Parse(SourceCallInsideLoop()),
            enableEstimatedComplexity: false,
            globalOptions: Options((ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, "false")));

        _ = Assert.Single(enabled, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
        Assert.DoesNotContain(disabled, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
    }

    [Fact]
    public async Task Maximum_complexity_none_does_not_report_threshold_diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(QuadraticLoopSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "none")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_is_below_threshold()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    int M() => 42;
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_equals_threshold()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(LinearLoopSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_reports_when_quadratic_exceeds_n_log_n()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(QuadraticLoopSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n_log_n")));

        AssertThreshold(
            diagnostics,
            "M",
            "Method 'M' has estimated complexity O(n\u00b2), exceeding configured maximum O(n log n)");
    }

    [Fact]
    public async Task Threshold_reports_when_exponential_exceeds_cubic()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
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
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n3")));

        AssertThreshold(
            diagnostics,
            "Fibonacci",
            "Method 'Fibonacci' has estimated complexity O(1.618^n), exceeding configured maximum O(n\u00b3)");
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_is_unknown()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public interface CustomCollection
                {
                    bool Probe(int value);
                }

                public sealed class Sample
                {
                    bool M(CustomCollection values) => values.Probe(42);
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "constant")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_and_threshold_are_incomparable()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    void M(int[] left, int[] right)
                    {
                        foreach (var outer in left)
                        {
                            Search(right);
                        }
                    }

                    private void Search(int[] values)
                    {
                        foreach (var value in values)
                        {
                            var x = value + 1;
                        }
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n2")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Tree_specific_thresholds_are_preserved_for_different_files()
    {
        SyntaxTree linearTree = Parse(
            """
            public sealed class LinearSample
            {
                void M(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "LinearSample.cs");
        SyntaxTree quadraticTree = Parse(QuadraticLoopSource("QuadraticSample"), "QuadraticSample.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            [linearTree, quadraticTree],
            treeOptions: ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>.Empty
                .Add(linearTree, Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n2")))
                .Add(quadraticTree, Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n"))));

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == linearTree);
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == quadraticTree
            && diagnostic.GetMessage(CultureInfo.InvariantCulture) == "Method 'M' has estimated complexity O(n\u00b2), exceeding configured maximum O(n)");
    }

    [Fact]
    public async Task Threshold_diagnostic_severity_uses_standard_roslyn_override()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(LinearLoopSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "constant")),
            thresholdDiagnosticReport: ReportDiagnostic.Warn);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Tree_specific_options_are_preserved_for_different_files()
    {
        SyntaxTree disabledTree = Parse(
            """
            public sealed class DisabledRoot
            {
                void M(int[] items)
                {
                    Shared.Helper(items);
                }
            }
            """,
            "DisabledRoot.cs");
        SyntaxTree enabledTree = Parse(
            """
            public sealed class EnabledRoot
            {
                void M(int[] items)
                {
                    Shared.Helper(items);
                }
            }
            """,
            "EnabledRoot.cs");
        SyntaxTree sharedTree = Parse(
            """
            public static class Shared
            {
                public static void Helper(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "Shared.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            [disabledTree, enabledTree, sharedTree],
            treeOptions: ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>.Empty
                .Add(disabledTree, Options((ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, "false")))
                .Add(enabledTree, Options((ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, "true"))));

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == EstimatedAlgorithmicComplexityId
            && diagnostic.Location.SourceTree == disabledTree
            && GetDiagnosticText(diagnostic) == "M");
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == EstimatedAlgorithmicComplexityId
            && diagnostic.Location.SourceTree == enabledTree
            && diagnostic.GetMessage(CultureInfo.InvariantCulture) == "Estimated algorithmic complexity for 'M' is O(n)");
    }

    [Fact]
    public void Cache_key_keeps_depth_specific_templates_isolated()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void MHigh(int[] items)
                {
                    A(items);
                }

                void MLow(int[] items)
                {
                    A(items);
                }

                private void A(int[] values)
                {
                    B(values);
                }

                private void B(int[] values)
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

        ComplexityExpression high = AnalyzeMethod(
            facts,
            "MHigh",
            context,
            new ComplexityAnalyzerOptions(
                interproceduralAnalysisEnabled: true,
                recursionAnalysisEnabled: true,
                maxCallDepth: 2,
                maxMethodsPerRoot: 32,
                ComplexityThreshold.None));
        ComplexityExpression low = AnalyzeMethod(
            facts,
            "MLow",
            context,
            new ComplexityAnalyzerOptions(
                interproceduralAnalysisEnabled: true,
                recursionAnalysisEnabled: true,
                maxCallDepth: 1,
                maxMethodsPerRoot: 32,
                ComplexityThreshold.None));

        Assert.Equal("O(n)", high.ToBigONotation());
        Assert.Equal("Unknown", low.ToBigONotation());
    }

    [Fact]
    public void Source_expansion_disabled_does_not_reuse_enabled_cache_entry()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void MOn(int[] items)
                {
                    Helper(items);
                }

                void MOff(int[] items)
                {
                    Helper(items);
                }

                private void Helper(int[] values)
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

        ComplexityExpression enabled = AnalyzeMethod(
            facts,
            "MOn",
            context,
            new ComplexityAnalyzerOptions(
                interproceduralAnalysisEnabled: true,
                recursionAnalysisEnabled: true,
                maxCallDepth: 5,
                maxMethodsPerRoot: 32,
                ComplexityThreshold.None));
        ComplexityExpression disabled = AnalyzeMethod(
            facts,
            "MOff",
            context,
            new ComplexityAnalyzerOptions(
                interproceduralAnalysisEnabled: false,
                recursionAnalysisEnabled: true,
                maxCallDepth: 5,
                maxMethodsPerRoot: 32,
                ComplexityThreshold.None));

        Assert.Equal("O(n)", enabled.ToBigONotation());
        Assert.Equal("Unknown", disabled.ToBigONotation());
    }

    private static ComplexityExpression AnalyzeMethod(
        CompilationFacts facts,
        string methodName,
        InterproceduralAnalysisContext context,
        ComplexityAnalyzerOptions options)
    {
        MethodDeclarationSyntax methodDeclaration = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName));
        SemanticModel semanticModel = facts.Compilation.GetSemanticModel(methodDeclaration.SyntaxTree);

        return MethodComplexityExtractor.AnalyzeMethod(
            methodDeclaration,
            semanticModel,
            context,
            options,
            CancellationToken.None);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        SyntaxTree syntaxTree,
        bool enableEstimatedComplexity = true,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? thresholdDiagnosticReport = null)
    {
        return await GetAnalyzerDiagnosticsAsync(
            [syntaxTree],
            enableEstimatedComplexity,
            globalOptions,
            treeOptions,
            thresholdDiagnosticReport);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        ImmutableArray<SyntaxTree> syntaxTrees,
        bool enableEstimatedComplexity = true,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? thresholdDiagnosticReport = null)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder specificDiagnosticOptions =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (enableEstimatedComplexity)
        {
            specificDiagnosticOptions.Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info);
        }

        if (thresholdDiagnosticReport.HasValue)
        {
            specificDiagnosticOptions.Add(MethodComplexityExceedsConfiguredThresholdId, thresholdDiagnosticReport.Value);
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ComplexityAnalyzerConfigurationPipelineTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: specificDiagnosticOptions.ToImmutable()));
        var provider = new TestAnalyzerConfigOptionsProvider(
            globalOptions ?? Options(),
            treeOptions ?? []);
        AnalyzerOptions analyzerOptions = new([], provider);

        return await compilation
            .WithAnalyzers([new ComplexityAnalyzer()], analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static void AssertComplexity(
        ImmutableArray<Diagnostic> diagnostics,
        string diagnosticText,
        string expectedMessage)
    {
        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == diagnosticText);

        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(
            diagnostic,
            DiagnosticPropertyNames.Complexity,
            expectedMessage[(expectedMessage.IndexOf(" is ", StringComparison.Ordinal) + " is ".Length)..]);
    }

    private static void AssertThreshold(
        ImmutableArray<Diagnostic> diagnostics,
        string diagnosticText,
        string expectedMessage)
    {
        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == diagnosticText);

        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.True(diagnostic.Properties.TryGetValue(DiagnosticPropertyNames.Complexity, out string? complexity));
        Assert.True(diagnostic.Properties.TryGetValue(DiagnosticPropertyNames.Threshold, out string? threshold));
        Assert.Contains(complexity!, expectedMessage, StringComparison.Ordinal);
        Assert.Contains(threshold!, expectedMessage, StringComparison.Ordinal);
    }

    private static void AssertProperty(
        Diagnostic diagnostic,
        string key,
        string expectedValue)
    {
        Assert.True(diagnostic.Properties.TryGetValue(key, out string? actualValue));
        Assert.Equal(expectedValue, actualValue);
    }

    private static string GetDiagnosticText(Diagnostic diagnostic)
    {
        SyntaxTree sourceTree = diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("Expected a source location.");
        return sourceTree
            .GetText()
            .GetSubText(diagnostic.Location.SourceSpan)
            .ToString();
    }

    private static string CreateCallChainSource(int helperCount)
    {
        string helpers = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, helperCount).Select(index =>
                index == helperCount
                    ? "    private void H" + index + "(int[] values) { foreach (var value in values) { var x = value + 1; } }"
                    : "    private void H" + index + "(int[] values) => H" + (index + 1) + "(values);"));

        return
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    H1(items);
                }

            """
            + helpers
            + Environment.NewLine
            + """
            }
            """;
    }

    private static string SourceCallInsideLoop()
    {
        return
            """
            public sealed class Sample
            {
                void M(int[] outer, int[] inner)
                {
                    foreach (var item in outer)
                    {
                        Check(inner);
                    }
                }

                private void Check(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """;
    }

    private static string LinearLoopSource()
    {
        return
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """;
    }

    private static string QuadraticLoopSource(string className = "Sample")
    {
        return $$"""
            public sealed class {{className}}
            {
                void M(int[] values)
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
            """;
    }

    private static SyntaxTree Parse(string source, string path = "Sample.cs")
    {
        return CSharpSyntaxTree.ParseText(source, path: path);
    }

    private static TestAnalyzerConfigOptions Options(params (string Key, string Value)[] options)
    {
        return new TestAnalyzerConfigOptions(options);
    }

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = Parse(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ComplexityAnalyzerConfigurationPipelineTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        return new CompilationFacts(compilation, syntaxTree);
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
        SyntaxTree SyntaxTree);
}

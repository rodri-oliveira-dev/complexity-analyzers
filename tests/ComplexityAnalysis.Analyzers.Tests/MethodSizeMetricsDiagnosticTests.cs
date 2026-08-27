using System.Collections.Immutable;
using System.Globalization;

using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class MethodSizeMetricsDiagnosticTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string MethodComplexityExceedsConfiguredThresholdId = "BIG1006";
    private const string CyclomaticComplexityExceedsConfiguredThresholdId = "BIG2001";
    private const string MaximumNestingDepthExceedsConfiguredThresholdId = "BIG2002";
    private const string MethodNlocExceedsConfiguredThresholdId = "BIG2003";
    private const string StatementCountExceedsConfiguredThresholdId = "BIG2004";
    private const string TokenCountExceedsConfiguredThresholdId = "BIG2005";

    [Fact]
    public async Task Missing_thresholds_do_not_report_method_size_diagnostics()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(Parse(SizedSource()));

        Assert.DoesNotContain(diagnostics, IsMethodSizeDiagnostic);
    }

    [Theory]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, MethodNlocExceedsConfiguredThresholdId)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, StatementCountExceedsConfiguredThresholdId)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, TokenCountExceedsConfiguredThresholdId)]
    public async Task Zero_threshold_is_valid_and_reports_non_empty_metrics(
        string optionKey,
        string diagnosticId)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((optionKey, "0")));

        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.5")]
    [InlineData("1 2")]
    [InlineData("999999999999")]
    [InlineData("ten")]
    public async Task Invalid_threshold_values_fall_back_to_unset(string value)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options(
                (ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, value),
                (ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, value),
                (ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, value)));

        Assert.DoesNotContain(diagnostics, IsMethodSizeDiagnostic);
    }

    [Theory]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, MethodNlocExceedsConfiguredThresholdId, "4")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, StatementCountExceedsConfiguredThresholdId, "4")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, TokenCountExceedsConfiguredThresholdId, "19")]
    public async Task Threshold_does_not_report_when_actual_is_below_maximum(
        string optionKey,
        string diagnosticId,
        string threshold)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((optionKey, threshold)));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    [Theory]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, MethodNlocExceedsConfiguredThresholdId, "3")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, StatementCountExceedsConfiguredThresholdId, "3")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, TokenCountExceedsConfiguredThresholdId, "18")]
    public async Task Threshold_does_not_report_when_actual_equals_maximum(
        string optionKey,
        string diagnosticId,
        string threshold)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((optionKey, threshold)));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    [Fact]
    public async Task Nloc_threshold_reports_metric_name_actual_value_and_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "2")));

        Diagnostic diagnostic = AssertThreshold(
            diagnostics,
            MethodNlocExceedsConfiguredThresholdId,
            "M",
            "Member 'M' has NLOC 3, exceeding configured maximum 2");
        AssertProperty(diagnostic, DiagnosticPropertyNames.MethodNloc, "3");
        AssertProperty(diagnostic, DiagnosticPropertyNames.Threshold, "2");
    }

    [Fact]
    public async Task Statement_count_threshold_reports_metric_name_actual_value_and_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "2")));

        Diagnostic diagnostic = AssertThreshold(
            diagnostics,
            StatementCountExceedsConfiguredThresholdId,
            "M",
            "Member 'M' has statement count 3, exceeding configured maximum 2");
        AssertProperty(diagnostic, DiagnosticPropertyNames.StatementCount, "3");
        AssertProperty(diagnostic, DiagnosticPropertyNames.Threshold, "2");
    }

    [Fact]
    public async Task Token_count_threshold_reports_metric_name_actual_value_and_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "17")));

        Diagnostic diagnostic = AssertThreshold(
            diagnostics,
            TokenCountExceedsConfiguredThresholdId,
            "M",
            "Member 'M' has token count 18, exceeding configured maximum 17");
        AssertProperty(diagnostic, DiagnosticPropertyNames.TokenCount, "18");
        AssertProperty(diagnostic, DiagnosticPropertyNames.Threshold, "17");
    }

    [Fact]
    public async Task Expression_bodied_member_returning_lambda_reports_parent_nloc_and_token_count()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    System.Func<int> M() => () => 1;
                }
                """),
            globalOptions: Options(
                (ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "0"),
                (ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "0")));

        _ = AssertThreshold(
            diagnostics,
            MethodNlocExceedsConfiguredThresholdId,
            "M",
            "Member 'M' has NLOC 1, exceeding configured maximum 0");
        _ = AssertThreshold(
            diagnostics,
            TokenCountExceedsConfiguredThresholdId,
            "M",
            "Member 'M' has token count 3, exceeding configured maximum 0");
    }

    [Fact]
    public async Task Thresholds_are_independent_from_big_o_cyclomatic_and_nesting_thresholds()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "2")),
            enableEstimatedComplexity: true);

        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == MethodNlocExceedsConfiguredThresholdId);
        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MaximumNestingDepthExceedsConfiguredThresholdId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == StatementCountExceedsConfiguredThresholdId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == TokenCountExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Tree_specific_thresholds_are_preserved_for_different_files()
    {
        SyntaxTree firstTree = Parse(SizedSource("First"), "First.cs");
        SyntaxTree secondTree = Parse(SizedSource("Second"), "Second.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            [firstTree, secondTree],
            treeOptions: ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>.Empty
                .Add(firstTree, Options((ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "3")))
                .Add(secondTree, Options((ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "2"))));

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == StatementCountExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == firstTree);
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == StatementCountExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == secondTree
            && diagnostic.GetMessage(CultureInfo.InvariantCulture) == "Member 'M' has statement count 3, exceeding configured maximum 2");
    }

    [Fact]
    public async Task Severity_uses_standard_roslyn_override()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(SizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "17")),
            tokenCountDiagnosticReport: ReportDiagnostic.Warn);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == TokenCountExceedsConfiguredThresholdId);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Generated_code_is_excluded_from_method_size_diagnostics()
    {
        SyntaxTree generatedTree = Parse(
            """
            // <auto-generated/>
            public sealed class GeneratedSample
            {
                void M()
                {
                    var value = 0;
                }
            }
            """,
            "GeneratedSample.g.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            generatedTree,
            globalOptions: Options(
                (ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "0"),
                (ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "0"),
                (ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "0")));

        Assert.DoesNotContain(diagnostics, IsMethodSizeDiagnostic);
    }

    [Fact]
    public async Task Nested_lambda_threshold_reports_only_for_independent_lambda_root()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
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
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "2")));

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == StatementCountExceedsConfiguredThresholdId);

        Assert.Equal(
            "Member 'lambda' has statement count 3, exceeding configured maximum 2",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(diagnostic, "=>");
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        SyntaxTree syntaxTree,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? tokenCountDiagnosticReport = null,
        bool enableEstimatedComplexity = false)
    {
        return await GetAnalyzerDiagnosticsAsync(
            [syntaxTree],
            globalOptions,
            treeOptions,
            tokenCountDiagnosticReport,
            enableEstimatedComplexity);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        ImmutableArray<SyntaxTree> syntaxTrees,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? tokenCountDiagnosticReport = null,
        bool enableEstimatedComplexity = false)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder specificDiagnosticOptions =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (tokenCountDiagnosticReport.HasValue)
        {
            specificDiagnosticOptions.Add(TokenCountExceedsConfiguredThresholdId, tokenCountDiagnosticReport.Value);
        }

        if (enableEstimatedComplexity)
        {
            specificDiagnosticOptions.Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info);
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "MethodSizeMetricsDiagnosticTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: specificDiagnosticOptions.ToImmutable()));
        AnalyzerOptions analyzerOptions = new(
            [],
            new TestAnalyzerConfigOptionsProvider(
                globalOptions ?? Options(),
                treeOptions ?? []));

        return await compilation
            .WithAnalyzers([new ComplexityAnalyzer()], analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static Diagnostic AssertThreshold(
        ImmutableArray<Diagnostic> diagnostics,
        string diagnosticId,
        string diagnosticText,
        string expectedMessage)
    {
        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == diagnosticId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == diagnosticText);

        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        return diagnostic;
    }

    private static bool IsMethodSizeDiagnostic(Diagnostic diagnostic)
    {
        return diagnostic.Id is MethodNlocExceedsConfiguredThresholdId
            or StatementCountExceedsConfiguredThresholdId
            or TokenCountExceedsConfiguredThresholdId;
    }

    private static void AssertProperty(
        Diagnostic diagnostic,
        string key,
        string expectedValue)
    {
        Assert.True(diagnostic.Properties.TryGetValue(key, out string? actualValue));
        Assert.Equal(expectedValue, actualValue);
    }

    private static void AssertDiagnosticText(Diagnostic diagnostic, string expectedText)
    {
        Assert.Equal(expectedText, GetDiagnosticText(diagnostic));
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

    private static string SizedSource(string className = "Sample")
    {
        return $$"""
            public sealed class {{className}}
            {
                void M()
                {
                    var value = 0;
                    if (value == 0)
                    {
                        value++;
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
}

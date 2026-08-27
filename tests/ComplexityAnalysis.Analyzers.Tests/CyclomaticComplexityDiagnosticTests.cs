using System.Collections.Immutable;
using System.Globalization;

using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class CyclomaticComplexityDiagnosticTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string MethodComplexityExceedsConfiguredThresholdId = "BIG1006";
    private const string CyclomaticComplexityExceedsConfiguredThresholdId = "BIG2001";

    [Fact]
    public async Task Missing_threshold_does_not_report_cyclomatic_diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(BranchingSource()));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.5")]
    [InlineData("ten")]
    public async Task Invalid_threshold_values_fall_back_to_unset(string value)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(BranchingSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, value)));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_is_below_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(BranchingSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "4")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_equals_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(BranchingSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "3")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_reports_when_actual_exceeds_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(BranchingSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2")));

        Diagnostic diagnostic = AssertCyclomaticThreshold(
            diagnostics,
            "M",
            "Member 'M' has cyclomatic complexity 3, exceeding configured maximum 2 (standard mode)");
        AssertProperty(diagnostic, DiagnosticPropertyNames.CyclomaticComplexity, "3");
        AssertProperty(diagnostic, DiagnosticPropertyNames.Threshold, "2");
        AssertProperty(diagnostic, DiagnosticPropertyNames.CyclomaticComplexityMode, "standard");
    }

    [Fact]
    public async Task Modified_mccabe_mode_changes_switch_threshold_behavior()
    {
        SyntaxTree source = Parse(
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    switch (value)
                    {
                        case 0:
                            return 0;
                        case 1:
                            return 1;
                        default:
                            return -1;
                    }
                }
            }
            """);

        ImmutableArray<Diagnostic> standardDiagnostics = await GetAnalyzerDiagnosticsAsync(
            source,
            globalOptions: Options(
                (ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2"),
                (ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, "standard")));
        ImmutableArray<Diagnostic> modifiedDiagnostics = await GetAnalyzerDiagnosticsAsync(
            source,
            globalOptions: Options(
                (ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2"),
                (ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, "modified_mccabe")));

        _ = AssertCyclomaticThreshold(
            standardDiagnostics,
            "M",
            "Member 'M' has cyclomatic complexity 3, exceeding configured maximum 2 (standard mode)");
        Assert.DoesNotContain(modifiedDiagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Invalid_mode_falls_back_to_standard()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    int M(int value)
                    {
                        switch (value)
                        {
                            case 0:
                                return 0;
                            case 1:
                                return 1;
                            default:
                                return -1;
                        }
                    }
                }
                """),
            globalOptions: Options(
                (ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2"),
                (ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, "invalid")));

        _ = AssertCyclomaticThreshold(
            diagnostics,
            "M",
            "Member 'M' has cyclomatic complexity 3, exceeding configured maximum 2 (standard mode)");
    }

    [Fact]
    public async Task Guarded_discard_switch_expression_arm_can_exceed_modified_mccabe_threshold()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    int M(int value)
                    {
                        return value switch
                        {
                            _ when Check(value) => 1,
                            _ => 0,
                        };
                    }

                    bool Check(int value) => value > 0;
                }
                """),
            globalOptions: Options(
                (ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2"),
                (ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, "modified_mccabe")));

        _ = AssertCyclomaticThreshold(
            diagnostics,
            "M",
            "Member 'M' has cyclomatic complexity 3, exceeding configured maximum 2 (modified_mccabe mode)");
    }

    [Fact]
    public async Task Tree_specific_thresholds_are_preserved_for_different_files()
    {
        SyntaxTree firstTree = Parse(BranchingSource("First"), "First.cs");
        SyntaxTree secondTree = Parse(BranchingSource("Second"), "Second.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            [firstTree, secondTree],
            treeOptions: ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>.Empty
                .Add(firstTree, Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "3")))
                .Add(secondTree, Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2"))));

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == firstTree);
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == secondTree
            && diagnostic.GetMessage(CultureInfo.InvariantCulture) == "Member 'M' has cyclomatic complexity 3, exceeding configured maximum 2 (standard mode)");
    }

    [Fact]
    public async Task Threshold_diagnostic_severity_uses_standard_roslyn_override()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(BranchingSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2")),
            cyclomaticDiagnosticReport: ReportDiagnostic.Warn);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Cyclomatic_threshold_does_not_enable_big_o_threshold()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(BranchingSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "2")),
            enableEstimatedComplexity: true);

        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Nested_lambda_threshold_reports_only_for_independent_lambda_root()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                using System;

                public sealed class Sample
                {
                    void M()
                    {
                        Func<int, int> lambda = value => value > 0 ? value : -value;
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "1")));

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);

        Assert.Equal(
            "Member 'lambda' has cyclomatic complexity 2, exceeding configured maximum 1 (standard mode)",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(diagnostic, "=>");
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        SyntaxTree syntaxTree,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? cyclomaticDiagnosticReport = null,
        bool enableEstimatedComplexity = false)
    {
        return await GetAnalyzerDiagnosticsAsync(
            [syntaxTree],
            globalOptions,
            treeOptions,
            cyclomaticDiagnosticReport,
            enableEstimatedComplexity);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        ImmutableArray<SyntaxTree> syntaxTrees,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? cyclomaticDiagnosticReport = null,
        bool enableEstimatedComplexity = false)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder specificDiagnosticOptions =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (cyclomaticDiagnosticReport.HasValue)
        {
            specificDiagnosticOptions.Add(CyclomaticComplexityExceedsConfiguredThresholdId, cyclomaticDiagnosticReport.Value);
        }

        if (enableEstimatedComplexity)
        {
            specificDiagnosticOptions.Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info);
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "CyclomaticComplexityDiagnosticTests",
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

    private static Diagnostic AssertCyclomaticThreshold(
        ImmutableArray<Diagnostic> diagnostics,
        string diagnosticText,
        string expectedMessage)
    {
        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == diagnosticText);

        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        return diagnostic;
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

    private static string BranchingSource(string className = "Sample")
    {
        return $$"""
            public sealed class {{className}}
            {
                int M(int value)
                {
                    if (value > 0)
                    {
                        return 1;
                    }
                    else if (value < 0)
                    {
                        return -1;
                    }

                    return 0;
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

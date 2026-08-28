using System.Collections.Immutable;
using System.Globalization;

using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class CognitiveComplexityDiagnosticTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string MethodComplexityExceedsConfiguredThresholdId = "BIG1006";
    private const string CyclomaticComplexityExceedsConfiguredThresholdId = "BIG2001";
    private const string MaximumNestingDepthExceedsConfiguredThresholdId = "BIG2002";
    private const string CognitiveComplexityExceedsConfiguredThresholdId = "BIG2007";

    [Fact]
    public async Task Missing_threshold_does_not_report_cognitive_complexity_diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(NestedSource()));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);
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
            Parse(NestedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, value)));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Zero_threshold_is_valid_and_reports_any_cognitive_complexity()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    void M(bool flag)
                    {
                        if (flag)
                        {
                        }
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "0")));

        _ = AssertCognitiveThreshold(
            diagnostics,
            "M",
            "Member 'M' has cognitive complexity 1, exceeding configured maximum 0");
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_is_below_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(NestedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "7")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_does_not_report_when_actual_equals_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(NestedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "6")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public async Task Threshold_reports_when_actual_exceeds_maximum()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(NestedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "5")));

        Diagnostic diagnostic = AssertCognitiveThreshold(
            diagnostics,
            "M",
            "Member 'M' has cognitive complexity 6, exceeding configured maximum 5");
        AssertProperty(diagnostic, DiagnosticPropertyNames.CognitiveComplexity, "6");
        AssertProperty(diagnostic, DiagnosticPropertyNames.Threshold, "5");
    }

    [Fact]
    public async Task Tree_specific_thresholds_are_preserved_for_different_files()
    {
        SyntaxTree firstTree = Parse(NestedSource("First"), "First.cs");
        SyntaxTree secondTree = Parse(NestedSource("Second"), "Second.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            [firstTree, secondTree],
            treeOptions: ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>.Empty
                .Add(firstTree, Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "6")))
                .Add(secondTree, Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "5"))));

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == firstTree);
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId
            && diagnostic.Location.SourceTree == secondTree
            && diagnostic.GetMessage(CultureInfo.InvariantCulture) == "Member 'M' has cognitive complexity 6, exceeding configured maximum 5");
    }

    [Fact]
    public async Task Threshold_diagnostic_severity_uses_standard_roslyn_override()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(NestedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "5")),
            cognitiveDiagnosticReport: ReportDiagnostic.Warn);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Cognitive_threshold_does_not_enable_other_metric_thresholds()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    int M(int[] values)
                    {
                        if (values.Length > 0)
                        {
                            foreach (var value in values)
                            {
                                if (value > 0)
                                {
                                    return value;
                                }
                            }
                        }

                        return 0;
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "5")),
            enableEstimatedComplexity: true);

        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);
        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CyclomaticComplexityExceedsConfiguredThresholdId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MaximumNestingDepthExceedsConfiguredThresholdId);
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
                        Func<int, int> lambda = value => value > 0
                            ? value > 10 ? 10 : value
                            : -value;
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "2")));

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);

        Assert.Equal(
            "Member 'lambda' has cognitive complexity 3, exceeding configured maximum 2",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(diagnostic, "=>");
    }

    [Fact]
    public async Task Generated_code_is_excluded_from_cognitive_complexity_diagnostics()
    {
        SyntaxTree generatedTree = Parse(
            """
            // <auto-generated/>
            public sealed class GeneratedSample
            {
                void M(bool flag)
                {
                    if (flag)
                    {
                    }
                }
            }
            """,
            "GeneratedSample.g.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            generatedTree,
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "0")));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId);
    }

    [Theory]
    [MemberData(nameof(MemberKindCases))]
    public async Task Supported_executable_member_kinds_report_independent_cognitive_thresholds(
        string scenario,
        string source,
        string diagnosticText)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(source),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumCognitiveComplexityKey, "0")));

        Diagnostic diagnostic = AssertCognitiveThreshold(
            diagnostics,
            diagnosticText,
            "Member '" + ExpectedDisplayName(diagnosticText) + "' has cognitive complexity 1, exceeding configured maximum 0");
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        _ = scenario;
    }

    public static TheoryData<string, string, string> MemberKindCases
    {
        get;
    } = new()
    {
        {
            "ordinary method",
            """
            public sealed class Sample
            {
                void M(bool flag)
                {
                    if (flag)
                    {
                    }
                }
            }
            """,
            "M"
        },
        {
            "constructor",
            """
            public sealed class Sample
            {
                public Sample(bool flag)
                {
                    if (flag)
                    {
                    }
                }
            }
            """,
            "Sample"
        },
        {
            "property getter",
            """
            public sealed class Sample
            {
                private bool flag;

                public int Value
                {
                    get
                    {
                        if (flag)
                        {
                            return 1;
                        }

                        return 0;
                    }
                }
            }
            """,
            "get"
        },
        {
            "operator",
            """
            public sealed class Sample
            {
                public static Sample operator +(Sample left, Sample right)
                {
                    if (left is null)
                    {
                        return right;
                    }

                    return left;
                }
            }
            """,
            "operator"
        },
        {
            "local function",
            """
            public sealed class Sample
            {
                void M()
                {
                    void Local(bool flag)
                    {
                        if (flag)
                        {
                        }
                    }
                }
            }
            """,
            "Local"
        },
        {
            "lambda",
            """
            using System;

            public sealed class Sample
            {
                void M()
                {
                    Func<bool, int> lambda = flag => flag ? 1 : 0;
                }
            }
            """,
            "=>"
        },
        {
            "anonymous method",
            """
            using System;

            public sealed class Sample
            {
                void M(bool flag)
                {
                    Action action = delegate
                    {
                        if (flag)
                        {
                        }
                    };
                }
            }
            """,
            "delegate"
        },
        {
            "expression-bodied property",
            """
            public sealed class Sample
            {
                private bool flag;

                public int Value => flag ? 1 : 0;
            }
            """,
            "Value"
        }
    };

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        SyntaxTree syntaxTree,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? cognitiveDiagnosticReport = null,
        bool enableEstimatedComplexity = false)
    {
        return await GetAnalyzerDiagnosticsAsync(
            [syntaxTree],
            globalOptions,
            treeOptions,
            cognitiveDiagnosticReport,
            enableEstimatedComplexity);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        ImmutableArray<SyntaxTree> syntaxTrees,
        AnalyzerConfigOptions? globalOptions = null,
        ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>? treeOptions = null,
        ReportDiagnostic? cognitiveDiagnosticReport = null,
        bool enableEstimatedComplexity = false)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder specificDiagnosticOptions =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (cognitiveDiagnosticReport.HasValue)
        {
            specificDiagnosticOptions.Add(CognitiveComplexityExceedsConfiguredThresholdId, cognitiveDiagnosticReport.Value);
        }

        if (enableEstimatedComplexity)
        {
            specificDiagnosticOptions.Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info);
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "CognitiveComplexityDiagnosticTests",
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

    private static Diagnostic AssertCognitiveThreshold(
        ImmutableArray<Diagnostic> diagnostics,
        string diagnosticText,
        string expectedMessage)
    {
        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == CognitiveComplexityExceedsConfiguredThresholdId)
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

    private static string NestedSource(string className = "Sample")
    {
        return $$"""
            public sealed class {{className}}
            {
                int M(bool a, bool b, bool c)
                {
                    if (a)
                    {
                        while (b)
                        {
                            if (c)
                            {
                                return 1;
                            }
                        }
                    }

                    return 0;
                }
            }
            """;
    }

    private static string ExpectedDisplayName(string diagnosticText)
    {
        return diagnosticText switch
        {
            "Sample" => "Sample.ctor",
            "get" => "Value.get",
            "operator" => "operator +",
            "=>" => "lambda",
            "delegate" => "anonymous method",
            "Value" => "Value.get",
            _ => diagnosticText,
        };
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

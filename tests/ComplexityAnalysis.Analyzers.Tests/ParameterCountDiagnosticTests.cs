using System.Collections.Immutable;
using System.Globalization;

using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ParameterCountDiagnosticTests
{
    private const string ParameterCountExceedsConfiguredThresholdId = "BIG2006";

    [Fact]
    public async Task Missing_threshold_does_not_report_parameter_count_diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(Parse(ParameterizedSource()));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
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
            Parse(ParameterizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, value)));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
    }

    [Fact]
    public async Task Whitespace_wrapped_threshold_is_valid()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(ParameterizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, " 2 ")));

        _ = Assert.Single(diagnostics, IsParameterCountDiagnostic);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("3")]
    public async Task Threshold_does_not_report_when_actual_is_below_or_equal_to_maximum(string threshold)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(ParameterizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, threshold)));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
    }

    [Fact]
    public async Task Threshold_reports_member_actual_value_maximum_location_and_properties()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(ParameterizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "2")));

        Diagnostic diagnostic = Assert.Single(diagnostics, IsParameterCountDiagnostic);

        Assert.Equal(
            "Member 'M' declares 3 parameters, exceeding configured maximum 2",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        AssertProperty(diagnostic, DiagnosticPropertyNames.ParameterCount, "3");
        AssertProperty(diagnostic, DiagnosticPropertyNames.Threshold, "2");
        AssertDiagnosticText(diagnostic, "M");
    }

    [Fact]
    public async Task Zero_threshold_is_valid_and_reports_members_with_parameters()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    void Empty() { }
                    void One(int value) { }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "0")));

        Diagnostic diagnostic = Assert.Single(diagnostics, IsParameterCountDiagnostic);

        Assert.Equal(
            "Member 'One' declares 1 parameters, exceeding configured maximum 0",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(diagnostic, "One");
    }

    [Fact]
    public async Task Extension_method_this_receiver_counts()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                using System.Collections.Generic;

                public static class Extensions
                {
                    public static bool ContainsValue(this IEnumerable<string> source, string value) => true;
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1")));

        Diagnostic diagnostic = Assert.Single(diagnostics, IsParameterCountDiagnostic);

        Assert.Equal(
            "Member 'ContainsValue' declares 2 parameters, exceeding configured maximum 1",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Generic_type_parameters_do_not_count()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    T Convert<T, TResult>(T value)
                    {
                        return value;
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1")));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
    }

    [Fact]
    public async Task Operators_constructors_local_functions_lambdas_and_anonymous_methods_report_independently()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    public Sample(int left, int right) { }

                    public static Sample operator +(Sample left, Sample right) => left;

                    public static implicit operator int(Sample value) => 0;

                    void M()
                    {
                        int Local(int left, int right) => left + right;
                        System.Func<int, int, int> lambda = (left, right) => left + right;
                        System.Func<int, int, int> anonymous = delegate(int left, int right)
                        {
                            return left + right;
                        };
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1")));

        ImmutableArray<string> messages =
        [
            .. diagnostics
                .Where(IsParameterCountDiagnostic)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
        ];

        Assert.Equal(
            [
                "Member 'Sample.ctor' declares 2 parameters, exceeding configured maximum 1",
                "Member 'operator +' declares 2 parameters, exceeding configured maximum 1",
                "Member 'Local' declares 2 parameters, exceeding configured maximum 1",
                "Member 'lambda' declares 2 parameters, exceeding configured maximum 1",
                "Member 'anonymous method' declares 2 parameters, exceeding configured maximum 1"
            ],
            messages.ToArray());
    }

    [Fact]
    public async Task Captured_variables_do_not_count_as_lambda_parameters()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    void M()
                    {
                        int factor = 10;
                        System.Func<int, int> lambda = x => x * factor;
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1")));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
    }

    [Fact]
    public async Task Accessor_implicit_value_parameters_do_not_count()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                using System;

                public sealed class Sample
                {
                    public int Count
                    {
                        get { return 1; }
                        set { _ = value; }
                    }

                    public int InitOnly
                    {
                        init { _ = value; }
                    }

                    public event Action Changed
                    {
                        add { _ = value; }
                        remove { _ = value; }
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "0")));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
    }

    [Fact]
    public async Task Indexer_accessors_count_explicit_index_parameters_only()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    public string this[int index, string key]
                    {
                        get { return key; }
                        set { _ = value; }
                    }
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1")));

        ImmutableArray<Diagnostic> parameterDiagnostics =
        [
            .. diagnostics
                .Where(IsParameterCountDiagnostic)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
        ];

        Assert.Collection(
            parameterDiagnostics,
            diagnostic =>
            {
                Assert.Equal("Member 'this[].get' declares 2 parameters, exceeding configured maximum 1", diagnostic.GetMessage(CultureInfo.InvariantCulture));
                AssertDiagnosticText(diagnostic, "get");
            },
            diagnostic =>
            {
                Assert.Equal("Member 'this[].set' declares 2 parameters, exceeding configured maximum 1", diagnostic.GetMessage(CultureInfo.InvariantCulture));
                AssertDiagnosticText(diagnostic, "set");
            });
    }

    [Fact]
    public async Task Primary_constructor_is_deferred_and_does_not_report()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Customer(string name, int age)
                {
                    public string Name { get; } = name;
                }
                """),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "0")));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
    }

    [Fact]
    public async Task Generated_code_is_excluded_from_parameter_count_diagnostics()
    {
        SyntaxTree generatedTree = Parse(
            """
            // <auto-generated/>
            public sealed class GeneratedSample
            {
                void M(int left, int right)
                {
                }
            }
            """,
            "GeneratedSample.g.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            generatedTree,
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "0")));

        Assert.DoesNotContain(diagnostics, IsParameterCountDiagnostic);
    }

    [Fact]
    public async Task Severity_uses_standard_roslyn_override()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(ParameterizedSource()),
            globalOptions: Options((ComplexityAnalyzerOptionsReader.MaximumParametersKey, "2")),
            parameterCountDiagnosticReport: ReportDiagnostic.Warn);

        Diagnostic diagnostic = Assert.Single(diagnostics, IsParameterCountDiagnostic);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        SyntaxTree syntaxTree,
        AnalyzerConfigOptions? globalOptions = null,
        ReportDiagnostic? parameterCountDiagnosticReport = null)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ParameterCountDiagnosticTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: CreateSpecificDiagnosticOptions(parameterCountDiagnosticReport)));
        AnalyzerOptions analyzerOptions = new(
            [],
            new TestAnalyzerConfigOptionsProvider(globalOptions ?? Options()));

        return await compilation
            .WithAnalyzers([new ComplexityAnalyzer()], analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableDictionary<string, ReportDiagnostic> CreateSpecificDiagnosticOptions(
        ReportDiagnostic? parameterCountDiagnosticReport)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder builder =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (parameterCountDiagnosticReport.HasValue)
        {
            builder.Add(ParameterCountExceedsConfiguredThresholdId, parameterCountDiagnosticReport.Value);
        }

        return builder.ToImmutable();
    }

    private static string ParameterizedSource()
    {
        return """
            public sealed class Sample
            {
                void M(int left, int right, int third)
                {
                }
            }
            """;
    }

    private static SyntaxTree Parse(string source, string path = "Sample.cs")
    {
        return CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Parse, SourceCodeKind.Regular),
            path: path);
    }

    private static bool IsParameterCountDiagnostic(Diagnostic diagnostic)
    {
        return diagnostic.Id == ParameterCountExceedsConfiguredThresholdId;
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
        private readonly AnalyzerConfigOptions globalOptions;

        internal TestAnalyzerConfigOptionsProvider(AnalyzerConfigOptions globalOptions)
        {
            this.globalOptions = globalOptions;
        }

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return globalOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return globalOptions;
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

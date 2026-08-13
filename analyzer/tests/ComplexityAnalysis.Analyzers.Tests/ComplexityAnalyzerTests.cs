using System;
using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityAnalyzerTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string LinearLookupInsideIterationId = "BIG1001";
    private const string MaterializationInsideIterationId = "BIG1002";
    private const string OrderingInsideIterationId = "BIG1003";
    private const string AnalyzerExecutionProbeId = "BIG9000";

    [Fact]
    public void Analyzer_can_be_instantiated()
    {
        DiagnosticAnalyzer analyzer = new ComplexityAnalyzer();

        ComplexityAnalyzer typedAnalyzer = Assert.IsType<ComplexityAnalyzer>(analyzer);
        Assert.NotNull(typedAnalyzer);
    }

    [Fact]
    public void SupportedDiagnostics_contains_estimated_complexity_and_the_phase_one_probe()
    {
        var analyzer = new ComplexityAnalyzer();

        Assert.Equal(
            [
                EstimatedAlgorithmicComplexityId,
                LinearLookupInsideIterationId,
                MaterializationInsideIterationId,
                OrderingInsideIterationId,
                AnalyzerExecutionProbeId
            ],
            analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id));
    }

    [Fact]
    public void EstimatedAlgorithmicComplexity_has_the_expected_public_descriptor_metadata()
    {
        DiagnosticDescriptor descriptor = new ComplexityAnalyzer()
            .SupportedDiagnostics
            .Single(descriptor => descriptor.Id == EstimatedAlgorithmicComplexityId);

        Assert.Equal(EstimatedAlgorithmicComplexityId, descriptor.Id);
        Assert.Equal("Estimated algorithmic complexity", descriptor.Title.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("Complexity", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.False(descriptor.IsEnabledByDefault);
    }

    [Theory]
    [InlineData(LinearLookupInsideIterationId, "Linear lookup inside iteration")]
    [InlineData(MaterializationInsideIterationId, "Materialization inside iteration")]
    [InlineData(OrderingInsideIterationId, "Ordering inside iteration")]
    public void Actionable_diagnostics_have_expected_public_descriptor_metadata(
        string diagnosticId,
        string expectedTitle)
    {
        DiagnosticDescriptor descriptor = new ComplexityAnalyzer()
            .SupportedDiagnostics
            .Single(descriptor => descriptor.Id == diagnosticId);

        Assert.Equal(diagnosticId, descriptor.Id);
        Assert.Equal(expectedTitle, descriptor.Title.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("Complexity", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public void AnalyzerExecutionProbe_has_the_expected_public_descriptor_metadata()
    {
        DiagnosticDescriptor descriptor = new ComplexityAnalyzer()
            .SupportedDiagnostics
            .Single(descriptor => descriptor.Id == AnalyzerExecutionProbeId);

        Assert.Equal(AnalyzerExecutionProbeId, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.False(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public async Task Analyzer_does_not_report_estimated_complexity_when_it_is_not_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
    }

    [Theory]
    [InlineData(
        """
        public sealed class Sample
        {
            public int M() => 42;
        }
        """,
        "Estimated time complexity: O(1)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public void M(int[] values)
            {
                foreach (var value in values)
                {
                    var x = value + 1;
                }
            }
        }
        """,
        "Estimated time complexity: O(n)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public void M(int[] values)
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
        """,
        "Estimated time complexity: O(n\u00b2)")]
    public async Task Analyzer_reports_estimated_complexity_when_explicitly_enabled(
        string source,
        string expectedMessage)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            source,
            enableComplexity: true);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.True(diagnostic.Location.IsInSource);

        SyntaxTree sourceTree = diagnostic.Location.SourceTree
            ?? throw new System.InvalidOperationException("Expected a source location.");
        string diagnosticText = sourceTree
            .GetText()
            .GetSubText(diagnostic.Location.SourceSpan)
            .ToString();
        Assert.Equal("M", diagnosticText);
    }

    [Fact]
    public async Task Analyzer_does_not_report_estimated_complexity_for_unknown_methods()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public interface CustomCollection
            {
                bool Probe(int value);
            }

            public sealed class Sample
            {
                public bool M(CustomCollection values) => values.Probe(42);
            }
            """,
            enableComplexity: true);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
    }

    [Fact]
    public async Task Big1001_reports_list_contains_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> customers, List<int> blockedCustomers)
                {
                    foreach (var customer in customers)
                    {
                        if (blockedCustomers.Contains(customer))
                        {
                        }
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == LinearLookupInsideIterationId);

        Assert.Equal(
            "Linear lookup 'List<T>.Contains' is executed inside an iteration estimated as O(n). Estimated combined complexity: O(n \u00b7 m). Consider an indexed lookup when appropriate.",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(diagnostic, "blockedCustomers.Contains(customer)");
    }

    [Fact]
    public async Task Big1001_does_not_report_list_contains_outside_loop()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> blockedCustomers) => blockedCustomers.Contains(42);
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LinearLookupInsideIterationId);
    }

    [Fact]
    public async Task Big1001_does_not_report_hashset_contains_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> customers, HashSet<int> blockedCustomers)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LinearLookupInsideIterationId);
    }

    [Fact]
    public async Task Big1001_does_not_report_custom_contains_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class CustomCollection
            {
                public bool Contains(int value) => true;
            }

            public sealed class Sample
            {
                void M(List<int> customers, CustomCollection blockedCustomers)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LinearLookupInsideIterationId);
    }

    [Fact]
    public async Task Big1001_reports_combined_complexity_for_two_independent_dimensions()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> left, List<int> right)
                {
                    foreach (var value in left)
                    {
                        _ = right.Contains(value);
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == LinearLookupInsideIterationId);

        Assert.Contains("Estimated combined complexity: O(n \u00b7 m).", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Big1002_reports_to_list_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        var copy = items.ToList();
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == MaterializationInsideIterationId);

        Assert.Equal(
            "Materialization 'Enumerable.ToList' is executed inside an iteration estimated as O(n), repeatedly enumerating the source and allocating results. Estimated combined complexity: O(n \u00b7 m).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(diagnostic, "items.ToList()");
    }

    [Fact]
    public async Task Big1002_does_not_report_to_list_outside_loop()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> items) => items.ToList();
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MaterializationInsideIterationId);
    }

    [Fact]
    public async Task Big1002_does_not_report_custom_to_list_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class CustomCollection
            {
                public CustomCollection ToList() => this;
            }

            public sealed class Sample
            {
                void M(List<int> customers, CustomCollection items)
                {
                    foreach (var customer in customers)
                    {
                        var copy = items.ToList();
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MaterializationInsideIterationId);
    }

    [Fact]
    public async Task Big1003_reports_orderby_consumed_inside_foreach_body()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        var sorted = items.OrderBy(item => item).ToList();
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == OrderingInsideIterationId);

        Assert.Equal(
            "Ordering 'Enumerable.OrderBy' is consumed inside an iteration estimated as O(n). Estimated combined complexity: O(n \u00b7 m log m).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(diagnostic, "items.OrderBy(item => item)");
    }

    [Fact]
    public async Task Big1003_does_not_report_deferred_orderby_without_consumption()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        var query = items.OrderBy(item => item);
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == OrderingInsideIterationId);
    }

    [Fact]
    public async Task Big1003_does_not_report_orderby_consumed_outside_loop()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(List<int> customers, IEnumerable<int> items)
                {
                    var query = items.OrderBy(item => item);
                    foreach (var customer in customers)
                    {
                        var current = customer;
                    }

                    return query.ToList();
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == OrderingInsideIterationId);
    }

    [Fact]
    public async Task Actionable_diagnostics_coexist_without_duplicate_reports()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, List<int> blockedCustomers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                        var sorted = items.OrderBy(item => item).ToList();
                    }
                }
            }
            """);

        Assert.Equal(1, diagnostics.Count(diagnostic => diagnostic.Id == LinearLookupInsideIterationId));
        Assert.Equal(1, diagnostics.Count(diagnostic => diagnostic.Id == MaterializationInsideIterationId));
        Assert.Equal(1, diagnostics.Count(diagnostic => diagnostic.Id == OrderingInsideIterationId));
    }

    [Fact]
    public async Task Analyzer_diagnostics_are_deterministic_for_repeated_source()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                public int Constant() => 42;

                void M(List<int> customers, List<int> blockedCustomers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                        var sorted = items.OrderBy(item => item).ToList();
                    }
                }
            }
            """;
        ImmutableArray<string>? expected = null;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
                source,
                enableProbe: true,
                enableComplexity: true);
            ImmutableArray<string> actual =
            [
                .. diagnostics
                .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .Select(FormatDeterministicDiagnostic)
            ];

            expected ??= actual;

            Assert.Equal(expected, actual);
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(EstimatedAlgorithmicComplexityId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(LinearLookupInsideIterationId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(MaterializationInsideIterationId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(OrderingInsideIterationId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(AnalyzerExecutionProbeId + "|", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Analyzer_does_not_report_the_probe_when_it_is_not_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
    }

    [Fact]
    public async Task Analyzer_reports_exactly_one_probe_per_compilation_when_explicitly_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """,
            enableProbe: true);

        Diagnostic diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyzer_reports_the_probe_at_a_source_location_when_source_is_available()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """,
            enableProbe: true);

        Diagnostic diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);

        Assert.True(diagnostic.Location.IsInSource);
    }

    [Fact]
    public async Task Analyzer_reports_only_one_probe_for_code_with_multiple_methods()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int First() => 1;
                public int Second() => 2;
                public int Third() => First() + Second();
            }
            """,
            enableProbe: true);

        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
    }

    [Fact]
    public async Task Generated_code_does_not_change_probe_emission_behavior()
    {
        ImmutableArray<SyntaxTree> syntaxTrees =
        [
            Parse(
                """
                public sealed class UserCode
                {
                    public int M() => 42;
                }
                """),
            Parse(
                """
                // <auto-generated/>
                public sealed class GeneratedCode
                {
                    public int First() => 1;
                    public int Second() => 2;
                }
                """,
                "Generated.g.cs")
        ];

        ImmutableArray<Diagnostic> disabledDiagnostics = await GetAnalyzerDiagnosticsAsync(syntaxTrees);
        ImmutableArray<Diagnostic> enabledDiagnostics = await GetAnalyzerDiagnosticsAsync(syntaxTrees, enableProbe: true);

        Assert.DoesNotContain(disabledDiagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
        _ = Assert.Single(enabledDiagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
    }

    [Fact]
    public async Task Analyzer_runs_on_a_valid_csharp_compilation_without_throwing()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public string M() => nameof(Sample);
            }
            """);

        Assert.Empty(diagnostics);
    }

    private static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        bool enableProbe = false,
        bool enableComplexity = false)
    {
        return GetAnalyzerDiagnosticsAsync([Parse(source)], enableProbe, enableComplexity);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        ImmutableArray<SyntaxTree> syntaxTrees,
        bool enableProbe = false,
        bool enableComplexity = false)
    {
        CSharpCompilation compilation = CreateCompilation(syntaxTrees, enableProbe, enableComplexity);
        var analyzer = new ComplexityAnalyzer();
        ImmutableArray<DiagnosticAnalyzer> analyzers = [analyzer];
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(
        ImmutableArray<SyntaxTree> syntaxTrees,
        bool enableProbe,
        bool enableComplexity)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder specificDiagnosticOptions =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (enableProbe)
        {
            specificDiagnosticOptions.Add(AnalyzerExecutionProbeId, ReportDiagnostic.Info);
        }

        if (enableComplexity)
        {
            specificDiagnosticOptions.Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info);
        }

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            specificDiagnosticOptions: specificDiagnosticOptions.ToImmutable());

        return CSharpCompilation.Create(
            assemblyName: "AnalyzerInfrastructureTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: compilationOptions);
    }

    private static SyntaxTree Parse(string source, string path = "Sample.cs")
    {
        return CSharpSyntaxTree.ParseText(source, path: path);
    }

    private static void AssertDiagnosticText(Diagnostic diagnostic, string expectedText)
    {
        SyntaxTree sourceTree = diagnostic.Location.SourceTree
            ?? throw new System.InvalidOperationException("Expected a source location.");
        string diagnosticText = sourceTree
            .GetText()
            .GetSubText(diagnostic.Location.SourceSpan)
            .ToString();

        Assert.Equal(expectedText, diagnosticText);
    }

    private static string FormatDeterministicDiagnostic(Diagnostic diagnostic)
    {
        SyntaxTree sourceTree = diagnostic.Location.SourceTree
            ?? throw new System.InvalidOperationException("Expected a source location.");
        string diagnosticText = sourceTree
            .GetText()
            .GetSubText(diagnostic.Location.SourceSpan)
            .ToString();

        return string.Join(
            "|",
            diagnostic.Id,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            diagnostic.Location.SourceSpan.Start.ToString(CultureInfo.InvariantCulture),
            diagnostic.Location.SourceSpan.Length.ToString(CultureInfo.InvariantCulture),
            diagnosticText);
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } = CreateTrustedPlatformReferences();

    private static ImmutableArray<MetadataReference> CreateTrustedPlatformReferences()
    {
        string trustedPlatformAssemblies =
            (string?)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? string.Empty;

        return
        [
            .. trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }
}

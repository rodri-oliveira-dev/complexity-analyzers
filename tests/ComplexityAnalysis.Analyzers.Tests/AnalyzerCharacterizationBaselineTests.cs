using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Diagnostics;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class AnalyzerCharacterizationBaselineTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string LinearLookupInsideIterationId = "BIG1001";
    private const string MaterializationInsideIterationId = "BIG1002";
    private const string OrderingInsideIterationId = "BIG1003";
    private const string InputDependentCallInsideIterationId = "BIG1004";
    private const string ExponentialRecursiveGrowthId = "BIG1005";
    private const string MethodComplexityExceedsConfiguredThresholdId = "BIG1006";
    private const string AnalyzerExecutionProbeId = "BIG9000";

    [Fact]
    public void Diagnostic_descriptor_baseline_preserves_current_public_contract()
    {
        DiagnosticDescriptor[] descriptors = [.. new ComplexityAnalyzer().SupportedDiagnostics];

        DescriptorCase[] expected =
        [
            new(EstimatedAlgorithmicComplexityId, "Estimated algorithmic complexity", "Complexity", DiagnosticSeverity.Info, false),
            new(LinearLookupInsideIterationId, "Linear lookup inside iteration", "Complexity", DiagnosticSeverity.Info, true),
            new(MaterializationInsideIterationId, "Materialization inside iteration", "Complexity", DiagnosticSeverity.Info, true),
            new(OrderingInsideIterationId, "Ordering inside iteration", "Complexity", DiagnosticSeverity.Info, true),
            new(InputDependentCallInsideIterationId, "Input-dependent method call inside iteration", "Complexity", DiagnosticSeverity.Info, true),
            new(ExponentialRecursiveGrowthId, "Exponential recursive growth", "Complexity", DiagnosticSeverity.Info, true),
            new(MethodComplexityExceedsConfiguredThresholdId, "Method complexity exceeds configured threshold", "Complexity", DiagnosticSeverity.Info, true),
            new(AnalyzerExecutionProbeId, "Analyzer execution probe", "Infrastructure", DiagnosticSeverity.Info, false)
        ];

        Assert.Equal(expected.Select(testCase => testCase.Id), descriptors.Select(descriptor => descriptor.Id));
        foreach (DescriptorCase testCase in expected)
        {
            DiagnosticDescriptor descriptor = Assert.Single(
                descriptors,
                descriptor => descriptor.Id == testCase.Id);

            Assert.Equal(testCase.Title, descriptor.Title.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(testCase.Category, descriptor.Category);
            Assert.Equal(testCase.DefaultSeverity, descriptor.DefaultSeverity);
            Assert.Equal(testCase.IsEnabledByDefault, descriptor.IsEnabledByDefault);
        }
    }

    [Theory]
    [MemberData(nameof(BasicAndIterationEstimateCases))]
    public async Task Analyzer_estimate_baseline_covers_basic_and_iteration_shapes(
        string scenario,
        string source,
        string expectedMessage)
    {
        _ = expectedMessage ?? throw new ArgumentNullException(nameof(expectedMessage));

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(source),
            enableEstimatedComplexity: true);

        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == "M");

        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(
            diagnostic,
            DiagnosticPropertyNames.Complexity,
            expectedMessage[(expectedMessage.IndexOf(" is ", StringComparison.Ordinal) + " is ".Length)..]);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        _ = scenario;
    }

    [Theory]
    [MemberData(nameof(KnownOperationEstimateCases))]
    public void Known_operation_estimate_matrix_covers_current_bcl_and_linq_subset(
        string scenario,
        string source,
        string expectedComplexity)
    {
        ComplexityExpression complexity = AnalyzeMethod(source);

        Assert.Equal(expectedComplexity, complexity.ToBigONotation());
        _ = scenario;
    }

    [Theory]
    [MemberData(nameof(CustomSameNameOperationCases))]
    public void Custom_same_name_operations_are_not_classified_as_bcl_or_linq_by_name(
        string scenario,
        string source,
        string expectedComplexity,
        string expectedContainingType)
    {
        MethodFacts facts = CreateMethodFacts(source);
        InvocationExpressionSyntax invocation = facts.MethodDeclaration
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single();
        IMethodSymbol methodSymbol = facts.SemanticModel
            .GetSymbolInfo(invocation, CancellationToken.None)
            .Symbol as IMethodSymbol
            ?? throw new InvalidOperationException("Expected invocation to resolve to a method symbol.");
        var resolver = new KnownOperationResolver(KnownOperationRegistry.Default);

        ComplexityExpression complexity = MethodComplexityExtractor.AnalyzeMethod(
            facts.MethodDeclaration,
            facts.SemanticModel,
            CancellationToken.None);

        Assert.Equal(expectedComplexity, complexity.ToBigONotation());
        Assert.Equal(
            expectedContainingType,
            methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
        Assert.False(resolver.TryResolve(methodSymbol, CancellationToken.None, out _));
        _ = scenario;
    }

    [Fact]
    public async Task Generated_code_is_excluded_from_method_diagnostics_even_when_rules_are_enabled()
    {
        SyntaxTree generatedTree = Parse(
            """
            // <auto-generated/>
            using System.Collections.Generic;

            public sealed class GeneratedSample
            {
                void M(List<int> values)
                {
                    foreach (var value in values)
                    {
                        _ = values.Contains(value);
                    }
                }
            }
            """,
            "GeneratedSample.g.cs");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            generatedTree,
            enableEstimatedComplexity: true,
            threshold: "constant");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LinearLookupInsideIterationId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MethodComplexityExceedsConfiguredThresholdId);
    }

    [Fact]
    public void Analyzer_callback_path_passes_cancellation_to_method_extractor()
    {
        MethodFacts facts = CreateMethodFacts(
            """
            public sealed class Sample
            {
                int M() => 42;
            }
            """);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            MethodComplexityExtractor.AnalyzeMethod(
                facts.MethodDeclaration,
                facts.SemanticModel,
                cancellationTokenSource.Token));
    }

    public static TheoryData<string, string, string> BasicAndIterationEstimateCases
    {
        get;
    } = new()
    {
        {
            "empty block",
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """,
            "Estimated algorithmic complexity for 'M' is O(1)"
        },
        {
            "straight-line block",
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    var value = left + right;
                    value *= 2;
                    return value;
                }
            }
            """,
            "Estimated algorithmic complexity for 'M' is O(1)"
        },
        {
            "expression-bodied method",
            """
            public sealed class Sample
            {
                int M(int value) => value + 1;
            }
            """,
            "Estimated algorithmic complexity for 'M' is O(1)"
        },
        {
            "single loop",
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
            """,
            "Estimated algorithmic complexity for 'M' is O(n)"
        },
        {
            "nested loops over same input",
            """
            public sealed class Sample
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
            """,
            "Estimated algorithmic complexity for 'M' is O(n\u00b2)"
        },
        {
            "independent sequential loops",
            """
            public sealed class Sample
            {
                void M(int[] left, int[] right)
                {
                    foreach (var item in left)
                    {
                        var x = item + 1;
                    }

                    foreach (var item in right)
                    {
                        var y = item + 1;
                    }
                }
            }
            """,
            "Estimated algorithmic complexity for 'M' is O(n + m)"
        },
        {
            "independent nested loops",
            """
            public sealed class Sample
            {
                void M(int[] left, int[] right)
                {
                    foreach (var outer in left)
                    {
                        foreach (var inner in right)
                        {
                            var x = outer + inner;
                        }
                    }
                }
            }
            """,
            "Estimated algorithmic complexity for 'M' is O(n \u00b7 m)"
        }
    };

    public static TheoryData<string, string, string> KnownOperationEstimateCases
    {
        get;
    } = new()
    {
        {
            "List<T>.Count",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                int M(List<int> values) => values.Count;
            }
            """,
            "O(1)"
        },
        {
            "List<T> indexer",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                int M(List<int> values) => values[0];
            }
            """,
            "O(1)"
        },
        {
            "List<T>.Contains",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> values) => values.Contains(42);
            }
            """,
            "O(n)"
        },
        {
            "List<T>.IndexOf",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                int M(List<int> values) => values.IndexOf(42);
            }
            """,
            "O(n)"
        },
        {
            "List<T>.Sort",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> values) => values.Sort();
            }
            """,
            "O(n log n)"
        },
        {
            "Dictionary<TKey,TValue>.ContainsKey",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(Dictionary<int, string> values) => values.ContainsKey(42);
            }
            """,
            "O(1)"
        },
        {
            "Dictionary<TKey,TValue>.ContainsValue",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(Dictionary<int, string> values) => values.ContainsValue("needle");
            }
            """,
            "O(n)"
        },
        {
            "HashSet<T>.Contains",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(HashSet<int> values) => values.Contains(42);
            }
            """,
            "O(1)"
        },
        {
            "array Length",
            """
            public sealed class Sample
            {
                int M(int[] values) => values.Length;
            }
            """,
            "O(1)"
        },
        {
            "string Length",
            """
            public sealed class Sample
            {
                int M(string text) => text.Length;
            }
            """,
            "O(1)"
        },
        {
            "LINQ Any without predicate",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                bool M(IEnumerable<int> values) => values.Any();
            }
            """,
            "O(1)"
        },
        {
            "LINQ Any with predicate",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                bool M(IEnumerable<int> values) => values.Any(value => value > 0);
            }
            """,
            "O(n)"
        },
        {
            "LINQ All",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                bool M(IEnumerable<int> values) => values.All(value => value > 0);
            }
            """,
            "O(n)"
        },
        {
            "LINQ Contains",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                bool M(IEnumerable<int> values) => values.Contains(42);
            }
            """,
            "O(n)"
        },
        {
            "LINQ Count on IEnumerable<T>",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(IEnumerable<int> values) => values.Count();
            }
            """,
            "O(n)"
        },
        {
            "LINQ Count on ICollection<T>",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(ICollection<int> values) => values.Count();
            }
            """,
            "O(1)"
        },
        {
            "LINQ LongCount",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                long M(IEnumerable<int> values) => values.LongCount();
            }
            """,
            "O(n)"
        },
        {
            "LINQ ToList",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> values) => values.ToList();
            }
            """,
            "O(n)"
        },
        {
            "LINQ ToArray",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int[] M(IEnumerable<int> values) => values.ToArray();
            }
            """,
            "O(n)"
        },
        {
            "LINQ ToDictionary",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                Dictionary<int, int> M(IEnumerable<int> values) => values.ToDictionary(value => value);
            }
            """,
            "O(n)"
        },
        {
            "LINQ ToHashSet",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                HashSet<int> M(IEnumerable<int> values) => values.ToHashSet();
            }
            """,
            "O(n)"
        },
        {
            "LINQ Sum",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(IEnumerable<int> values) => values.Sum();
            }
            """,
            "O(n)"
        },
        {
            "LINQ Min",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(IEnumerable<int> values) => values.Min();
            }
            """,
            "O(n)"
        },
        {
            "LINQ Max",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(IEnumerable<int> values) => values.Max();
            }
            """,
            "O(n)"
        },
        {
            "LINQ Aggregate",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(IEnumerable<int> values) => values.Aggregate((left, right) => left + right);
            }
            """,
            "O(n)"
        },
        {
            "LINQ Where creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IEnumerable<int> M(IEnumerable<int> values) => values.Where(value => value > 0);
            }
            """,
            "O(1)"
        },
        {
            "LINQ Select creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IEnumerable<int> M(IEnumerable<int> values) => values.Select(value => value + 1);
            }
            """,
            "O(1)"
        },
        {
            "LINQ SelectMany creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IEnumerable<int> M(IEnumerable<int> outer, int[] inner) => outer.SelectMany(_ => inner);
            }
            """,
            "O(1)"
        },
        {
            "LINQ OrderBy creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IOrderedEnumerable<int> M(IEnumerable<int> values) => values.OrderBy(value => value);
            }
            """,
            "O(1)"
        },
        {
            "LINQ OrderByDescending creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IOrderedEnumerable<int> M(IEnumerable<int> values) => values.OrderByDescending(value => value);
            }
            """,
            "O(1)"
        },
        {
            "LINQ ThenBy creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IOrderedEnumerable<int> M(IEnumerable<int> values) => values.OrderBy(value => value).ThenBy(value => value);
            }
            """,
            "O(1)"
        },
        {
            "LINQ ThenByDescending creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IOrderedEnumerable<int> M(IEnumerable<int> values) => values.OrderBy(value => value).ThenByDescending(value => value);
            }
            """,
            "O(1)"
        },
        {
            "LINQ Distinct creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IEnumerable<int> M(IEnumerable<int> values) => values.Distinct();
            }
            """,
            "O(1)"
        },
        {
            "LINQ GroupBy creation",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                IEnumerable<IGrouping<int, int>> M(IEnumerable<int> values) => values.GroupBy(value => value);
            }
            """,
            "O(1)"
        },
        {
            "LINQ Where consumed by ToList",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> values) => values.Where(value => value > 0).ToList();
            }
            """,
            "O(n)"
        },
        {
            "LINQ OrderBy consumed by foreach",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(IEnumerable<int> values)
                {
                    foreach (var value in values.OrderBy(value => value))
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n log n)"
        },
        {
            "LINQ SelectMany consumed by ToList",
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> outer, int[] inner) => outer.SelectMany(_ => inner).ToList();
            }
            """,
            "O(n \u00b7 m)"
        }
    };

    public static TheoryData<string, string, string, string> CustomSameNameOperationCases
    {
        get;
    } = new()
    {
        {
            "custom Contains instance method",
            """
            public sealed class CustomCollection
            {
                public bool Contains(int value) => true;
            }

            public sealed class Sample
            {
                bool M(CustomCollection values) => values.Contains(42);
            }
            """,
            "O(1)",
            "CustomCollection"
        },
        {
            "custom Where extension method",
            """
            using System.Collections.Generic;

            namespace MyCompany
            {
                public static class QueryExtensions
                {
                    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, System.Func<T, bool> predicate) => source;
                }

                public sealed class Sample
                {
                    IEnumerable<int> M(IEnumerable<int> values) => values.Where(value => value > 0);
                }
            }
            """,
            "O(1)",
            "MyCompany.QueryExtensions"
        },
        {
            "custom ToList instance method",
            """
            public sealed class CustomSequence
            {
                public int ToList() => 1;
            }

            public sealed class Sample
            {
                int M(CustomSequence values) => values.ToList();
            }
            """,
            "O(1)",
            "CustomSequence"
        },
        {
            "custom Count extension method",
            """
            using System.Collections.Generic;

            namespace MyCompany
            {
                public static class QueryExtensions
                {
                    public static int Count<T>(this IEnumerable<T> source) => 1;
                }

                public sealed class Sample
                {
                    int M(IEnumerable<int> values) => values.Count();
                }
            }
            """,
            "O(1)",
            "MyCompany.QueryExtensions"
        }
    };

    private static ComplexityExpression AnalyzeMethod(string source)
    {
        MethodFacts facts = CreateMethodFacts(source);

        return MethodComplexityExtractor.AnalyzeMethod(
            facts.MethodDeclaration,
            facts.SemanticModel,
            CancellationToken.None);
    }

    private static MethodFacts CreateMethodFacts(string source)
    {
        SyntaxTree syntaxTree = Parse(source);
        CSharpCompilation compilation = CreateCompilation([syntaxTree]);
        AssertCompilationHasNoErrors(compilation);

        MethodDeclarationSyntax methodDeclaration = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, "M"));
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

        return new MethodFacts(methodDeclaration, semanticModel);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        SyntaxTree syntaxTree,
        bool enableEstimatedComplexity = false,
        string? threshold = null)
    {
        CSharpCompilation compilation = CreateCompilation(
            [syntaxTree],
            enableEstimatedComplexity,
            threshold);
        AssertCompilationHasNoErrors(compilation);

        AnalyzerOptions analyzerOptions = threshold is null
            ? new AnalyzerOptions([])
            : new AnalyzerOptions(
                [],
                new TestAnalyzerConfigOptionsProvider(
                    Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, threshold))));

        return await compilation
            .WithAnalyzers([new ComplexityAnalyzer()], analyzerOptions)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(
        ImmutableArray<SyntaxTree> syntaxTrees,
        bool enableEstimatedComplexity = false,
        string? threshold = null)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder diagnosticOptions =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (enableEstimatedComplexity)
        {
            diagnosticOptions.Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info);
        }

        if (threshold is not null)
        {
            diagnosticOptions.Add(MethodComplexityExceedsConfiguredThresholdId, ReportDiagnostic.Info);
        }

        return CSharpCompilation.Create(
            assemblyName: "AnalyzerCharacterizationBaselineTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: diagnosticOptions.ToImmutable()));
    }

    private static SyntaxTree Parse(string source, string path = "Sample.cs")
    {
        return CSharpSyntaxTree.ParseText(source, path: path);
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

    private static void AssertCompilationHasNoErrors(Compilation compilation)
    {
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];

        Assert.Empty(errors);
    }

    private static void AssertProperty(
        Diagnostic diagnostic,
        string key,
        string expectedValue)
    {
        Assert.True(diagnostic.Properties.TryGetValue(key, out string? actualValue));
        Assert.Equal(expectedValue, actualValue);
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

    private sealed record DescriptorCase(
        string Id,
        string Title,
        string Category,
        DiagnosticSeverity DefaultSeverity,
        bool IsEnabledByDefault);

    private sealed record MethodFacts(
        MethodDeclarationSyntax MethodDeclaration,
        SemanticModel SemanticModel);
}

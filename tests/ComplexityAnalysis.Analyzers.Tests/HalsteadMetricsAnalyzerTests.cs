using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class HalsteadMetricsAnalyzerTests
{
    [Theory]
    [MemberData(nameof(SupportedExecutableMemberCases))]
    public void Supported_executable_member_kinds_flow_to_halstead_metrics(
        string scenario,
        string source,
        string expectedKind,
        string expectedDisplayName)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMembers(facts)
            .Single(member =>
                StringComparer.Ordinal.Equals(member.Kind.ToString(), expectedKind)
                && StringComparer.Ordinal.Equals(member.DisplayName, expectedDisplayName));

        HalsteadMetrics metrics = Analyze(member, facts);

        Assert.True(metrics.Length > 0);
        Assert.True(metrics.Vocabulary > 0);
        AssertFinite(metrics);
        _ = scenario;
    }

    [Fact]
    public void Halstead_metrics_use_nested_executable_roots_independently()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M(int seed)
                {
                    int total = seed;
                    int Local(int value) => value + total;
                    total++;
                }
            }
            """);

        ImmutableArray<ExecutableMember> members = CreateMembers(facts);
        HalsteadMetrics parent = Analyze(members.Single(member => member.DisplayName == "M"), facts);
        HalsteadMetrics local = Analyze(members.Single(member => member.DisplayName == "Local"), facts);

        Assert.Equal(new HalsteadPrimitiveCounts(3, 5, 3, 6), parent.PrimitiveCounts);
        Assert.Equal(new HalsteadPrimitiveCounts(2, 2, 2, 2), local.PrimitiveCounts);
        AssertFinite(parent);
        AssertFinite(local);
    }

    [Fact]
    public void Bodyless_executable_member_does_not_produce_halstead_metrics()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public partial class Sample
            {
                partial void M();
            }
            """);
        ExecutableMember member = CreateMembers(facts).Single();

        bool analyzed = HalsteadMetricsAnalyzer.TryAnalyze(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out HalsteadMetrics metrics);

        Assert.False(analyzed);
        Assert.Equal(default, metrics);
    }

    [Fact]
    public void Already_canceled_token_stops_halstead_metrics_analysis()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M()
                {
                    int value = 1;
                }
            }
            """);
        ExecutableMember member = CreateMembers(facts).Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            HalsteadMetricsAnalyzer.TryAnalyze(
                member,
                facts.SemanticModel,
                cancellation.Token,
                out _));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Public_analyzer_contract_does_not_expose_halstead_threshold_diagnostic()
    {
        ImmutableArray<DiagnosticDescriptor> descriptors = new ComplexityAnalyzer().SupportedDiagnostics;

        Assert.DoesNotContain(descriptors, descriptor => descriptor.Id == "BIG2008");
        Assert.DoesNotContain(descriptors, descriptor => descriptor.Title.ToString(CultureInfo.InvariantCulture).Contains(
            "Halstead",
            StringComparison.Ordinal));

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            Parse(
                """
                public sealed class Sample
                {
                    int M(int left, int right)
                    {
                        int sum = left + right;
                        return sum;
                    }
                }
                """));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "BIG2008");
    }

    public static TheoryData<string, string, string, string> SupportedExecutableMemberCases
    {
        get;
    } = new()
    {
        {
            "ordinary method",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    return value + 1;
                }
            }
            """,
            nameof(ExecutableMemberKind.OrdinaryMethod),
            "M"
        },
        {
            "constructor",
            """
            public sealed class Sample
            {
                private int value;

                public Sample(int seed)
                {
                    value = seed + 1;
                }
            }
            """,
            nameof(ExecutableMemberKind.Constructor),
            "Sample.ctor"
        },
        {
            "accessor",
            """
            public sealed class Sample
            {
                private int value;

                public int Value
                {
                    get
                    {
                        return value + 1;
                    }
                }
            }
            """,
            nameof(ExecutableMemberKind.Accessor),
            "Value.get"
        },
        {
            "setter accessor",
            """
            public sealed class Sample
            {
                private int value;

                public int Value
                {
                    set
                    {
                        value = value + 1;
                    }
                }
            }
            """,
            nameof(ExecutableMemberKind.Accessor),
            "Value.set"
        },
        {
            "init accessor",
            """
            public sealed class Sample
            {
                private int value;

                public int Value
                {
                    get => value;
                    init
                    {
                        value = value + 1;
                    }
                }
            }
            """,
            nameof(ExecutableMemberKind.Accessor),
            "Value.set"
        },
        {
            "event add accessor",
            """
            using System;

            public sealed class Sample
            {
                private Action? handlers;

                public event Action Changed
                {
                    add
                    {
                        handlers += value;
                    }
                    remove
                    {
                        handlers -= value;
                    }
                }
            }
            """,
            nameof(ExecutableMemberKind.Accessor),
            "Changed.add"
        },
        {
            "event remove accessor",
            """
            using System;

            public sealed class Sample
            {
                private Action? handlers;

                public event Action Changed
                {
                    add
                    {
                        handlers += value;
                    }
                    remove
                    {
                        handlers -= value;
                    }
                }
            }
            """,
            nameof(ExecutableMemberKind.Accessor),
            "Changed.remove"
        },
        {
            "operator",
            """
            public sealed class Sample
            {
                private readonly int value;

                public Sample(int value)
                {
                    this.value = value;
                }

                public static Sample operator +(Sample left, Sample right)
                {
                    return new Sample(left.value + right.value);
                }
            }
            """,
            nameof(ExecutableMemberKind.Operator),
            "operator +"
        },
        {
            "conversion operator",
            """
            public sealed class Sample
            {
                private readonly int value;

                public Sample(int value)
                {
                    this.value = value;
                }

                public static explicit operator int(Sample sample)
                {
                    return sample.value + 1;
                }
            }
            """,
            nameof(ExecutableMemberKind.ConversionOperator),
            "explicit operator int"
        },
        {
            "local function",
            """
            public sealed class Sample
            {
                void M()
                {
                    int Local(int value)
                    {
                        return value + 1;
                    }
                }
            }
            """,
            nameof(ExecutableMemberKind.LocalFunction),
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
                    Func<int, int> lambda = value => value + 1;
                }
            }
            """,
            nameof(ExecutableMemberKind.Lambda),
            "lambda"
        },
        {
            "anonymous method",
            """
            using System;

            public sealed class Sample
            {
                void M()
                {
                    Func<int, int> anonymous = delegate (int value)
                    {
                        return value + 1;
                    };
                }
            }
            """,
            nameof(ExecutableMemberKind.AnonymousMethod),
            "anonymous method"
        },
        {
            "expression-bodied property",
            """
            public sealed class Sample
            {
                private int value;

                public int Value => value + 1;
            }
            """,
            nameof(ExecutableMemberKind.ExpressionBodiedProperty),
            "Value.get"
        },
    };

    private static HalsteadMetrics Analyze(
        ExecutableMember member,
        CompilationFacts facts)
    {
        bool analyzed = HalsteadMetricsAnalyzer.TryAnalyze(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out HalsteadMetrics metrics);

        Assert.True(analyzed);
        return metrics;
    }

    private static ImmutableArray<ExecutableMember> CreateMembers(CompilationFacts facts)
    {
        ImmutableArray<ExecutableMember>.Builder members = ImmutableArray.CreateBuilder<ExecutableMember>();
        foreach (SyntaxNode node in facts.SyntaxTree.GetRoot().DescendantNodes())
        {
            if (ExecutableMember.TryCreate(
                node,
                facts.SemanticModel,
                CancellationToken.None,
                out ExecutableMember? member)
                && member is not null)
            {
                members.Add(member);
            }
        }

        return members.ToImmutable();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(SyntaxTree syntaxTree)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "HalsteadMetricsAnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: ImmutableDictionary<string, ReportDiagnostic>.Empty.Add(
                    "BIG2008",
                    ReportDiagnostic.Info)));
        AssertCompilationHasNoErrors(compilation);

        return await compilation
            .WithAnalyzers([new ComplexityAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }

    private static SyntaxTree Parse(string source, string path = "Sample.cs")
    {
        return CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Parse, SourceCodeKind.Regular),
            path);
    }

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = Parse(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "HalsteadMetricsAnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertCompilationHasNoErrors(compilation);

        return new CompilationFacts(syntaxTree, compilation.GetSemanticModel(syntaxTree));
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

    private static void AssertFinite(HalsteadMetrics metrics)
    {
        AssertFinite(metrics.CalculatedLength);
        AssertFinite(metrics.Volume);
        AssertFinite(metrics.Difficulty);
        AssertFinite(metrics.Effort);
        AssertFinite(metrics.EstimatedImplementationTime);
        AssertFinite(metrics.EstimatedDeliveredBugs);
    }

    private static void AssertFinite(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
        Assert.True(value >= 0.0);
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
            .. trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record CompilationFacts(
        SyntaxTree SyntaxTree,
        SemanticModel SemanticModel);
}

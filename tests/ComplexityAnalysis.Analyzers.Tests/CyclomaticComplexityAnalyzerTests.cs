using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class CyclomaticComplexityAnalyzerTests
{
    [Theory]
    [MemberData(nameof(StandardDecisionPointCases))]
    public void Standard_mode_counts_documented_decision_points(
        string scenario,
        string source,
        int expected)
    {
        CyclomaticComplexityResult result = AnalyzeMethod(
            source,
            CyclomaticComplexityAnalysisMode.Standard);

        Assert.Equal(expected, result.Value);
        _ = scenario;
    }

    [Theory]
    [MemberData(nameof(SwitchConventionCases))]
    public void Modified_mccabe_changes_only_switch_family_counting(
        string scenario,
        string source,
        int expectedStandard,
        int expectedModified)
    {
        CyclomaticComplexityResult standard = AnalyzeMethod(
            source,
            CyclomaticComplexityAnalysisMode.Standard);
        CyclomaticComplexityResult modified = AnalyzeMethod(
            source,
            CyclomaticComplexityAnalysisMode.ModifiedMcCabe);

        Assert.Equal(expectedStandard, standard.Value);
        Assert.Equal(expectedModified, modified.Value);
        _ = scenario;
    }

    [Fact]
    public void Nested_executable_members_do_not_inflate_parent_metric()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            using System;

            public sealed class Sample
            {
                void M()
                {
                    void Local(int value)
                    {
                        if (value > 0)
                        {
                        }
                    }

                    Func<int, int> lambda = value => value > 0 ? value : -value;
                }
            }
            """);
        ExecutableMember member = CreateMethodMember(facts, "M");

        bool analyzed = new CyclomaticComplexityAnalyzer().TryAnalyze(
            member,
            CyclomaticComplexityAnalysisMode.Standard,
            CancellationToken.None,
            out CyclomaticComplexityResult result);

        Assert.True(analyzed);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void Bodyless_method_does_not_produce_a_metric_result()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public partial class Sample
            {
                partial void M();
            }
            """);
        ExecutableMember member = CreateMethodMember(facts, "M");

        bool analyzed = new CyclomaticComplexityAnalyzer().TryAnalyze(
            member,
            CyclomaticComplexityAnalysisMode.Standard,
            CancellationToken.None,
            out _);

        Assert.False(analyzed);
    }

    public static TheoryData<string, string, int> StandardDecisionPointCases
    {
        get;
    } = new()
    {
        {
            "straight-line block",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    return value * 2;
                }
            }
            """,
            1
        },
        {
            "if",
            """
            public sealed class Sample
            {
                void M(int value)
                {
                    if (value > 0)
                    {
                    }
                }
            }
            """,
            2
        },
        {
            "else-if chain",
            """
            public sealed class Sample
            {
                void M(int value)
                {
                    if (value > 0)
                    {
                    }
                    else if (value < 0)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """,
            3
        },
        {
            "loops",
            """
            public sealed class Sample
            {
                void M(int[] values, (int Left, int Right)[] pairs)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                    }

                    foreach (var value in values)
                    {
                    }

                    foreach (var (left, right) in pairs)
                    {
                    }

                    while (values.Length > 0)
                    {
                        break;
                    }

                    do
                    {
                    }
                    while (values.Length < 0);
                }
            }
            """,
            6
        },
        {
            "catch and catch filter",
            """
            public sealed class Sample
            {
                void M(System.Exception exception)
                {
                    try
                    {
                    }
                    catch (System.InvalidOperationException) when (exception.Message.Length > 0)
                    {
                    }
                    catch (System.Exception)
                    {
                    }
                }
            }
            """,
            4
        },
        {
            "conditional expression",
            """
            public sealed class Sample
            {
                int M(bool flag) => flag ? 1 : 0;
            }
            """,
            2
        },
        {
            "boolean short-circuit chain",
            """
            public sealed class Sample
            {
                void M(bool left, bool middle, bool right)
                {
                    if (left && middle || right)
                    {
                    }
                }
            }
            """,
            4
        },
        {
            "or pattern",
            """
            public sealed class Sample
            {
                void M(int value)
                {
                    if (value is 0 or 1)
                    {
                    }
                }
            }
            """,
            3
        },
    };

    public static TheoryData<string, string, int, int> SwitchConventionCases
    {
        get;
    } = new()
    {
        {
            "switch statement cases",
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
            """,
            3,
            2
        },
        {
            "switch statement pattern guard",
            """
            public sealed class Sample
            {
                int M(object value)
                {
                    switch (value)
                    {
                        case int number when number > 0:
                            return number;
                        default:
                            return 0;
                    }
                }
            }
            """,
            3,
            3
        },
        {
            "switch expression arms and guard",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    return value switch
                    {
                        0 => 0,
                        1 when value > 0 => 1,
                        _ => -1,
                    };
                }
            }
            """,
            4,
            3
        },
        {
            "default-only switch",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    switch (value)
                    {
                        default:
                            return -1;
                    }
                }
            }
            """,
            1,
            1
        },
    };

    private static CyclomaticComplexityResult AnalyzeMethod(
        string source,
        CyclomaticComplexityAnalysisMode mode)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMethodMember(facts, "M");

        bool analyzed = new CyclomaticComplexityAnalyzer().TryAnalyze(
            member,
            mode,
            CancellationToken.None,
            out CyclomaticComplexityResult result);

        Assert.True(analyzed);
        return result;
    }

    private static ExecutableMember CreateMethodMember(
        CompilationFacts facts,
        string methodName)
    {
        MethodDeclarationSyntax method = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == methodName);

        bool created = ExecutableMember.TryCreateOrdinaryMethod(
            method,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        return member;
    }

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "CyclomaticComplexityAnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        return new CompilationFacts(syntaxTree, compilation.GetSemanticModel(syntaxTree));
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

using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class MaximumNestingDepthAnalyzerTests
{
    [Theory]
    [MemberData(nameof(BaselineCases))]
    [MemberData(nameof(SingleConstructCases))]
    [MemberData(nameof(NestedConstructCases))]
    [MemberData(nameof(SiblingConstructCases))]
    [MemberData(nameof(ExcludedConstructCases))]
    public void Analyzer_reports_documented_maximum_nesting_depth(
        string scenario,
        string source,
        int expected)
    {
        MaximumNestingDepthResult result = AnalyzeMethod(source);

        Assert.Equal(expected, result.Value);
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
                void M(bool a)
                {
                    if (a)
                    {
                        void Local(bool b)
                        {
                            if (b)
                            {
                                while (b)
                                {
                                }
                            }
                        }

                        Func<bool, int> lambda = b => b ? 1 : 0;
                        Action anonymous = delegate
                        {
                            if (a)
                            {
                            }
                        };
                    }
                }
            }
            """);
        ExecutableMember member = CreateMethodMember(facts, "M");

        bool analyzed = new MaximumNestingDepthAnalyzer().TryAnalyze(
            member,
            CancellationToken.None,
            out MaximumNestingDepthResult result);

        Assert.True(analyzed);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void Lambda_root_is_analyzed_independently()
    {
        CompilationFacts facts = CreateCompilationFacts(
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
            """);
        LambdaExpressionSyntax lambda = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .Single();
        bool created = ExecutableMember.TryCreate(
            lambda,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        bool analyzed = new MaximumNestingDepthAnalyzer().TryAnalyze(
            member,
            CancellationToken.None,
            out MaximumNestingDepthResult result);

        Assert.True(analyzed);
        Assert.Equal(2, result.Value);
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

        bool analyzed = new MaximumNestingDepthAnalyzer().TryAnalyze(
            member,
            CancellationToken.None,
            out _);

        Assert.False(analyzed);
    }

    [Fact]
    public void Already_canceled_token_stops_analysis()
    {
        CompilationFacts facts = CreateCompilationFacts(
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
            """);
        ExecutableMember member = CreateMethodMember(facts, "M");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            new MaximumNestingDepthAnalyzer().TryAnalyze(
                member,
                cancellation.Token,
                out _));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    public static TheoryData<string, string, int> BaselineCases
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
            0
        },
        {
            "straight-line block",
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    var value = left + right;
                    return value * 2;
                }
            }
            """,
            0
        },
        {
            "plain lexical blocks",
            """
            public sealed class Sample
            {
                void M()
                {
                    {
                        {
                            {
                                var value = 1;
                            }
                        }
                    }
                }
            }
            """,
            0
        },
        {
            "initializers",
            """
            public sealed class Child
            {
                public int[] Values { get; set; } = [];
            }

            public sealed class Sample
            {
                object M()
                {
                    var value = new
                    {
                        Child = new Child
                        {
                            Values = new[] { 1, 2, 3 }
                        }
                    };

                    return value;
                }
            }
            """,
            0
        }
    };

    public static TheoryData<string, string, int> SingleConstructCases
    {
        get;
    } = new()
    {
        {
            "if",
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
            1
        },
        {
            "for",
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                    }
                }
            }
            """,
            1
        },
        {
            "foreach",
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    foreach (var value in values)
                    {
                    }
                }
            }
            """,
            1
        },
        {
            "foreach variable",
            """
            public sealed class Sample
            {
                void M((int Left, int Right)[] pairs)
                {
                    foreach (var (left, right) in pairs)
                    {
                    }
                }
            }
            """,
            1
        },
        {
            "while",
            """
            public sealed class Sample
            {
                void M(bool flag)
                {
                    while (flag)
                    {
                        break;
                    }
                }
            }
            """,
            1
        },
        {
            "do while",
            """
            public sealed class Sample
            {
                void M(bool flag)
                {
                    do
                    {
                    }
                    while (flag);
                }
            }
            """,
            1
        },
        {
            "switch statement",
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
            1
        },
        {
            "switch expression",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    return value switch
                    {
                        0 => 0,
                        _ => -1,
                    };
                }
            }
            """,
            1
        },
        {
            "try catch finally",
            """
            public sealed class Sample
            {
                void M()
                {
                    try
                    {
                    }
                    catch (System.Exception)
                    {
                    }
                    finally
                    {
                    }
                }
            }
            """,
            1
        },
        {
            "conditional expression",
            """
            public sealed class Sample
            {
                int M(bool flag) => flag ? 1 : 0;
            }
            """,
            1
        }
    };

    public static TheoryData<string, string, int> NestedConstructCases
    {
        get;
    } = new()
    {
        {
            "if in if",
            """
            public sealed class Sample
            {
                void M(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                        }
                    }
                }
            }
            """,
            2
        },
        {
            "if loop if",
            """
            public sealed class Sample
            {
                void M(bool a, bool b, int[] values)
                {
                    if (a)
                    {
                        foreach (var value in values)
                        {
                            if (b)
                            {
                            }
                        }
                    }
                }
            }
            """,
            3
        },
        {
            "switch case if",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    switch (value)
                    {
                        case 0:
                            if (value == 0)
                            {
                                return 1;
                            }

                            break;
                    }

                    return 0;
                }
            }
            """,
            2
        },
        {
            "try catch loop",
            """
            public sealed class Sample
            {
                void M(bool flag)
                {
                    try
                    {
                        if (flag)
                        {
                        }
                    }
                    catch (System.Exception)
                    {
                        while (flag)
                        {
                            break;
                        }
                    }
                }
            }
            """,
            2
        },
        {
            "else body child",
            """
            public sealed class Sample
            {
                void M(bool a, bool b)
                {
                    if (a)
                    {
                    }
                    else
                    {
                        while (b)
                        {
                            break;
                        }
                    }
                }
            }
            """,
            2
        },
        {
            "nested conditional expression",
            """
            public sealed class Sample
            {
                int M(bool a, bool b) => a ? b ? 1 : 2 : 3;
            }
            """,
            2
        },
        {
            "switch expression arm conditional",
            """
            public sealed class Sample
            {
                int M(int value, bool flag)
                {
                    return value switch
                    {
                        0 => flag ? 1 : 2,
                        _ => 3,
                    };
                }
            }
            """,
            2
        }
    };

    public static TheoryData<string, string, int> SiblingConstructCases
    {
        get;
    } = new()
    {
        {
            "flat if siblings",
            """
            public sealed class Sample
            {
                void M(bool a, bool b, bool c)
                {
                    if (a)
                    {
                    }

                    if (b)
                    {
                    }

                    if (c)
                    {
                    }
                }
            }
            """,
            1
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
                    else if (value == 0)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """,
            1
        },
        {
            "switch sections are siblings",
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
                        case 2:
                            return 2;
                        default:
                            return -1;
                    }
                }
            }
            """,
            1
        },
        {
            "catch clauses are siblings",
            """
            public sealed class Sample
            {
                void M()
                {
                    try
                    {
                    }
                    catch (System.ArgumentException)
                    {
                    }
                    catch (System.InvalidOperationException)
                    {
                    }
                    catch (System.Exception)
                    {
                    }
                }
            }
            """,
            1
        }
    };

    public static TheoryData<string, string, int> ExcludedConstructCases
    {
        get;
    } = new()
    {
        {
            "boolean expression chain inside if remains one if level",
            """
            public sealed class Sample
            {
                void M(bool a, bool b, bool c)
                {
                    if (a && b || c)
                    {
                    }
                }
            }
            """,
            1
        },
        {
            "patterns and guard remain switch branch level",
            """
            public sealed class Sample
            {
                int M(object value)
                {
                    switch (value)
                    {
                        case int number when number is > 0 and < 10:
                            return number;
                        default:
                            return 0;
                    }
                }
            }
            """,
            1
        },
        {
            "using lock fixed checked unchecked do not count by themselves",
            """
            public unsafe sealed class Sample
            {
                void M(object gate, int* pointer)
                {
                    using (var disposable = new Disposable())
                    {
                    }

                    lock (gate)
                    {
                    }

                    fixed (int* pinned = new[] { 1 })
                    {
                    }

                    checked
                    {
                        var value = 1 + 2;
                    }

                    unchecked
                    {
                        var value = 1 + 2;
                    }
                }

                private sealed class Disposable : System.IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
            """,
            0
        },
        {
            "comments and strings do not count",
            """
            public sealed class Sample
            {
                string M()
                {
                    // if (a) { for (;;) { } }
                    return "if while switch try";
                }
            }
            """,
            0
        }
    };

    private static MaximumNestingDepthResult AnalyzeMethod(string source)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMethodMember(facts, "M");

        bool analyzed = new MaximumNestingDepthAnalyzer().TryAnalyze(
            member,
            CancellationToken.None,
            out MaximumNestingDepthResult result);

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
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Parse, SourceCodeKind.Regular));
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "MaximumNestingDepthAnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
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

using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class CognitiveComplexityCalculatorTests
{
    [Theory]
    [MemberData(nameof(BaselineCases))]
    [MemberData(nameof(ControlFlowCases))]
    [MemberData(nameof(BooleanSequenceCases))]
    [MemberData(nameof(PatternAndGuardCases))]
    [MemberData(nameof(RecursionCases))]
    [MemberData(nameof(JumpAndExclusionCases))]
    public void Calculator_reports_documented_cognitive_complexity(
        string scenario,
        string source,
        int expected)
    {
        CognitiveComplexity result = AnalyzeMethod(source);

        Assert.Equal(expected, result.Value);
        _ = scenario;
    }

    [Fact]
    public void Nested_control_flow_scores_higher_than_flat_control_flow()
    {
        CognitiveComplexity flat = AnalyzeMethod(
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
            """);
        CognitiveComplexity nested = AnalyzeMethod(
            """
            public sealed class Sample
            {
                void M(bool a, bool b, bool c)
                {
                    if (a)
                    {
                        if (b)
                        {
                            if (c)
                            {
                            }
                        }
                    }
                }
            }
            """);

        Assert.Equal(3, flat.Value);
        Assert.Equal(6, nested.Value);
        Assert.True(nested.Value > flat.Value);
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

        bool analyzed = new CognitiveComplexityCalculator().TryCalculate(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out CognitiveComplexity result);

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
        bool analyzed = new CognitiveComplexityCalculator().TryCalculate(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out CognitiveComplexity result);

        Assert.True(analyzed);
        Assert.Equal(3, result.Value);
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

        bool analyzed = new CognitiveComplexityCalculator().TryCalculate(
            member,
            facts.SemanticModel,
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
            new CognitiveComplexityCalculator().TryCalculate(
                member,
                facts.SemanticModel,
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
                            var value = 1;
                        }
                    }
                }
            }
            """,
            0
        }
    };

    public static TheoryData<string, string, int> ControlFlowCases
    {
        get;
    } = new()
    {
        {
            "single if",
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
            "else-if chain with final else",
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
            4
        },
        {
            "foreach containing if",
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    foreach (var value in values)
                    {
                        if (value > 0)
                        {
                        }
                    }
                }
            }
            """,
            3
        },
        {
            "worked example if while if",
            """
            public sealed class Sample
            {
                void M(bool a, bool b, bool c)
                {
                    if (a)
                    {
                        while (b)
                        {
                            if (c)
                            {
                            }
                        }
                    }
                }
            }
            """,
            6
        },
        {
            "switch statement counts family once and nested flow inside case",
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
                        case 1:
                            return 2;
                        default:
                            return 0;
                    }

                    return -1;
                }
            }
            """,
            4
        },
        {
            "switch expression counts family and nested ternary in arm",
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
            3
        },
        {
            "multiple catches are siblings",
            """
            public sealed class Sample
            {
                void M(bool flag)
                {
                    try
                    {
                    }
                    catch (System.ArgumentException)
                    {
                        if (flag)
                        {
                        }
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
            4
        },
        {
            "nested conditional expression",
            """
            public sealed class Sample
            {
                int M(bool a, bool b) => a ? b ? 1 : 2 : 3;
            }
            """,
            3
        }
    };

    public static TheoryData<string, string, int> BooleanSequenceCases
    {
        get;
    } = new()
    {
        {
            "single logical and",
            """
            public sealed class Sample
            {
                void M(bool a, bool b)
                {
                    if (a && b)
                    {
                    }
                }
            }
            """,
            2
        },
        {
            "repeated same logical operator is one sequence",
            """
            public sealed class Sample
            {
                void M(bool a, bool b, bool c)
                {
                    if (a && b && c)
                    {
                    }
                }
            }
            """,
            2
        },
        {
            "logical operator change adds sequence cost",
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
            3
        },
        {
            "grouping preserves operator sequence semantics",
            """
            public sealed class Sample
            {
                void M(bool a, bool b, bool c, bool d)
                {
                    if ((a && b) || (c && d))
                    {
                    }
                }
            }
            """,
            4
        },
        {
            "parentheses alone do not inflate score",
            """
            public sealed class Sample
            {
                void M(bool a, bool b)
                {
                    if ((a && b))
                    {
                    }
                }
            }
            """,
            2
        }
    };

    public static TheoryData<string, string, int> PatternAndGuardCases
    {
        get;
    } = new()
    {
        {
            "pattern and sequence",
            """
            public sealed class Sample
            {
                bool M(int value)
                {
                    return value is > 0 and < 10;
                }
            }
            """,
            1
        },
        {
            "pattern operator change",
            """
            public sealed class Sample
            {
                bool M(int value)
                {
                    return value is (> 0 and < 10) or 42;
                }
            }
            """,
            2
        },
        {
            "switch label pattern and when guard",
            """
            public sealed class Sample
            {
                int M(object value, bool enabled)
                {
                    switch (value)
                    {
                        case int number when enabled && number > 0:
                            return number;
                        default:
                            return 0;
                    }
                }
            }
            """,
            4
        },
        {
            "switch expression pattern and when guard",
            """
            public sealed class Sample
            {
                int M(object value, bool enabled)
                {
                    return value switch
                    {
                        int number and > 0 when enabled => number,
                        _ => 0,
                    };
                }
            }
            """,
            4
        },
        {
            "catch filter",
            """
            public sealed class Sample
            {
                void M(bool a, bool b)
                {
                    try
                    {
                    }
                    catch (System.Exception) when (a || b)
                    {
                    }
                }
            }
            """,
            3
        }
    };

    public static TheoryData<string, string, int> RecursionCases
    {
        get;
    } = new()
    {
        {
            "direct self recursion counts once",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    if (value <= 0)
                    {
                        return 0;
                    }

                    return M(value - 1) + M(value - 2);
                }
            }
            """,
            2
        },
        {
            "ordinary non-recursive call does not count as recursion",
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    return Helper();
                }

                int Helper()
                {
                    return 0;
                }
            }
            """,
            0
        }
    };

    public static TheoryData<string, string, int> JumpAndExclusionCases
    {
        get;
    } = new()
    {
        {
            "break inside loop adds jump cost",
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
            2
        },
        {
            "continue and goto add jump cost",
            """
            public sealed class Sample
            {
                void M(bool flag)
                {
                start:
                    while (flag)
                    {
                        continue;
                    }

                    goto start;
                }
            }
            """,
            3
        },
        {
            "return throw await using lock are excluded by themselves",
            """
            using System.Threading.Tasks;

            public sealed class Sample
            {
                async Task M(object gate)
                {
                    using (var disposable = new Disposable())
                    {
                    }

                    lock (gate)
                    {
                    }

                    await Task.CompletedTask;
                    if (false)
                    {
                        throw new System.InvalidOperationException();
                    }

                    return;
                }

                private sealed class Disposable : System.IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
            """,
            1
        },
        {
            "yield return and yield break are excluded by themselves",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                IEnumerable<int> M()
                {
                    yield return 1;
                    yield break;
                }
            }
            """,
            0
        },
        {
            "fixed is excluded by itself",
            """
            public unsafe sealed class Sample
            {
                void M()
                {
                    fixed (int* pinned = new[] { 1 })
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

    private static CognitiveComplexity AnalyzeMethod(string source)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMethodMember(facts, "M");

        bool analyzed = new CognitiveComplexityCalculator().TryCalculate(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out CognitiveComplexity result);

        Assert.True(analyzed);
        return result;
    }

    [Fact]
    public void Local_function_direct_recursion_counts_once_for_local_root()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void Root()
                {
                    int Local(int value)
                    {
                        if (value <= 0)
                        {
                            return 0;
                        }

                        return Local(value - 1);
                    }
                }
            }
            """);
        LocalFunctionStatementSyntax localFunction = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<LocalFunctionStatementSyntax>()
            .Single();
        bool created = ExecutableMember.TryCreate(
            localFunction,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        bool analyzed = new CognitiveComplexityCalculator().TryCalculate(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out CognitiveComplexity result);

        Assert.True(analyzed);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void Same_name_overload_call_does_not_count_as_direct_self_recursion()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                int M(int value)
                {
                    return M();
                }

                int M()
                {
                    return 0;
                }
            }
            """);
        ExecutableMember member = CreateMethodMember(facts, "M", parameterCount: 1);

        bool analyzed = new CognitiveComplexityCalculator().TryCalculate(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out CognitiveComplexity result);

        Assert.True(analyzed);
        Assert.Equal(0, result.Value);
    }

    private static ExecutableMember CreateMethodMember(
        CompilationFacts facts,
        string methodName,
        int? parameterCount = null)
    {
        MethodDeclarationSyntax method = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method =>
                method.Identifier.ValueText == methodName
                && (!parameterCount.HasValue || method.ParameterList.Parameters.Count == parameterCount.Value));

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
            assemblyName: "CognitiveComplexityCalculatorTests",
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

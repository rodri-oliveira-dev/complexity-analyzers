using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class HalsteadClassificationAnalyzerTests
{
    [Fact]
    public void Counts_repeated_operator_and_operand_occurrences_separately_from_distinct_identities()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    int sum = left + right;
                    sum = sum + left + right;
                    return sum;
                }
            }
            """);

        AssertPrimitiveCounts(result, n1: 3, n2: 4, n1Total: 6, n2Total: 9);
        Assert.Equal(3, CountOperator(result, HalsteadOperatorKind.Add));
        Assert.Equal(2, CountOperator(result, HalsteadOperatorKind.SimpleAssignment));
        Assert.Equal(4, CountOperand(result, HalsteadOperandKind.Local, "local:sum"));
        Assert.Equal(2, CountOperand(result, HalsteadOperandKind.Parameter, "parameter:left"));
        Assert.Equal(2, CountOperand(result, HalsteadOperandKind.Parameter, "parameter:right"));
    }

    [Fact]
    public void Identifier_renaming_changes_operand_identity_without_changing_operator_counts()
    {
        HalsteadClassificationResult original = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    int result = left + right;
                    return result;
                }
            }
            """);
        HalsteadClassificationResult renamed = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int first, int second)
                {
                    int renamed = first + second;
                    return renamed;
                }
            }
            """);

        Assert.Equal(original.DistinctOperatorCount, renamed.DistinctOperatorCount);
        Assert.Equal(original.TotalOperatorCount, renamed.TotalOperatorCount);
        Assert.Contains(original.Elements, element => EqualsOperand(element, HalsteadOperandKind.Parameter, "parameter:left"));
        Assert.DoesNotContain(renamed.Elements, element => EqualsOperand(element, HalsteadOperandKind.Parameter, "parameter:left"));
        Assert.Contains(renamed.Elements, element => EqualsOperand(element, HalsteadOperandKind.Parameter, "parameter:first"));
    }

    [Fact]
    public void Literal_identity_uses_logical_literal_value_when_available()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M()
                {
                    int one = 1;
                    int hex = 0x1;
                    int two = 2;
                    return one + hex + two;
                }
            }
            """);

        Assert.Equal(2, CountOperand(result, HalsteadOperandKind.NumericLiteral, "int:1"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "int:2"));
    }

    [Fact]
    public void Comments_trivia_and_whitespace_only_formatting_do_not_change_classification()
    {
        HalsteadClassificationResult compact = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    int sum = left + right;
                    return sum;
                }
            }
            """);
        HalsteadClassificationResult formatted = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int left, int right)
                {
                    // Leading comment.
                    int sum
                        =
                        left
                        +
                        right; /* trailing comment */

                    return
                        sum;
                }
            }
            """);

        Assert.Equal(
            compact.Elements.Select(element => element.Identity.ToString()),
            formatted.Elements.Select(element => element.Identity.ToString()));
    }

    [Fact]
    public void Expression_bodied_member_counts_arrow_and_body_expression()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int value) => value + 1;
            }
            """);

        AssertPrimitiveCounts(result, n1: 2, n2: 2, n1Total: 2, n2Total: 2);
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.LambdaOrExpressionBody));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.Add));
    }

    [Fact]
    public void Null_operators_are_classified_by_syntax_context()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(string? text, int[]? values)
                {
                    text ??= "fallback";
                    int length = text?.Length ?? 0;
                    int? first = values?[0];
                    return first ?? length;
                }
            }
            """);

        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.NullCoalescingAssignment));
        Assert.Equal(2, CountOperator(result, HalsteadOperatorKind.NullCoalescing));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.ConditionalAccess));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.ConditionalElementAccess));
    }

    [Fact]
    public void Lambda_root_and_parent_are_classified_independently()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M(int seed)
                {
                    int total = seed;
                    System.Func<int, int> transform = value => value + total;
                    total++;
                }
            }
            """);

        ImmutableArray<ExecutableMember> members = CreateMembers(facts);
        HalsteadClassificationResult parent = Analyze(members.Single(member => member.DisplayName == "M"), facts);
        HalsteadClassificationResult lambda = Analyze(members.Single(member => member.Kind == ExecutableMemberKind.Lambda), facts);

        Assert.Equal(0, CountOperator(parent, HalsteadOperatorKind.Add));
        Assert.Equal(1, CountOperator(parent, HalsteadOperatorKind.LambdaOrExpressionBody));
        Assert.Equal(1, CountOperator(lambda, HalsteadOperatorKind.Add));
        Assert.Equal(1, CountOperator(lambda, HalsteadOperatorKind.LambdaOrExpressionBody));
    }

    [Fact]
    public void Local_function_root_and_parent_are_classified_independently()
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
        HalsteadClassificationResult parent = Analyze(members.Single(member => member.DisplayName == "M"), facts);
        HalsteadClassificationResult local = Analyze(members.Single(member => member.DisplayName == "Local"), facts);

        Assert.Equal(0, CountOperator(parent, HalsteadOperatorKind.Add));
        Assert.Equal(1, CountOperator(parent, HalsteadOperatorKind.LambdaOrExpressionBody));
        Assert.Equal(1, CountOperator(local, HalsteadOperatorKind.Add));
        Assert.Equal(1, CountOperator(local, HalsteadOperatorKind.LambdaOrExpressionBody));
    }

    [Fact]
    public void Patterns_and_pattern_combinators_are_classified()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                bool M(object value)
                {
                    int number = 1;
                    return value is int matched and > 0 || number is > 0 or not 42;
                }
            }
            """);

        Assert.Equal(2, CountOperator(result, HalsteadOperatorKind.Is));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.PatternAnd));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.PatternOr));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.PatternNot));
        Assert.Equal(2, CountOperator(result, HalsteadOperatorKind.GreaterThan));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.PatternVariable, "pattern:matched"));
    }

    [Fact]
    public void Interpolated_string_counts_literal_text_and_interpolation_expression_operands()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                string M(string name)
                {
                    return $"Hello {name}!";
                }
            }
            """);

        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.StringLiteral, "string:Hello !"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.Parameter, "parameter:name"));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.Return));
    }

    [Fact]
    public void Range_index_and_element_access_are_classified()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int[] values)
                {
                    return values[1..^1].Length;
                }
            }
            """);

        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.ElementAccess));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.Range));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.Index));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.MemberAccess));
    }

    [Fact]
    public void Collection_expression_and_spread_are_classified_when_supported_by_the_sdk()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int[] M(int[] values)
                {
                    int[] copy = [1, .. values, 2];
                    return copy;
                }
            }
            """);

        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.CollectionExpression));
        Assert.Equal(1, CountOperator(result, HalsteadOperatorKind.CollectionSpread));
    }

    [Fact]
    public void Empty_executable_member_reports_zero_primitive_counts()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """);

        AssertPrimitiveCounts(result, n1: 0, n2: 0, n1Total: 0, n2Total: 0);
    }

    [Fact]
    public void Bodyless_member_does_not_produce_a_classification_result()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public partial class Sample
            {
                partial void M();
            }
            """);
        ExecutableMember member = CreateMembers(facts).Single();

        bool analyzed = new HalsteadClassificationAnalyzer().TryAnalyze(
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
            new HalsteadClassificationAnalyzer().TryAnalyze(
                member,
                facts.SemanticModel,
                cancellation.Token,
                out _));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static HalsteadClassificationResult AnalyzeMethod(string source)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMembers(facts).Single(member => member.DisplayName == "M");

        return Analyze(member, facts);
    }

    private static HalsteadClassificationResult Analyze(
        ExecutableMember member,
        CompilationFacts facts)
    {
        bool analyzed = new HalsteadClassificationAnalyzer().TryAnalyze(
            member,
            facts.SemanticModel,
            CancellationToken.None,
            out HalsteadClassificationResult result);

        Assert.True(analyzed);
        return result;
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

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Parse, SourceCodeKind.Regular));
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "HalsteadClassificationAnalyzerTests",
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

    private static void AssertPrimitiveCounts(
        HalsteadClassificationResult result,
        int n1,
        int n2,
        int n1Total,
        int n2Total)
    {
        Assert.Equal(n1, result.DistinctOperatorCount);
        Assert.Equal(n2, result.DistinctOperandCount);
        Assert.Equal(n1Total, result.TotalOperatorCount);
        Assert.Equal(n2Total, result.TotalOperandCount);
    }

    private static int CountOperator(
        HalsteadClassificationResult result,
        HalsteadOperatorKind kind)
    {
        return result.Elements.Count(element =>
            element.Role == HalsteadElementRole.Operator
            && element.Identity.Kind == kind.ToString());
    }

    private static int CountOperand(
        HalsteadClassificationResult result,
        HalsteadOperandKind kind,
        string canonicalValue)
    {
        return result.Elements.Count(element => EqualsOperand(element, kind, canonicalValue));
    }

    private static bool EqualsOperand(
        HalsteadElement element,
        HalsteadOperandKind kind,
        string canonicalValue)
    {
        return element.Role == HalsteadElementRole.Operand
            && element.Identity.Kind == kind.ToString()
            && element.Identity.CanonicalValue == canonicalValue;
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

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
    public void Expression_operator_families_are_classified()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                private event System.Action? Changed;

                void M(int[] values, bool flag)
                {
                    int value = 8;
                    int other = 2;
                    int add = value + other;
                    int subtract = value - other;
                    int multiply = value * other;
                    int divide = value / other;
                    int modulo = value % other;
                    bool comparisons = value == other || value != other || value < other || value <= other || value > other || value >= other;
                    bool logical = flag && comparisons || !flag;
                    int bitwise = (value & other) | (value ^ other);
                    int shifted = (value << 1) + (value >> 1) + (value >>> 1);
                    value += 1;
                    value -= 1;
                    value *= 2;
                    value /= 2;
                    value %= 2;
                    value &= 3;
                    value |= 4;
                    value ^= 5;
                    value <<= 1;
                    value >>= 1;
                    value >>>= 1;
                    string? text = null;
                    text ??= "fallback";
                    int positive = +value;
                    int negative = -value;
                    int complement = ~value;
                    ++value;
                    --value;
                    value++;
                    value--;
                    int conditional = flag ? value : other;
                    Helper implicitObject = new();
                    Helper explicitObject = new Helper();
                    int[] array = new int[] { 1, 2 };
                    int[] implicitArray = new[] { 1, 2 };
                    int member = explicitObject.Value;
                    int? maybeMember = explicitObject?.Value;
                    int element = values[0];
                    int fromEnd = values[^1];
                    System.Func<int, int, int> combine = (left, right) => left + right;
                    Publisher publisher = new Publisher();
                    publisher.Changed += Handler;
                    publisher.Changed -= Handler;
                    Changed += Handler;
                    Changed -= Handler;
                    Changed?.Invoke();
                    Consume(add, subtract, multiply, divide, modulo, logical, bitwise, shifted, text, positive, negative, complement, conditional, implicitObject, array, implicitArray, member, maybeMember, element, fromEnd, combine, publisher);
                }

                void Handler()
                {
                }

                void Consume(params object?[] values)
                {
                }

                private sealed class Helper
                {
                    public int Value { get; set; }
                }

                private sealed class Publisher
                {
                    public event System.Action? Changed;
                }
            }
            """);

        HalsteadOperatorKind[] expectedOperators =
        [
            HalsteadOperatorKind.Add,
            HalsteadOperatorKind.Subtract,
            HalsteadOperatorKind.Multiply,
            HalsteadOperatorKind.Divide,
            HalsteadOperatorKind.Modulo,
            HalsteadOperatorKind.Equal,
            HalsteadOperatorKind.NotEqual,
            HalsteadOperatorKind.LessThan,
            HalsteadOperatorKind.LessThanOrEqual,
            HalsteadOperatorKind.GreaterThan,
            HalsteadOperatorKind.GreaterThanOrEqual,
            HalsteadOperatorKind.LogicalAnd,
            HalsteadOperatorKind.LogicalOr,
            HalsteadOperatorKind.LogicalNot,
            HalsteadOperatorKind.BitwiseAnd,
            HalsteadOperatorKind.BitwiseOr,
            HalsteadOperatorKind.ExclusiveOr,
            HalsteadOperatorKind.LeftShift,
            HalsteadOperatorKind.RightShift,
            HalsteadOperatorKind.UnsignedRightShift,
            HalsteadOperatorKind.AddAssignment,
            HalsteadOperatorKind.SubtractAssignment,
            HalsteadOperatorKind.MultiplyAssignment,
            HalsteadOperatorKind.DivideAssignment,
            HalsteadOperatorKind.ModuloAssignment,
            HalsteadOperatorKind.AndAssignment,
            HalsteadOperatorKind.OrAssignment,
            HalsteadOperatorKind.ExclusiveOrAssignment,
            HalsteadOperatorKind.LeftShiftAssignment,
            HalsteadOperatorKind.RightShiftAssignment,
            HalsteadOperatorKind.UnsignedRightShiftAssignment,
            HalsteadOperatorKind.NullCoalescingAssignment,
            HalsteadOperatorKind.UnaryPlus,
            HalsteadOperatorKind.UnaryMinus,
            HalsteadOperatorKind.BitwiseNot,
            HalsteadOperatorKind.PreIncrement,
            HalsteadOperatorKind.PreDecrement,
            HalsteadOperatorKind.PostIncrement,
            HalsteadOperatorKind.PostDecrement,
            HalsteadOperatorKind.Conditional,
            HalsteadOperatorKind.ImplicitObjectCreation,
            HalsteadOperatorKind.ObjectCreation,
            HalsteadOperatorKind.ArrayCreation,
            HalsteadOperatorKind.MemberAccess,
            HalsteadOperatorKind.ConditionalAccess,
            HalsteadOperatorKind.ElementAccess,
            HalsteadOperatorKind.Index,
            HalsteadOperatorKind.Invocation,
            HalsteadOperatorKind.LambdaOrExpressionBody,
        ];

        foreach (HalsteadOperatorKind expectedOperator in expectedOperators)
        {
            Assert.True(
                CountOperator(result, expectedOperator) > 0,
                $"Expected operator '{expectedOperator}' to be classified.");
        }

        Assert.True(CountOperandKind(result, HalsteadOperandKind.Event) > 0);
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.Parameter, "parameter:left"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.Parameter, "parameter:right"));
    }

    [Fact]
    public void Control_flow_statement_families_are_classified()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(int[] values, object input)
                {
                    int total = 0;
                    if (values.Length > 0)
                    {
                        total++;
                    }
                    else
                    {
                        total--;
                    }

                    for (int i = 0; i < values.Length; i++)
                    {
                        if (i == 2)
                        {
                            continue;
                        }

                        total += i;
                    }

                    foreach (int item in values)
                    {
                        total += item;
                        break;
                    }

                    while (total < 10)
                    {
                        total++;
                    }

                    do
                    {
                        total--;
                    }
                    while (total > 5);

                    switch (total)
                    {
                        case 0 when values.Length == 0:
                            goto done;
                        case 1:
                            total += 2;
                            break;
                        default:
                            total += 1;
                            break;
                    }

                    try
                    {
                        using (Disposable disposable = new Disposable())
                        {
                            lock (disposable)
                            {
                                checked
                                {
                                    total += checked(total + 1);
                                }
                            }
                        }

                        using Disposable another = new Disposable();
                        object required = input ?? throw new System.ArgumentNullException(nameof(input));
                        if (required is string)
                        {
                            throw new System.InvalidOperationException();
                        }
                    }
                    catch (System.InvalidOperationException ex)
                    {
                        total += ex.Message.Length;
                    }
                    finally
                    {
                        total += 1;
                    }

                done:
                    return total;
                }

                private sealed class Disposable : System.IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
            """);

        HalsteadOperatorKind[] expectedOperators =
        [
            HalsteadOperatorKind.If,
            HalsteadOperatorKind.Else,
            HalsteadOperatorKind.For,
            HalsteadOperatorKind.Foreach,
            HalsteadOperatorKind.While,
            HalsteadOperatorKind.Do,
            HalsteadOperatorKind.Switch,
            HalsteadOperatorKind.Case,
            HalsteadOperatorKind.Default,
            HalsteadOperatorKind.When,
            HalsteadOperatorKind.Try,
            HalsteadOperatorKind.Catch,
            HalsteadOperatorKind.Finally,
            HalsteadOperatorKind.Break,
            HalsteadOperatorKind.Continue,
            HalsteadOperatorKind.Goto,
            HalsteadOperatorKind.Using,
            HalsteadOperatorKind.Lock,
            HalsteadOperatorKind.Checked,
            HalsteadOperatorKind.Throw,
            HalsteadOperatorKind.Return,
        ];

        foreach (HalsteadOperatorKind expectedOperator in expectedOperators)
        {
            Assert.True(
                CountOperator(result, expectedOperator) > 0,
                $"Expected operator '{expectedOperator}' to be classified.");
        }

        Assert.True(CountOperand(result, HalsteadOperandKind.Local, "local:item") > 0);
        Assert.True(CountOperand(result, HalsteadOperandKind.Local, "local:ex") > 0);
    }

    [Fact]
    public void Literal_pattern_and_type_expression_families_are_classified()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M(object input)
                {
                    byte b = 1;
                    sbyte sb = 2;
                    short s = 3;
                    ushort us = 4;
                    uint ui = 5u;
                    long l = 6L;
                    ulong ul = 7UL;
                    float f = 8.5f;
                    double d = 9.5d;
                    decimal m = 10.5m;
                    char c = 'c';
                    bool t = true;
                    bool ff = false;
                    object? n = null;
                    Consume("utf8"u8);
                    _ = typeof(string);
                    _ = sizeof(int);
                    _ = default(decimal);
                    _ = (long)42;
                    bool isString = input is string;
                    return input switch
                    {
                        int number when number is < 0 or <= 1 or >= 3 => number,
                        string { Length: > 0 } => 1,
                        null => 0,
                        _ => -1
                    };
                }

                void Consume(System.ReadOnlySpan<byte> value)
                {
                }
            }
            """);

        Assert.True(CountOperator(result, HalsteadOperatorKind.Switch) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.SwitchArm) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.When) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.Is) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.PatternOr) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.LessThan) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.LessThanOrEqual) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.GreaterThan) > 0);
        Assert.True(CountOperator(result, HalsteadOperatorKind.GreaterThanOrEqual) > 0);
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.CharacterLiteral, "char:c"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.BooleanLiteral, "bool:true"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.BooleanLiteral, "bool:false"));
        Assert.True(CountOperand(result, HalsteadOperandKind.NullLiteral, "null") > 0);
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.StringLiteral, "string:utf8"));
        Assert.True(CountOperand(result, HalsteadOperandKind.Discard, "discard:_") > 0);
        Assert.True(CountOperand(result, HalsteadOperandKind.Property, "property:Length") > 0);
        Assert.True(CountOperand(result, HalsteadOperandKind.PatternVariable, "pattern:number") > 0);
        Assert.True(CountOperand(result, HalsteadOperandKind.TypeName, "type:string") > 0);
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "byte:1"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "sbyte:2"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "short:3"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "ushort:4"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "uint:5"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "long:6"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "ulong:7"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "float:8.5"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "double:9.5"));
        Assert.Equal(1, CountOperand(result, HalsteadOperandKind.NumericLiteral, "decimal:10.5"));
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

        bool analyzed = HalsteadClassificationAnalyzer.TryAnalyze(
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
            HalsteadClassificationAnalyzer.TryAnalyze(
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
        bool analyzed = HalsteadClassificationAnalyzer.TryAnalyze(
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

    private static int CountOperandKind(
        HalsteadClassificationResult result,
        HalsteadOperandKind kind)
    {
        return result.Elements.Count(element =>
            element.Role == HalsteadElementRole.Operand
            && element.Identity.Kind == kind.ToString());
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

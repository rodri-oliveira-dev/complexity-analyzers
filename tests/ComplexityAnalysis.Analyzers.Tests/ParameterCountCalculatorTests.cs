using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ParameterCountCalculatorTests
{
    [Theory]
    [InlineData("void M() { }", 0)]
    [InlineData("void M(int value) { }", 1)]
    [InlineData("void M(int id, string name, System.Threading.CancellationToken cancellationToken) { }", 3)]
    [InlineData("void M<T, TResult>(T value) where T : class { }", 1)]
    [InlineData("""void M(int id, string name = "default", params string[] values) { }""", 3)]
    [InlineData("void M(ref int value, in int input, out int result) { result = value + input; }", 3)]
    public void Ordinary_method_counts_source_declared_parameters(
        string methodSource,
        int expected)
    {
        ParameterCount count = AnalyzeSingleMember(
            $$"""
            public sealed class Sample
            {
                {{methodSource}}
            }
            """,
            ExecutableMemberKind.OrdinaryMethod);

        Assert.Equal(expected, count.Value);
    }

    [Fact]
    public void Extension_method_receiver_counts_as_source_declared_parameter()
    {
        ParameterCount count = AnalyzeSingleMember(
            """
            using System.Collections.Generic;

            public static class Extensions
            {
                public static bool ContainsValue(this IEnumerable<string> source, string value) => true;
            }
            """,
            ExecutableMemberKind.OrdinaryMethod);

        Assert.Equal(2, count.Value);
    }

    [Theory]
    [InlineData(
        """
        public sealed class Sample
        {
            public Sample() { }
        }
        """,
        "Sample.ctor",
        0)]
    [InlineData(
        """
        public sealed class Sample
        {
            public Sample(string name, int age) { }
        }
        """,
        "Sample.ctor",
        2)]
    [InlineData(
        """
        public sealed class Sample
        {
            static Sample() { }
        }
        """,
        "Sample.cctor",
        0)]
    public void Constructors_count_declared_parameters(
        string source,
        string displayName,
        int expected)
    {
        ParameterCount count = AnalyzeMember(source, displayName);

        Assert.Equal(expected, count.Value);
    }

    [Theory]
    [InlineData(
        """
        public sealed class Sample
        {
            public static Sample operator !(Sample value) => value;
        }
        """,
        "operator !",
        1)]
    [InlineData(
        """
        public sealed class Sample
        {
            public static Sample operator +(Sample left, Sample right) => left;
        }
        """,
        "operator +",
        2)]
    [InlineData(
        """
        public sealed class Sample
        {
            public static implicit operator int(Sample value) => 0;
        }
        """,
        "implicit operator int",
        1)]
    public void Operators_count_declared_parameters(
        string source,
        string displayName,
        int expected)
    {
        ParameterCount count = AnalyzeMember(source, displayName);

        Assert.Equal(expected, count.Value);
    }

    [Fact]
    public void Local_function_counts_its_parameters_without_inflating_parent()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M(int parent)
                {
                    int Local(int left, int right) => left + right;
                }
            }
            """);

        ImmutableArray<ExecutableMember> members = CreateMembers(facts);
        ParameterCount parent = Analyze(members.Single(member => member.DisplayName == "M"));
        ParameterCount local = Analyze(members.Single(member => member.DisplayName == "Local"));

        Assert.Equal(1, parent.Value);
        Assert.Equal(2, local.Value);
    }

    [Theory]
    [InlineData("System.Func<int, int> f = x => x + 1;", 1)]
    [InlineData("System.Func<int, int, int> f = (x, y) => x + y;", 2)]
    [InlineData("System.Func<int, string, string> f = (int x, string value) => value + x;", 2)]
    [InlineData("System.Func<int> f = () => 1;", 0)]
    public void Lambdas_count_declared_parameters(string lambdaSource, int expected)
    {
        ParameterCount count = AnalyzeSingleMember(
            $$"""
            public sealed class Sample
            {
                void M()
                {
                    {{lambdaSource}}
                }
            }
            """,
            ExecutableMemberKind.Lambda);

        Assert.Equal(expected, count.Value);
    }

    [Fact]
    public void Lambda_captured_variables_do_not_count_as_parameters()
    {
        ParameterCount count = AnalyzeSingleMember(
            """
            public sealed class Sample
            {
                void M()
                {
                    int factor = 10;
                    System.Func<int, int> f = x => x * factor;
                }
            }
            """,
            ExecutableMemberKind.Lambda);

        Assert.Equal(1, count.Value);
    }

    [Theory]
    [InlineData("System.Func<int, int, int> f = delegate(int x, int y) { return x + y; };", 2)]
    [InlineData("System.Action f = delegate { };", 0)]
    public void Anonymous_methods_count_explicit_parameter_list_when_present(
        string anonymousMethodSource,
        int expected)
    {
        ParameterCount count = AnalyzeSingleMember(
            $$"""
            public sealed class Sample
            {
                void M()
                {
                    {{anonymousMethodSource}}
                }
            }
            """,
            ExecutableMemberKind.AnonymousMethod);

        Assert.Equal(expected, count.Value);
    }

    [Fact]
    public void Ordinary_accessors_do_not_count_implicit_value_parameter()
    {
        CompilationFacts facts = CreateCompilationFacts(
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
            """);

        ImmutableArray<ExecutableMember> accessors =
        [
            .. CreateMembers(facts).Where(member => member.Kind == ExecutableMemberKind.Accessor)
        ];

        Assert.Equal(5, accessors.Length);
        Assert.All(accessors, accessor => Assert.Equal(0, Analyze(accessor).Value));
    }

    [Fact]
    public void Indexer_accessors_count_explicit_index_parameters_but_not_setter_value()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                public string this[int index, string key]
                {
                    get { return key; }
                    set { _ = value; }
                }
            }
            """);

        ImmutableArray<ExecutableMember> accessors =
        [
            .. CreateMembers(facts).Where(member => member.Kind == ExecutableMemberKind.Accessor)
        ];

        Assert.Equal(2, accessors.Length);
        Assert.All(accessors, accessor => Assert.Equal(2, Analyze(accessor).Value));
    }

    [Fact]
    public void Expression_bodied_property_counts_zero_parameters()
    {
        ParameterCount count = AnalyzeSingleMember(
            """
            public sealed class Sample
            {
                public int Answer => 42;
            }
            """,
            ExecutableMemberKind.ExpressionBodiedProperty);

        Assert.Equal(0, count.Value);
    }

    [Fact]
    public void Primary_constructor_is_deferred_by_current_executable_member_matrix()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Customer(string name, int age)
            {
                public string Name { get; } = name;
            }
            """);

        Assert.DoesNotContain(
            CreateMembers(facts),
            member => member.DisplayName.Contains("Customer", StringComparison.Ordinal));
    }

    [Fact]
    public void Bodyless_method_does_not_produce_parameter_count()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public partial class Sample
            {
                partial void M(int value);
            }
            """);
        ExecutableMember member = CreateMembers(facts).Single();

        bool calculated = new ParameterCountCalculator().TryCalculate(
            member,
            CancellationToken.None,
            out _);

        Assert.False(calculated);
    }

    [Fact]
    public void Already_canceled_token_stops_analysis()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M(int value) { }
            }
            """);
        ExecutableMember member = CreateMembers(facts).Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            new ParameterCountCalculator().TryCalculate(
                member,
                cancellation.Token,
                out _));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static ParameterCount AnalyzeSingleMember(
        string source,
        ExecutableMemberKind kind)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMembers(facts).Single(member => member.Kind == kind);

        return Analyze(member);
    }

    private static ParameterCount AnalyzeMember(
        string source,
        string displayName)
    {
        CompilationFacts facts = CreateCompilationFacts(source);
        ExecutableMember member = CreateMembers(facts).Single(member => member.DisplayName == displayName);

        return Analyze(member);
    }

    private static ParameterCount Analyze(ExecutableMember member)
    {
        bool calculated = new ParameterCountCalculator().TryCalculate(
            member,
            CancellationToken.None,
            out ParameterCount count);

        Assert.True(calculated);
        return count;
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
            assemblyName: "ParameterCountCalculatorTests",
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

using System.Collections.Immutable;
using System.Reflection;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class InputSizeResolverTests
{
    [Fact]
    public void Array_parameter_maps_to_n()
    {
        MethodAnalysisContext context = CreateContext("void A(int[] items) { }");

        AssertParameterVariable(context, "items", "n");
    }

    [Fact]
    public void Multiple_array_parameters_map_to_signature_order()
    {
        MethodAnalysisContext context = CreateContext("void B(int[] left, int[] right) { }");

        AssertParameterVariable(context, "left", "n");
        AssertParameterVariable(context, "right", "m");
    }

    [Fact]
    public void Bool_before_collection_does_not_consume_canonical_variable()
    {
        MethodAnalysisContext context = CreateContext("void C(bool enabled, int[] items) { }");

        Assert.False(context.TryGetInputSizeVariable(GetParameter(context, "enabled"), out _));
        AssertParameterVariable(context, "items", "n");
    }

    [Fact]
    public void String_parameters_map_to_signature_order()
    {
        MethodAnalysisContext context = CreateContext("void D(string text, string pattern) { }");

        AssertParameterVariable(context, "text", "n");
        AssertParameterVariable(context, "pattern", "m");
    }

    [Fact]
    public void Integral_parameter_can_represent_bound_dimension()
    {
        MethodAnalysisContext context = CreateContext("void E(int count) { }");

        AssertParameterVariable(context, "count", "n");
    }

    [Fact]
    public void Collection_and_enumerable_parameters_are_size_candidates()
    {
        MethodAnalysisContext context = CreateContext(
            """
            void F(
                System.Collections.Generic.IReadOnlyCollection<int> values,
                System.Collections.Generic.IEnumerable<string> names)
            {
            }
            """);

        AssertParameterVariable(context, "values", "n");
        AssertParameterVariable(context, "names", "m");
    }

    [Fact]
    public void Enum_and_bool_parameters_are_not_size_candidates()
    {
        MethodAnalysisContext context = CreateContext(
            """
            enum Mode { First, Second }

            void G(Mode mode, bool enabled, int[] items)
            {
            }
            """);

        Assert.False(context.TryGetInputSizeVariable(GetParameter(context, "mode"), out _));
        Assert.False(context.TryGetInputSizeVariable(GetParameter(context, "enabled"), out _));
        AssertParameterVariable(context, "items", "n");
    }

    [Fact]
    public void CancellationToken_parameter_does_not_consume_canonical_variable()
    {
        MethodAnalysisContext context = CreateContext(
            """
            void H(System.Threading.CancellationToken cancellationToken, int[] items)
            {
            }
            """);

        Assert.False(context.TryGetInputSizeVariable(GetParameter(context, "cancellationToken"), out _));
        AssertParameterVariable(context, "items", "n");
    }

    [Fact]
    public void Canonical_variable_fallback_is_deterministic_after_known_names()
    {
        MethodAnalysisContext context = CreateContext("void I(int a, int b, int c, int d, int e) { }");

        AssertParameterVariable(context, "a", "n");
        AssertParameterVariable(context, "b", "m");
        AssertParameterVariable(context, "c", "k");
        AssertParameterVariable(context, "d", "p");
        AssertParameterVariable(context, "e", "v5");
    }

    [Fact]
    public void Symbol_lookup_uses_symbol_equality_comparer()
    {
        MethodFacts facts = CreateFacts("void J(int[] items) { }");
        MethodAnalysisContext context = MethodAnalysisContext.Create(
            facts.SemanticModel,
            facts.MethodSymbol,
            CancellationToken.None);

        IMethodSymbol sameMethodSymbol = facts.SemanticModel.GetDeclaredSymbol(
            facts.MethodDeclaration,
            CancellationToken.None)!;

        Assert.Same(SymbolEqualityComparer.Default, context.InputSizeVariables.KeyComparer);
        Assert.True(SymbolEqualityComparer.Default.Equals(facts.MethodSymbol.Parameters[0], sameMethodSymbol.Parameters[0]));
        Assert.True(context.TryGetInputSizeVariable(sameMethodSymbol.Parameters[0], out ComplexityVariable variable));
        Assert.Equal("n", variable.Name);
    }

    [Fact]
    public void Independent_method_contexts_do_not_share_variable_state()
    {
        MethodAnalysisContext first = CreateContext("void K(int[] items) { }");
        MethodAnalysisContext second = CreateContext("void L(string text) { }");

        AssertParameterVariable(first, "items", "n");
        AssertParameterVariable(second, "text", "n");
        _ = Assert.Single(first.InputSizeVariables);
        _ = Assert.Single(second.InputSizeVariables);
    }

    [Fact]
    public void Already_cancelled_token_is_respected_before_semantic_resolution()
    {
        MethodFacts facts = CreateFacts("void M(int[] items) { }");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            MethodAnalysisContext.Create(
                facts.MethodDeclaration,
                facts.SemanticModel,
                cancellationTokenSource.Token));
    }

    [Fact]
    public void Already_cancelled_token_is_respected_by_input_size_resolver()
    {
        MethodFacts facts = CreateFacts("void N(int[] items) { }");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var resolver = new InputSizeResolver(facts.SemanticModel, cancellationTokenSource.Token);

        _ = Assert.Throws<OperationCanceledException>(() =>
            resolver.ResolveParameterVariables(facts.MethodSymbol));
    }

    private static MethodAnalysisContext CreateContext(string methodSource)
    {
        MethodFacts facts = CreateFacts(methodSource);

        return MethodAnalysisContext.Create(
            facts.SemanticModel,
            facts.MethodSymbol,
            CancellationToken.None);
    }

    private static MethodFacts CreateFacts(string methodSource)
    {
        string source =
            """
            public sealed partial class Sample
            {
            """ + methodSource + """

            }
            """;

        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "InputSizeResolverTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        MethodDeclarationSyntax methodDeclaration = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
        IMethodSymbol methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration)!;

        return new MethodFacts(semanticModel, methodDeclaration, methodSymbol);
    }

    private static IParameterSymbol GetParameter(MethodAnalysisContext context, string name)
    {
        return context.MethodSymbol.Parameters.Single(parameter => parameter.Name == name);
    }

    private static void AssertParameterVariable(
        MethodAnalysisContext context,
        string parameterName,
        string expectedVariableName)
    {
        Assert.True(
            context.TryGetInputSizeVariable(GetParameter(context, parameterName), out ComplexityVariable variable),
            "Expected parameter '" + parameterName + "' to have an input-size variable.");
        Assert.Equal(expectedVariableName, variable.Name);
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } =
        [
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CancellationToken).GetTypeInfo().Assembly.Location)
        ];

    private sealed record MethodFacts(
        SemanticModel SemanticModel,
        MethodDeclarationSyntax MethodDeclaration,
        IMethodSymbol MethodSymbol);
}

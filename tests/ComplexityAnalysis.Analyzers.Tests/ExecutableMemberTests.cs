using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ExecutableMemberTests
{
    [Fact]
    public void Ordinary_method_member_captures_symbol_body_location_display_and_tree()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                public int M(int value)
                {
                    return value + 1;
                }
            }
            """);
        MethodDeclarationSyntax method = GetMethod(facts, "M");

        bool created = ExecutableMember.TryCreateOrdinaryMethod(
            method,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        Assert.Same(method, member.Declaration);
        Assert.Equal("M", member.DisplayName);
        Assert.Equal("M", member.Symbol.Name);
        Assert.Same(facts.SyntaxTree, member.SyntaxTree);
        Assert.Equal(method.Identifier.Span, member.DiagnosticLocation.SourceSpan);
        Assert.NotNull(member.Body.Block);
        Assert.Null(member.Body.Expression);
        Assert.True(member.Body.HasBody);
    }

    [Fact]
    public void Ordinary_expression_bodied_method_member_captures_expression_body()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                public int M(int value) => value + 1;
            }
            """);
        MethodDeclarationSyntax method = GetMethod(facts, "M");

        bool created = ExecutableMember.TryCreateOrdinaryMethod(
            method,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        Assert.Null(member.Body.Block);
        Assert.NotNull(member.Body.Expression);
        Assert.True(member.Body.HasBody);
        Assert.Equal("value + 1", member.Body.Expression.ToString());
    }

    [Fact]
    public void Ordinary_method_member_without_body_is_represented_without_executable_body()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public partial class Sample
            {
                partial void M();
            }
            """);
        MethodDeclarationSyntax method = GetMethod(facts, "M");

        bool created = ExecutableMember.TryCreateOrdinaryMethod(
            method,
            facts.SemanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        Assert.Null(member.Body.Block);
        Assert.Null(member.Body.Expression);
        Assert.False(member.Body.HasBody);
    }

    private static MethodDeclarationSyntax GetMethod(
        CompilationFacts facts,
        string methodName)
    {
        return facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName));
    }

    private static CompilationFacts CreateCompilationFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ExecutableMemberTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

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

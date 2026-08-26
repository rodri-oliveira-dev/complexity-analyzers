using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;

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

    [Fact]
    public void Method_analysis_context_compatibility_wrapper_uses_executable_member_symbol()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                public void M(int[] input)
                {
                    foreach (var item in input)
                    {
                    }
                }
            }
            """);
        MethodDeclarationSyntax method = GetMethod(facts, "M");
        ExecutableMember member = CreateMember(facts, method);
        InterproceduralAnalysisContext interproceduralContext = InterproceduralAnalysisContext.Create(
            facts.SemanticModel.Compilation,
            CancellationToken.None);
        InterproceduralRootAnalysisState rootState = interproceduralContext.CreateRootState(
            member.Symbol,
            CancellationToken.None);

        MethodAnalysisContext fromMethod = MethodAnalysisContext.Create(
            method,
            facts.SemanticModel,
            interproceduralContext,
            rootState,
            CancellationToken.None);
        MethodAnalysisContext fromMember = MethodAnalysisContext.Create(
            member,
            facts.SemanticModel,
            interproceduralContext,
            rootState,
            CancellationToken.None);

        Assert.True(SymbolEqualityComparer.Default.Equals(member.Symbol, fromMethod.MethodSymbol));
        Assert.True(SymbolEqualityComparer.Default.Equals(fromMethod.MethodSymbol, fromMember.MethodSymbol));
        Assert.Same(interproceduralContext, fromMethod.InterproceduralContext);
        Assert.Same(rootState, fromMethod.InterproceduralRootState);
        Assert.Equal(fromMember.Options.MaxCallDepth, fromMethod.Options.MaxCallDepth);
        Assert.Equal(fromMember.Options.MaxMethodsPerRoot, fromMethod.Options.MaxMethodsPerRoot);
    }

    [Fact]
    public void Method_complexity_compatibility_wrappers_delegate_through_executable_member()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                public void M(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                    }
                }
            }
            """);
        MethodDeclarationSyntax method = GetMethod(facts, "M");
        ExecutableMember member = CreateMember(facts, method);
        InterproceduralAnalysisContext interproceduralContext = InterproceduralAnalysisContext.Create(
            facts.SemanticModel.Compilation,
            CancellationToken.None);
        InterproceduralRootAnalysisState rootState = interproceduralContext.CreateRootState(
            member.Symbol,
            CancellationToken.None);

        InterproceduralAnalysisResult sourceResult = new MethodComplexityExtractor().AnalyzeSourceMethod(
            method,
            member.Symbol,
            facts.SemanticModel,
            interproceduralContext,
            rootState,
            ComplexityAnalyzerOptions.Default,
            CancellationToken.None);

        Assert.Equal(InterproceduralAnalysisResultKind.Known, sourceResult.Kind);
        Assert.Equal("O(n)", sourceResult.Complexity.ToBigONotation());

        MethodAnalysisContext methodContext = MethodAnalysisContext.Create(
            facts.SemanticModel,
            member.Symbol,
            CancellationToken.None);
        bool solved = MethodComplexityExtractor.TrySolveDirectRecurrence(
            method,
            methodContext,
            out ComplexityExpression? recurrenceComplexity);

        Assert.False(solved);
        Assert.Null(recurrenceComplexity);
    }

    [Fact]
    public void Recursion_and_recurrence_compatibility_wrappers_delegate_through_executable_member()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                public int M(int n)
                {
                    if (n <= 1)
                    {
                        return 1;
                    }

                    return M(n - 1);
                }
            }
            """);
        MethodDeclarationSyntax method = GetMethod(facts, "M");
        ExecutableMember member = CreateMember(facts, method);
        MethodAnalysisContext context = MethodAnalysisContext.Create(
            facts.SemanticModel,
            member.Symbol,
            CancellationToken.None);

        RecursiveCallAnalysisResult recursiveResult = new RecursiveCallAnalyzer().Analyze(
            method,
            context);
        RecurrenceExtractionResult recurrenceResult = new RecurrenceExtractor().Extract(
            method,
            context);

        Assert.True(recursiveResult.IsSupported);
        Assert.True(recursiveResult.HasDirectRecursiveCalls);
        Assert.True(recurrenceResult.IsExtracted);
        Assert.Equal("n", recurrenceResult.Relation!.ComplexityVariable.Name);
        Assert.Equal("O(1)", recurrenceResult.Relation.NonRecursiveWork.ToBigONotation());
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

    private static ExecutableMember CreateMember(
        CompilationFacts facts,
        MethodDeclarationSyntax method)
    {
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

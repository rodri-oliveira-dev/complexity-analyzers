using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class InterproceduralAnalysisContextTests
{
    [Fact]
    public void Context_uses_internal_default_budget_from_phase_spec()
    {
        CompilationFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """);

        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        Assert.Same(facts.Compilation, context.Compilation);
        Assert.Equal(5, context.Budget.MaximumCallDepth);
        Assert.Equal(32, context.Budget.MaximumMethodsPerRootAnalysis);
        Assert.NotNull(context.SourceMethodResolver);
        Assert.NotNull(context.TemplateCache);
    }

    [Fact]
    public void Template_cache_is_isolated_between_compilations()
    {
        const string source = """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """;
        CompilationFacts firstFacts = CreateFacts(source);
        CompilationFacts secondFacts = CreateFacts(source);
        IMethodSymbol firstMethod = GetMethod(firstFacts, "M");
        IMethodSymbol secondMethod = GetMethod(secondFacts, "M");
        InterproceduralAnalysisContext firstContext = InterproceduralAnalysisContext.Create(
            firstFacts.Compilation,
            CancellationToken.None);
        InterproceduralAnalysisContext secondContext = InterproceduralAnalysisContext.Create(
            secondFacts.Compilation,
            CancellationToken.None);
        InterproceduralAnalysisResult result = Known(ComplexityFactory.Constant());

        firstContext.TemplateCache.StoreCompleted(firstMethod, result, CancellationToken.None);

        Assert.True(firstContext.TemplateCache.TryGetCompleted(firstMethod, CancellationToken.None, out _));
        Assert.False(secondContext.TemplateCache.TryGetCompleted(secondMethod, CancellationToken.None, out _));
        Assert.Equal(1, firstContext.TemplateCache.Count);
        Assert.Equal(0, secondContext.TemplateCache.Count);
    }

    [Fact]
    public void Template_cache_uses_original_method_definition_for_identity_hits()
    {
        CompilationFacts facts = CreateFacts(
            """
            public static class Helpers
            {
                public static T Echo<T>(T value) => value;
            }

            public sealed class Sample
            {
                int M(int value) => Helpers.Echo(value);
            }
            """);
        IMethodSymbol definition = GetMethod(facts, "Echo");
        IMethodSymbol constructedTarget = GetSingleInvocationTarget(facts);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        InterproceduralAnalysisResult result = Known(ComplexityFactory.Linear(ComplexityVariable.N));

        context.TemplateCache.StoreCompleted(definition, result, CancellationToken.None);

        Assert.True(context.TemplateCache.TryGetCompleted(
            constructedTarget,
            CancellationToken.None,
            out InterproceduralAnalysisResult cached));
        Assert.Same(result, cached);
    }

    [Fact]
    public void Template_cache_does_not_collide_overloads()
    {
        CompilationFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Visit(int[] values)
                {
                }

                void Visit(string text)
                {
                }
            }
            """);
        IMethodSymbol arrayOverload = GetMethod(
            facts,
            "Visit",
            method => method.Parameters[0].Type.TypeKind == TypeKind.Array);
        IMethodSymbol stringOverload = GetMethod(
            facts,
            "Visit",
            method => method.Parameters[0].Type.SpecialType == SpecialType.System_String);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        InterproceduralAnalysisResult arrayResult = Known(ComplexityFactory.Linear(ComplexityVariable.N));
        InterproceduralAnalysisResult stringResult = Known(ComplexityFactory.LogN(ComplexityVariable.M));

        context.TemplateCache.StoreCompleted(arrayOverload, arrayResult, CancellationToken.None);
        context.TemplateCache.StoreCompleted(stringOverload, stringResult, CancellationToken.None);

        Assert.True(context.TemplateCache.TryGetCompleted(arrayOverload, CancellationToken.None, out InterproceduralAnalysisResult cachedArray));
        Assert.True(context.TemplateCache.TryGetCompleted(stringOverload, CancellationToken.None, out InterproceduralAnalysisResult cachedString));
        Assert.Same(arrayResult, cachedArray);
        Assert.Same(stringResult, cachedString);
        Assert.Equal(2, context.TemplateCache.Count);
    }

    [Fact]
    public void Root_states_keep_active_call_paths_independent()
    {
        CompilationFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Root()
                {
                }

                void Left()
                {
                }

                void Right()
                {
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        InterproceduralRootAnalysisState firstRoot = context.CreateRootState(GetMethod(facts, "Root"), CancellationToken.None);
        InterproceduralRootAnalysisState secondRoot = context.CreateRootState(GetMethod(facts, "Root"), CancellationToken.None);
        IMethodSymbol left = GetMethod(facts, "Left");
        IMethodSymbol right = GetMethod(facts, "Right");

        Assert.True(firstRoot.TryEnterMethod(left, out InterproceduralRootAnalysisState firstNext, out _));
        Assert.True(secondRoot.TryEnterMethod(right, out InterproceduralRootAnalysisState secondNext, out _));

        Assert.True(firstNext.ContainsActiveMethod(left));
        Assert.False(firstNext.ContainsActiveMethod(right));
        Assert.True(secondNext.ContainsActiveMethod(right));
        Assert.False(secondNext.ContainsActiveMethod(left));
    }

    [Fact]
    public void Root_state_increments_depth_when_entering_callee()
    {
        CompilationFacts facts = CreateRootAndCalleeFacts();
        InterproceduralRootAnalysisState root = CreateRootState(facts);
        IMethodSymbol callee = GetMethod(facts, "Callee");

        Assert.True(root.TryEnterMethod(callee, out InterproceduralRootAnalysisState next, out _));

        Assert.Equal(0, root.CurrentDepth);
        Assert.Equal(1, next.CurrentDepth);
        Assert.Equal(1, next.ExpandedMethodCount);
        Assert.True(next.ContainsActiveMethod(callee));
    }

    [Fact]
    public void Root_state_stops_at_maximum_depth()
    {
        CompilationFacts facts = CreateRootAndCalleeFacts();
        AnalysisBudget budget = new(maximumCallDepth: 1, maximumMethodsPerRootAnalysis: 32);
        InterproceduralRootAnalysisState root = InterproceduralRootAnalysisState.Create(
            GetMethod(facts, "Root"),
            budget,
            CancellationToken.None);
        IMethodSymbol callee = GetMethod(facts, "Callee");
        IMethodSymbol next = GetMethod(facts, "Next");

        Assert.True(root.TryEnterMethod(callee, out InterproceduralRootAnalysisState firstCallee, out _));
        Assert.False(firstCallee.TryEnterMethod(next, out InterproceduralRootAnalysisState unchanged, out InterproceduralAnalysisResult boundary));

        Assert.Same(firstCallee, unchanged);
        Assert.Equal(InterproceduralAnalysisResultKind.BudgetExceeded, boundary.Kind);
        _ = Assert.IsType<UnknownComplexity>(boundary.Complexity);
    }

    [Fact]
    public void Root_state_stops_at_method_count_budget()
    {
        CompilationFacts facts = CreateRootAndCalleeFacts();
        AnalysisBudget budget = new(maximumCallDepth: 5, maximumMethodsPerRootAnalysis: 1);
        InterproceduralRootAnalysisState root = InterproceduralRootAnalysisState.Create(
            GetMethod(facts, "Root"),
            budget,
            CancellationToken.None);
        IMethodSymbol callee = GetMethod(facts, "Callee");
        IMethodSymbol next = GetMethod(facts, "Next");

        Assert.True(root.TryEnterMethod(callee, out InterproceduralRootAnalysisState insideCallee, out _));
        InterproceduralRootAnalysisState afterCallee = insideCallee.ExitMethod(callee);
        Assert.False(afterCallee.TryEnterMethod(next, out InterproceduralRootAnalysisState unchanged, out InterproceduralAnalysisResult boundary));

        Assert.Same(afterCallee, unchanged);
        Assert.Equal(1, afterCallee.ExpandedMethodCount);
        Assert.Equal(InterproceduralAnalysisResultKind.BudgetExceeded, boundary.Kind);
    }

    [Fact]
    public void Root_state_detects_cycle_boundary_without_touching_shared_cache()
    {
        CompilationFacts facts = CreateRootAndCalleeFacts();
        InterproceduralRootAnalysisState root = CreateRootState(facts);
        IMethodSymbol rootMethod = GetMethod(facts, "Root");

        Assert.False(root.TryEnterMethod(rootMethod, out InterproceduralRootAnalysisState unchanged, out InterproceduralAnalysisResult boundary));

        Assert.Same(root, unchanged);
        Assert.Equal(InterproceduralAnalysisResultKind.CycleBoundary, boundary.Kind);
        _ = Assert.IsType<UnknownComplexity>(boundary.Complexity);
    }

    [Fact]
    public void Cancellation_is_respected_without_permanently_storing_cache_entries()
    {
        CompilationFacts facts = CreateRootAndCalleeFacts();
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        IMethodSymbol method = GetMethod(facts, "Root");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            context.TemplateCache.StoreCompleted(
                method,
                Known(ComplexityFactory.Constant()),
                cancellationTokenSource.Token));
        _ = Assert.Throws<OperationCanceledException>(() =>
            context.TemplateCache.TryGetCompleted(
                method,
                cancellationTokenSource.Token,
                out _));
        _ = Assert.Throws<OperationCanceledException>(() =>
            InterproceduralRootAnalysisState.Create(
                method,
                AnalysisBudget.Default,
                cancellationTokenSource.Token));

        Assert.Equal(0, context.TemplateCache.Count);
    }

    [Fact]
    public async Task Template_cache_supports_basic_concurrent_access()
    {
        CompilationFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void M0() { }
                void M1() { }
                void M2() { }
                void M3() { }
                void M4() { }
                void M5() { }
                void M6() { }
                void M7() { }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        ImmutableArray<IMethodSymbol> methods =
        [
            .. facts.SyntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(method => facts.SemanticModel.GetDeclaredSymbol(method, CancellationToken.None))
                .Where(method => method is not null)
                .Cast<IMethodSymbol>()
        ];

        await Task.WhenAll(methods.Select((method, index) => Task.Run(() =>
        {
            InterproceduralAnalysisResult result = Known(ComplexityFactory.Polynomial(
                ComplexityVariable.N,
                index + 1));

            Assert.True(context.TemplateCache.TryReserveAnalysis(
                method,
                CancellationToken.None,
                out InterproceduralAnalysisResult? completed));
            Assert.Null(completed);

            context.TemplateCache.StoreCompleted(method, result, CancellationToken.None);
            Assert.True(context.TemplateCache.TryGetCompleted(method, CancellationToken.None, out _));
        })));

        Assert.Equal(methods.Length, context.TemplateCache.Count);
    }

    private static InterproceduralRootAnalysisState CreateRootState(CompilationFacts facts)
    {
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        return context.CreateRootState(GetMethod(facts, "Root"), CancellationToken.None);
    }

    private static CompilationFacts CreateRootAndCalleeFacts()
    {
        return CreateFacts(
            """
            public sealed class Sample
            {
                void Root()
                {
                }

                void Callee()
                {
                }

                void Next()
                {
                }
            }
            """);
    }

    private static InterproceduralAnalysisResult Known(ComplexityExpression complexity)
    {
        return InterproceduralAnalysisResult.Known(
            new MethodComplexityTemplate(
                complexity,
                []));
    }

    private static IMethodSymbol GetMethod(
        CompilationFacts facts,
        string name,
        Func<IMethodSymbol, bool>? predicate = null)
    {
        return facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(method => facts.SemanticModel.GetDeclaredSymbol(method, CancellationToken.None))
            .Where(method => method is not null)
            .Cast<IMethodSymbol>()
            .Where(method => StringComparer.Ordinal.Equals(method.Name, name))
            .Single(predicate ?? (_ => true));
    }

    private static IMethodSymbol GetSingleInvocationTarget(CompilationFacts facts)
    {
        InvocationExpressionSyntax invocation = facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single();
        return facts.SemanticModel.GetSymbolInfo(invocation, CancellationToken.None).Symbol as IMethodSymbol
            ?? throw new InvalidOperationException("Expected invocation to resolve to a method symbol.");
    }

    private static CompilationFacts CreateFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "InterproceduralAnalysisContextTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        return new CompilationFacts(
            compilation,
            syntaxTree,
            compilation.GetSemanticModel(syntaxTree));
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
            .. trustedPlatformAssemblies.Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record CompilationFacts(
        CSharpCompilation Compilation,
        SyntaxTree SyntaxTree,
        SemanticModel SemanticModel);
}

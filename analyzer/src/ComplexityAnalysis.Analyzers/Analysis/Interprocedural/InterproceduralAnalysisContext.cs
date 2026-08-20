using System;
using System.Collections.Concurrent;
using System.Threading;

using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class InterproceduralAnalysisContext
{
    private readonly ConcurrentDictionary<MethodComplexityCacheKey, ComplexityExpression> _directRecurrenceSolutions =
        new(MethodComplexityCacheKey.Comparer);
    private readonly ConcurrentDictionary<SyntaxTree, SemanticModel> _semanticModels = new();
    private readonly ConcurrentDictionary<SyntaxTree, ComplexityAnalyzerOptions> _treeOptions = new();
    private readonly Func<SyntaxTree, ComplexityAnalyzerOptions> _optionsResolver;

    private InterproceduralAnalysisContext(
        Compilation compilation,
        SourceMethodResolver sourceMethodResolver,
        MethodComplexityTemplateCache templateCache,
        AnalysisBudget budget,
        Func<SyntaxTree, ComplexityAnalyzerOptions> optionsResolver)
    {
        Compilation = compilation;
        SourceMethodResolver = sourceMethodResolver;
        TemplateCache = templateCache;
        Budget = budget;
        _optionsResolver = optionsResolver ?? throw new ArgumentNullException(nameof(optionsResolver));
    }

    internal Compilation Compilation
    {
        get;
    }

    internal SourceMethodResolver SourceMethodResolver
    {
        get;
    }

    internal MethodComplexityTemplateCache TemplateCache
    {
        get;
    }

    internal AnalysisBudget Budget
    {
        get;
    }

    internal int DirectRecurrenceCacheCount
        => _directRecurrenceSolutions.Count;

    internal int SemanticModelCacheCount
        => _semanticModels.Count;

    internal static InterproceduralAnalysisContext Create(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        return Create(compilation, AnalysisBudget.Default, cancellationToken);
    }

    internal static InterproceduralAnalysisContext Create(
        Compilation compilation,
        AnalysisBudget budget,
        CancellationToken cancellationToken)
    {
        _ = compilation ?? throw new ArgumentNullException(nameof(compilation));
        _ = budget ?? throw new ArgumentNullException(nameof(budget));

        cancellationToken.ThrowIfCancellationRequested();

        return new InterproceduralAnalysisContext(
            compilation,
            new SourceMethodResolver(),
            new MethodComplexityTemplateCache(),
            budget,
            _ => ComplexityAnalyzerOptions.Default.WithAnalysisBudget(budget));
    }

    internal static InterproceduralAnalysisContext Create(
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        _ = compilation ?? throw new ArgumentNullException(nameof(compilation));
        _ = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));

        cancellationToken.ThrowIfCancellationRequested();

        return new InterproceduralAnalysisContext(
            compilation,
            new SourceMethodResolver(),
            new MethodComplexityTemplateCache(),
            AnalysisBudget.Default,
            syntaxTree => ComplexityAnalyzerOptionsReader.Read(optionsProvider, syntaxTree));
    }

    internal ComplexityAnalyzerOptions GetOptions(
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken)
    {
        _ = syntaxTree ?? throw new ArgumentNullException(nameof(syntaxTree));

        cancellationToken.ThrowIfCancellationRequested();

        return _treeOptions.GetOrAdd(syntaxTree, _optionsResolver);
    }

    internal SemanticModel GetSemanticModel(
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken)
    {
        _ = syntaxTree ?? throw new ArgumentNullException(nameof(syntaxTree));

        cancellationToken.ThrowIfCancellationRequested();

        SemanticModel semanticModel = _semanticModels.GetOrAdd(
            syntaxTree,
            tree => Compilation.GetSemanticModel(tree));

        cancellationToken.ThrowIfCancellationRequested();

        return semanticModel;
    }

    internal ComplexityAnalyzerOptions GetAnalysisOptions(
        SyntaxTree syntaxTree,
        AnalysisBudget rootBudget,
        CancellationToken cancellationToken)
    {
        _ = rootBudget ?? throw new ArgumentNullException(nameof(rootBudget));

        return GetOptions(syntaxTree, cancellationToken).WithAnalysisBudget(rootBudget);
    }

    internal InterproceduralRootAnalysisState CreateRootState(
        IMethodSymbol rootMethodSymbol,
        CancellationToken cancellationToken)
    {
        _ = rootMethodSymbol ?? throw new ArgumentNullException(nameof(rootMethodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        return InterproceduralRootAnalysisState.Create(
            rootMethodSymbol,
            Budget,
            cancellationToken);
    }

    internal static InterproceduralRootAnalysisState CreateRootState(
        IMethodSymbol rootMethodSymbol,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        _ = rootMethodSymbol ?? throw new ArgumentNullException(nameof(rootMethodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        return InterproceduralRootAnalysisState.Create(
            rootMethodSymbol,
            AnalysisBudget.FromOptions(options),
            cancellationToken);
    }

    internal bool TryGetDirectRecurrenceSolution(
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken,
        out ComplexityExpression complexity)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        return _directRecurrenceSolutions.TryGetValue(
            MethodComplexityCacheKey.Create(methodSymbol, options),
            out complexity);
    }

    internal void StoreDirectRecurrenceSolution(
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options,
        ComplexityExpression complexity,
        CancellationToken cancellationToken)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));
        _ = complexity ?? throw new ArgumentNullException(nameof(complexity));

        cancellationToken.ThrowIfCancellationRequested();

        _ = _directRecurrenceSolutions.TryAdd(
            MethodComplexityCacheKey.Create(methodSymbol, options),
            complexity);
    }
}

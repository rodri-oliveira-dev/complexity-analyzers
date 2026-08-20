using System;
using System.Collections.Concurrent;
using System.Threading;

using ComplexityAnalysis.Analyzers.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class InterproceduralAnalysisContext
{
    private readonly ConcurrentDictionary<SyntaxTree, ComplexityAnalyzerOptions> treeOptions = new();
    private readonly Func<SyntaxTree, ComplexityAnalyzerOptions> optionsResolver;

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
        this.optionsResolver = optionsResolver ?? throw new ArgumentNullException(nameof(optionsResolver));
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

        return treeOptions.GetOrAdd(syntaxTree, optionsResolver);
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

    internal InterproceduralRootAnalysisState CreateRootState(
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
}

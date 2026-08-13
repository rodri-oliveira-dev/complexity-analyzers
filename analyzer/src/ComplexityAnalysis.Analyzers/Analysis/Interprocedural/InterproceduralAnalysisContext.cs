using System;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class InterproceduralAnalysisContext
{
    private InterproceduralAnalysisContext(
        Compilation compilation,
        SourceMethodResolver sourceMethodResolver,
        MethodComplexityTemplateCache templateCache,
        AnalysisBudget budget)
    {
        Compilation = compilation;
        SourceMethodResolver = sourceMethodResolver;
        TemplateCache = templateCache;
        Budget = budget;
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
            budget);
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
}

using System;
using System.Collections.Immutable;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class MethodAnalysisContext
{
    private MethodAnalysisContext(
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        ImmutableDictionary<ISymbol, ComplexityVariable> inputSizeVariables,
        ImmutableDictionary<ISymbol, LoopBoundExpression> localLoopBounds,
        InterproceduralAnalysisContext? interproceduralContext,
        InterproceduralRootAnalysisState? interproceduralRootState,
        ComplexityAnalyzerOptions options,
        bool treatsDirectRecursiveInvocationsAsConstant,
        CancellationToken cancellationToken)
    {
        SemanticModel = semanticModel;
        MethodSymbol = methodSymbol;
        InputSizeVariables = inputSizeVariables;
        LocalLoopBounds = localLoopBounds;
        InterproceduralContext = interproceduralContext;
        InterproceduralRootState = interproceduralRootState;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        TreatsDirectRecursiveInvocationsAsConstant = treatsDirectRecursiveInvocationsAsConstant;
        CancellationToken = cancellationToken;
    }

    internal SemanticModel SemanticModel
    {
        get;
    }

    internal IMethodSymbol MethodSymbol
    {
        get;
    }

    internal ImmutableDictionary<ISymbol, ComplexityVariable> InputSizeVariables
    {
        get;
    }

    internal ImmutableDictionary<ISymbol, LoopBoundExpression> LocalLoopBounds
    {
        get;
    }

    internal InterproceduralAnalysisContext? InterproceduralContext
    {
        get;
    }

    internal InterproceduralRootAnalysisState? InterproceduralRootState
    {
        get;
    }

    internal ComplexityAnalyzerOptions Options
    {
        get;
    }

    internal bool TreatsDirectRecursiveInvocationsAsConstant
    {
        get;
    }

    internal CancellationToken CancellationToken
    {
        get;
    }

    internal static MethodAnalysisContext Create(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        IMethodSymbol methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) as IMethodSymbol
            ?? throw new InvalidOperationException("The method declaration must resolve to a method symbol.");

        return Create(semanticModel, methodSymbol, cancellationToken);
    }

    internal static MethodAnalysisContext Create(
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        return Create(
            semanticModel,
            methodSymbol,
            ComplexityAnalyzerOptions.Default,
            interproceduralContext: null,
            interproceduralRootState: null,
            cancellationToken);
    }

    internal static MethodAnalysisContext Create(
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        InterproceduralAnalysisContext? interproceduralContext,
        InterproceduralRootAnalysisState? interproceduralRootState,
        CancellationToken cancellationToken)
    {
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        ComplexityAnalyzerOptions options = interproceduralContext is null
            ? ComplexityAnalyzerOptions.Default
            : interproceduralContext.GetAnalysisOptions(
                semanticModel.SyntaxTree,
                interproceduralRootState?.Budget ?? interproceduralContext.Budget,
                cancellationToken);

        return Create(
            semanticModel,
            methodSymbol,
            options,
            interproceduralContext,
            interproceduralRootState,
            cancellationToken);
    }

    internal static MethodAnalysisContext Create(
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        ComplexityAnalyzerOptions options,
        InterproceduralAnalysisContext? interproceduralContext,
        InterproceduralRootAnalysisState? interproceduralRootState,
        CancellationToken cancellationToken)
    {
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        InputSizeResolver resolver = new(semanticModel, cancellationToken);
        ImmutableDictionary<ISymbol, ComplexityVariable> inputSizeVariables =
            resolver.ResolveParameterVariables(methodSymbol);

        return new MethodAnalysisContext(
            semanticModel,
            methodSymbol,
            inputSizeVariables,
            ImmutableDictionary.Create<ISymbol, LoopBoundExpression>(SymbolEqualityComparer.Default),
            interproceduralContext,
            interproceduralRootState,
            options,
            treatsDirectRecursiveInvocationsAsConstant: false,
            cancellationToken);
    }

    internal bool TryGetInputSizeVariable(ISymbol symbol, out ComplexityVariable variable)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        CancellationToken.ThrowIfCancellationRequested();

        return InputSizeVariables.TryGetValue(symbol, out variable!);
    }

    internal bool TryGetLocalLoopBound(ISymbol symbol, out LoopBoundExpression bound)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        CancellationToken.ThrowIfCancellationRequested();

        return LocalLoopBounds.TryGetValue(symbol, out bound);
    }

    internal MethodAnalysisContext WithLocalLoopBound(ISymbol symbol, LoopBoundExpression bound)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        CancellationToken.ThrowIfCancellationRequested();

        return new MethodAnalysisContext(
            SemanticModel,
            MethodSymbol,
            InputSizeVariables,
            LocalLoopBounds.SetItem(symbol, bound),
            InterproceduralContext,
            InterproceduralRootState,
            Options,
            TreatsDirectRecursiveInvocationsAsConstant,
            CancellationToken);
    }

    internal MethodAnalysisContext WithoutLocalLoopBound(ISymbol symbol)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        CancellationToken.ThrowIfCancellationRequested();

        return LocalLoopBounds.ContainsKey(symbol)
            ? new MethodAnalysisContext(
                SemanticModel,
                MethodSymbol,
                InputSizeVariables,
                LocalLoopBounds.Remove(symbol),
                InterproceduralContext,
                InterproceduralRootState,
                Options,
                TreatsDirectRecursiveInvocationsAsConstant,
                CancellationToken)
            : this;
    }

    internal MethodAnalysisContext WithDirectRecursiveInvocationsAsConstant()
    {
        CancellationToken.ThrowIfCancellationRequested();

        return TreatsDirectRecursiveInvocationsAsConstant
            ? this
            : new MethodAnalysisContext(
                SemanticModel,
                MethodSymbol,
                InputSizeVariables,
                LocalLoopBounds,
                InterproceduralContext,
                InterproceduralRootState,
                Options,
                treatsDirectRecursiveInvocationsAsConstant: true,
                CancellationToken);
    }
}

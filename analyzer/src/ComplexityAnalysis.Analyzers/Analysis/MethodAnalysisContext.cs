using System;
using System.Collections.Immutable;
using System.Threading;

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
        CancellationToken cancellationToken)
    {
        SemanticModel = semanticModel;
        MethodSymbol = methodSymbol;
        InputSizeVariables = inputSizeVariables;
        LocalLoopBounds = localLoopBounds;
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
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        InputSizeResolver resolver = new(semanticModel, cancellationToken);
        ImmutableDictionary<ISymbol, ComplexityVariable> inputSizeVariables =
            resolver.ResolveParameterVariables(methodSymbol);

        return new MethodAnalysisContext(
            semanticModel,
            methodSymbol,
            inputSizeVariables,
            ImmutableDictionary.Create<ISymbol, LoopBoundExpression>(SymbolEqualityComparer.Default),
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
                CancellationToken)
            : this;
    }
}

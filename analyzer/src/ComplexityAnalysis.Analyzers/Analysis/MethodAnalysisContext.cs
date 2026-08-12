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
        CancellationToken cancellationToken)
    {
        SemanticModel = semanticModel;
        MethodSymbol = methodSymbol;
        InputSizeVariables = inputSizeVariables;
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
            cancellationToken);
    }

    internal bool TryGetInputSizeVariable(ISymbol symbol, out ComplexityVariable variable)
    {
        _ = symbol ?? throw new ArgumentNullException(nameof(symbol));

        CancellationToken.ThrowIfCancellationRequested();

        return InputSizeVariables.TryGetValue(symbol, out variable!);
    }
}

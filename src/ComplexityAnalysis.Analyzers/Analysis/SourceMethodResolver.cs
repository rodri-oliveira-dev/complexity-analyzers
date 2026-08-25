using System;
using System.Collections.Immutable;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class SourceMethodResolver
{
    private readonly KnownOperationResolver knownOperationResolver;

    internal SourceMethodResolver()
        : this(new KnownOperationResolver(KnownOperationRegistry.Default))
    {
    }

    internal SourceMethodResolver(KnownOperationResolver knownOperationResolver)
    {
        this.knownOperationResolver = knownOperationResolver
            ?? throw new ArgumentNullException(nameof(knownOperationResolver));
    }

    internal CallTargetResolution Resolve(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        _ = invocation ?? throw new ArgumentNullException(nameof(invocation));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol targetMethodSymbol)
        {
            return CallTargetResolution.Unsupported();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (knownOperationResolver.TryResolve(
            targetMethodSymbol,
            cancellationToken,
            out KnownOperationMapping knownOperationMapping))
        {
            return CallTargetResolution.KnownOperation(targetMethodSymbol, knownOperationMapping);
        }

        IMethodSymbol sourceMethodDefinition = GetSourceMethodDefinition(targetMethodSymbol);
        return IsSafeSourceDispatch(targetMethodSymbol, sourceMethodDefinition)
            && TryGetSourceMethodDeclaration(
                sourceMethodDefinition,
                cancellationToken,
                out MethodDeclarationSyntax? sourceMethodDeclaration)
            && sourceMethodDeclaration is not null
            ? CallTargetResolution.SourceMethod(
                targetMethodSymbol,
                sourceMethodDefinition,
                sourceMethodDeclaration)
            : CallTargetResolution.Unsupported();
    }

    private static IMethodSymbol GetSourceMethodDefinition(IMethodSymbol methodSymbol)
    {
        IMethodSymbol sourceMethod = methodSymbol.ReducedFrom ?? methodSymbol;
        return sourceMethod.OriginalDefinition;
    }

    private static bool TryGetSourceMethodDeclaration(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken,
        out MethodDeclarationSyntax? methodDeclaration)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ImmutableArray<SyntaxReference> syntaxReferences = methodSymbol.DeclaringSyntaxReferences;
        if (syntaxReferences.IsDefaultOrEmpty)
        {
            methodDeclaration = null;
            return false;
        }

        foreach (SyntaxReference syntaxReference in syntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (syntaxReference.GetSyntax(cancellationToken) is MethodDeclarationSyntax declaration)
            {
                methodDeclaration = declaration;
                return true;
            }
        }

        methodDeclaration = null;
        return false;
    }

    private static bool IsSafeSourceDispatch(
        IMethodSymbol targetMethodSymbol,
        IMethodSymbol sourceMethodDefinition)
    {
        bool isUnsupportedDispatch = targetMethodSymbol.MethodKind == MethodKind.DelegateInvoke
            || sourceMethodDefinition.MethodKind != MethodKind.Ordinary
            || IsInterfaceMethod(targetMethodSymbol)
            || IsInterfaceMethod(sourceMethodDefinition)
            || targetMethodSymbol.IsAbstract
            || sourceMethodDefinition.IsAbstract;

        bool isSupportedDispatch = sourceMethodDefinition.IsStatic
            || sourceMethodDefinition.DeclaredAccessibility == Accessibility.Private
            || IsSealedDispatch(targetMethodSymbol)
            || IsSealedDispatch(sourceMethodDefinition)
            || (!targetMethodSymbol.IsVirtual
                && !targetMethodSymbol.IsOverride
                && !sourceMethodDefinition.IsVirtual
                && !sourceMethodDefinition.IsOverride);

        return !isUnsupportedDispatch && isSupportedDispatch;
    }

    private static bool IsSealedDispatch(IMethodSymbol methodSymbol)
    {
        return methodSymbol.IsSealed
            || methodSymbol.ContainingType?.IsSealed == true;
    }

    private static bool IsInterfaceMethod(IMethodSymbol methodSymbol)
    {
        return methodSymbol.ContainingType?.TypeKind == TypeKind.Interface;
    }
}

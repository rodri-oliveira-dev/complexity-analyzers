using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal static class KnownOperationInvocation
{
    internal static bool TryResolve(
        InvocationExpressionSyntax invocation,
        MethodAnalysisContext context,
        KnownOperationResolver resolver,
        out IMethodSymbol? methodSymbol,
        out KnownOperationMapping? mapping)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        methodSymbol = symbolInfo.Symbol as IMethodSymbol;
        if (methodSymbol is null
            || !resolver.TryResolve(methodSymbol, context.CancellationToken, out KnownOperationMapping resolvedMapping))
        {
            mapping = null;
            return false;
        }

        mapping = resolvedMapping;
        return true;
    }

    internal static bool TryGetReceiverExpression(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        out ExpressionSyntax? receiver)
    {
        receiver = null;

        if (methodSymbol.ReducedFrom is not null
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            receiver = memberAccess.Expression;
            return true;
        }

        if (methodSymbol.ReducedFrom is null
            && invocation.Expression is MemberAccessExpressionSyntax
            && methodSymbol.IsStatic
            && invocation.ArgumentList.Arguments.Count > 0)
        {
            receiver = invocation.ArgumentList.Arguments[0].Expression;
            return true;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax instanceMemberAccess)
        {
            receiver = instanceMemberAccess.Expression;
            return true;
        }

        return false;
    }
}

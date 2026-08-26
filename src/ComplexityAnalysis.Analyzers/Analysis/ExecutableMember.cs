using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class ExecutableMember
{
    private ExecutableMember(
        SyntaxNode declaration,
        IMethodSymbol symbol,
        ExecutableMemberBody body,
        Location diagnosticLocation,
        string displayName)
    {
        Declaration = declaration ?? throw new ArgumentNullException(nameof(declaration));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Body = body;
        DiagnosticLocation = diagnosticLocation ?? throw new ArgumentNullException(nameof(diagnosticLocation));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    internal SyntaxNode Declaration
    {
        get;
    }

    internal IMethodSymbol Symbol
    {
        get;
    }

    internal ExecutableMemberBody Body
    {
        get;
    }

    internal Location DiagnosticLocation
    {
        get;
    }

    internal string DisplayName
    {
        get;
    }

    internal SyntaxTree SyntaxTree
        => Declaration.SyntaxTree;

    internal static bool TryCreateOrdinaryMethod(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            member = null;
            return false;
        }

        member = CreateOrdinaryMethod(methodDeclaration, methodSymbol);
        return true;
    }

    internal static ExecutableMember CreateOrdinaryMethod(
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        ExecutableMemberBody body = methodDeclaration.Body is not null
            ? ExecutableMemberBody.FromBlock(methodDeclaration.Body)
            : methodDeclaration.ExpressionBody is not null
                ? ExecutableMemberBody.FromExpression(methodDeclaration.ExpressionBody.Expression)
                : ExecutableMemberBody.None();

        return new ExecutableMember(
            methodDeclaration,
            methodSymbol,
            body,
            methodDeclaration.Identifier.GetLocation(),
            methodDeclaration.Identifier.ValueText);
    }
}

using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class ExecutableMember
{
    private ExecutableMember(
        SyntaxNode declaration,
        IMethodSymbol symbol,
        ExecutableMemberBody body,
        Location diagnosticLocation,
        string displayName,
        ExecutableMemberKind kind)
    {
        Declaration = declaration ?? throw new ArgumentNullException(nameof(declaration));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Body = body;
        DiagnosticLocation = diagnosticLocation ?? throw new ArgumentNullException(nameof(diagnosticLocation));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Kind = kind;
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

    internal ExecutableMemberKind Kind
    {
        get;
    }

    internal SyntaxTree SyntaxTree
        => Declaration.SyntaxTree;

    internal bool SupportsDirectRecursion
        => Kind is not ExecutableMemberKind.Lambda
            and not ExecutableMemberKind.AnonymousMethod
            and not ExecutableMemberKind.ExpressionBodiedProperty;

    internal static bool TryCreate(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        _ = node ?? throw new ArgumentNullException(nameof(node));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        switch (node)
        {
            case MethodDeclarationSyntax methodDeclaration:
                return TryCreateOrdinaryMethod(methodDeclaration, semanticModel, cancellationToken, out member);
            case ConstructorDeclarationSyntax constructorDeclaration:
                return TryCreateConstructor(constructorDeclaration, semanticModel, cancellationToken, out member);
            case AccessorDeclarationSyntax accessorDeclaration:
                return TryCreateAccessor(accessorDeclaration, semanticModel, cancellationToken, out member);
            case OperatorDeclarationSyntax operatorDeclaration:
                return TryCreateOperator(operatorDeclaration, semanticModel, cancellationToken, out member);
            case ConversionOperatorDeclarationSyntax conversionDeclaration:
                return TryCreateConversionOperator(conversionDeclaration, semanticModel, cancellationToken, out member);
            case LocalFunctionStatementSyntax localFunction:
                return TryCreateLocalFunction(localFunction, semanticModel, cancellationToken, out member);
            case LambdaExpressionSyntax lambda:
                return TryCreateLambda(lambda, semanticModel, cancellationToken, out member);
            case AnonymousMethodExpressionSyntax anonymousMethod:
                return TryCreateAnonymousMethod(anonymousMethod, semanticModel, cancellationToken, out member);
            case PropertyDeclarationSyntax propertyDeclaration:
                return TryCreateExpressionBodiedProperty(propertyDeclaration, semanticModel, cancellationToken, out member);
            default:
                member = null;
                return false;
        }
    }

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
            methodDeclaration.Identifier.ValueText,
            ExecutableMemberKind.OrdinaryMethod);
    }

    internal static bool TryCreateConstructor(
        ConstructorDeclarationSyntax constructorDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        _ = constructorDeclaration ?? throw new ArgumentNullException(nameof(constructorDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetDeclaredSymbol(constructorDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
            || !HasBody(constructorDeclaration.Body, constructorDeclaration.ExpressionBody))
        {
            member = null;
            return false;
        }

        ExecutableMemberBody body = CreateBody(constructorDeclaration.Body, constructorDeclaration.ExpressionBody);
        string suffix = methodSymbol.MethodKind == MethodKind.StaticConstructor
            ? ".cctor"
            : ".ctor";
        member = new ExecutableMember(
            constructorDeclaration,
            methodSymbol,
            body,
            constructorDeclaration.Identifier.GetLocation(),
            FormatContainingType(methodSymbol) + suffix,
            ExecutableMemberKind.Constructor);
        return true;
    }

    internal static bool TryCreateAccessor(
        AccessorDeclarationSyntax accessorDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        _ = accessorDeclaration ?? throw new ArgumentNullException(nameof(accessorDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetDeclaredSymbol(accessorDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
            || !HasBody(accessorDeclaration.Body, accessorDeclaration.ExpressionBody))
        {
            member = null;
            return false;
        }

        member = new ExecutableMember(
            accessorDeclaration,
            methodSymbol,
            CreateBody(accessorDeclaration.Body, accessorDeclaration.ExpressionBody),
            accessorDeclaration.Keyword.GetLocation(),
            FormatAccessor(methodSymbol),
            ExecutableMemberKind.Accessor);
        return true;
    }

    internal static bool TryCreateOperator(
        OperatorDeclarationSyntax operatorDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        _ = operatorDeclaration ?? throw new ArgumentNullException(nameof(operatorDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetDeclaredSymbol(operatorDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
            || !HasBody(operatorDeclaration.Body, operatorDeclaration.ExpressionBody))
        {
            member = null;
            return false;
        }

        member = new ExecutableMember(
            operatorDeclaration,
            methodSymbol,
            CreateBody(operatorDeclaration.Body, operatorDeclaration.ExpressionBody),
            operatorDeclaration.OperatorKeyword.GetLocation(),
            "operator " + operatorDeclaration.OperatorToken.Text,
            ExecutableMemberKind.Operator);
        return true;
    }

    internal static bool TryCreateConversionOperator(
        ConversionOperatorDeclarationSyntax conversionDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        _ = conversionDeclaration ?? throw new ArgumentNullException(nameof(conversionDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetDeclaredSymbol(conversionDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
            || !HasBody(conversionDeclaration.Body, conversionDeclaration.ExpressionBody))
        {
            member = null;
            return false;
        }

        member = new ExecutableMember(
            conversionDeclaration,
            methodSymbol,
            CreateBody(conversionDeclaration.Body, conversionDeclaration.ExpressionBody),
            conversionDeclaration.ImplicitOrExplicitKeyword.GetLocation(),
            conversionDeclaration.ImplicitOrExplicitKeyword.Text
                + " operator "
                + conversionDeclaration.Type.ToString(),
            ExecutableMemberKind.ConversionOperator);
        return true;
    }

    internal static bool TryCreateLocalFunction(
        LocalFunctionStatementSyntax localFunction,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        _ = localFunction ?? throw new ArgumentNullException(nameof(localFunction));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetDeclaredSymbol(localFunction, cancellationToken) is not IMethodSymbol methodSymbol
            || !HasBody(localFunction.Body, localFunction.ExpressionBody))
        {
            member = null;
            return false;
        }

        member = CreateLocalFunction(localFunction, methodSymbol);
        return true;
    }

    internal static ExecutableMember CreateLocalFunction(
        LocalFunctionStatementSyntax localFunction,
        IMethodSymbol methodSymbol)
    {
        _ = localFunction ?? throw new ArgumentNullException(nameof(localFunction));
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        return new ExecutableMember(
            localFunction,
            methodSymbol,
            CreateBody(localFunction.Body, localFunction.ExpressionBody),
            localFunction.Identifier.GetLocation(),
            localFunction.Identifier.ValueText,
            ExecutableMemberKind.LocalFunction);
    }

    private static bool TryCreateLambda(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetOperation(lambda, cancellationToken) is not IAnonymousFunctionOperation operation
            || operation.Symbol is not IMethodSymbol methodSymbol)
        {
            member = null;
            return false;
        }

        ExecutableMemberBody body = lambda.Body switch
        {
            BlockSyntax block => ExecutableMemberBody.FromBlock(block),
            ExpressionSyntax expression => ExecutableMemberBody.FromExpression(expression),
            _ => ExecutableMemberBody.None(),
        };

        if (!body.HasBody)
        {
            member = null;
            return false;
        }

        member = new ExecutableMember(
            lambda,
            methodSymbol,
            body,
            lambda.ArrowToken.GetLocation(),
            "lambda",
            ExecutableMemberKind.Lambda);
        return true;
    }

    private static bool TryCreateAnonymousMethod(
        AnonymousMethodExpressionSyntax anonymousMethod,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (semanticModel.GetOperation(anonymousMethod, cancellationToken) is not IAnonymousFunctionOperation operation
            || operation.Symbol is not IMethodSymbol methodSymbol
            || anonymousMethod.Block is null)
        {
            member = null;
            return false;
        }

        member = new ExecutableMember(
            anonymousMethod,
            methodSymbol,
            ExecutableMemberBody.FromBlock(anonymousMethod.Block),
            anonymousMethod.DelegateKeyword.GetLocation(),
            "anonymous method",
            ExecutableMemberKind.AnonymousMethod);
        return true;
    }

    private static bool TryCreateExpressionBodiedProperty(
        PropertyDeclarationSyntax propertyDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember? member)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (propertyDeclaration.ExpressionBody is null
            || semanticModel.GetDeclaredSymbol(propertyDeclaration, cancellationToken) is not IPropertySymbol propertySymbol
            || propertySymbol.GetMethod is not IMethodSymbol getterSymbol)
        {
            member = null;
            return false;
        }

        member = new ExecutableMember(
            propertyDeclaration,
            getterSymbol,
            ExecutableMemberBody.FromExpression(propertyDeclaration.ExpressionBody.Expression),
            propertyDeclaration.Identifier.GetLocation(),
            propertyDeclaration.Identifier.ValueText + ".get",
            ExecutableMemberKind.ExpressionBodiedProperty);
        return true;
    }

    private static bool HasBody(BlockSyntax? block, ArrowExpressionClauseSyntax? expressionBody)
    {
        return block is not null || expressionBody is not null;
    }

    private static ExecutableMemberBody CreateBody(
        BlockSyntax? block,
        ArrowExpressionClauseSyntax? expressionBody)
    {
        return block is not null
            ? ExecutableMemberBody.FromBlock(block)
            : expressionBody is not null
                ? ExecutableMemberBody.FromExpression(expressionBody.Expression)
                : ExecutableMemberBody.None();
    }

    private static string FormatAccessor(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.AssociatedSymbol is IPropertySymbol propertySymbol)
        {
            return propertySymbol.Name + "." + FormatAccessorSuffix(methodSymbol.MethodKind);
        }
        else if (methodSymbol.AssociatedSymbol is IEventSymbol eventSymbol)
        {
            return eventSymbol.Name + "." + FormatAccessorSuffix(methodSymbol.MethodKind);
        }

        return methodSymbol.Name;
    }

    private static string FormatAccessorSuffix(MethodKind methodKind)
    {
        return methodKind == MethodKind.PropertyGet
            ? "get"
            : methodKind == MethodKind.PropertySet
                ? "set"
                : methodKind == MethodKind.EventAdd
                    ? "add"
                    : methodKind == MethodKind.EventRemove
                        ? "remove"
                        : "accessor";
    }

    private static string FormatContainingType(IMethodSymbol methodSymbol)
    {
        return methodSymbol.ContainingType?.Name ?? methodSymbol.Name;
    }
}

internal enum ExecutableMemberKind
{
    OrdinaryMethod,
    Constructor,
    Accessor,
    Operator,
    ConversionOperator,
    LocalFunction,
    Lambda,
    AnonymousMethod,
    ExpressionBodiedProperty,
}

using System;
using System.Collections.Immutable;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class ArgumentComplexityBinder
{
    private static readonly KnownOperationResolver KnownOperationResolver = new(KnownOperationRegistry.Default);

    internal ImmutableDictionary<ComplexityVariable, ComplexityExpression> Bind(
        InvocationExpressionSyntax invocation,
        IMethodSymbol targetMethodSymbol,
        IMethodSymbol sourceMethodDefinition,
        MethodComplexityTemplate calleeTemplate,
        MethodAnalysisContext callerContext,
        CancellationToken cancellationToken)
    {
        _ = invocation ?? throw new ArgumentNullException(nameof(invocation));
        _ = targetMethodSymbol ?? throw new ArgumentNullException(nameof(targetMethodSymbol));
        _ = sourceMethodDefinition ?? throw new ArgumentNullException(nameof(sourceMethodDefinition));
        _ = calleeTemplate ?? throw new ArgumentNullException(nameof(calleeTemplate));
        _ = callerContext ?? throw new ArgumentNullException(nameof(callerContext));

        cancellationToken.ThrowIfCancellationRequested();

        ImmutableDictionary<IParameterSymbol, ExpressionSyntax> argumentsByParameter =
            ArgumentParameterBinder.BindArgumentsToParameters(
                invocation,
                targetMethodSymbol,
                sourceMethodDefinition.Parameters,
                cancellationToken);
        ImmutableDictionary<ComplexityVariable, ComplexityExpression>.Builder bindings =
            ImmutableDictionary.CreateBuilder<ComplexityVariable, ComplexityExpression>();

        foreach (KeyValuePair<IParameterSymbol, ComplexityVariable> pair in calleeTemplate.ParameterVariables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bindings[pair.Value] = argumentsByParameter.TryGetValue(pair.Key, out ExpressionSyntax? argument)
                ? ResolveArgumentComplexity(argument, callerContext, cancellationToken)
                : ComplexityFactory.Unknown();
        }

        return bindings.ToImmutable();
    }

    private static ComplexityExpression ResolveArgumentComplexity(
        ExpressionSyntax expression,
        MethodAnalysisContext callerContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);

        if (callerContext.SemanticModel.GetConstantValue(expression, cancellationToken).HasValue)
        {
            return ComplexityFactory.Constant();
        }

        if (TryResolveCallerDimension(expression, callerContext, cancellationToken, out ComplexityVariable? variable)
            && variable is not null)
        {
            return ComplexityFactory.Linear(variable);
        }

        if (TryResolveSimpleSizePreservingTransformation(
            expression,
            callerContext,
            cancellationToken,
            out ComplexityExpression? transformedComplexity)
            && transformedComplexity is not null)
        {
            return transformedComplexity;
        }

        return ComplexityFactory.Unknown();
    }

    private static bool TryResolveSimpleSizePreservingTransformation(
        ExpressionSyntax expression,
        MethodAnalysisContext callerContext,
        CancellationToken cancellationToken,
        out ComplexityExpression? complexity)
    {
        cancellationToken.ThrowIfCancellationRequested();

        complexity = null;
        expression = UnwrapParentheses(expression);

        if (expression is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        ExpressionSyntax left = UnwrapParentheses(binary.Left);
        ExpressionSyntax right = UnwrapParentheses(binary.Right);
        bool leftIsDimension = TryResolveCallerDimension(left, callerContext, cancellationToken, out ComplexityVariable? leftVariable)
            && leftVariable is not null;
        bool rightIsDimension = TryResolveCallerDimension(right, callerContext, cancellationToken, out ComplexityVariable? rightVariable)
            && rightVariable is not null;
        bool leftIsConstant = TryGetIntegerConstant(left, callerContext, cancellationToken, out long leftConstant);
        bool rightIsConstant = TryGetIntegerConstant(right, callerContext, cancellationToken, out long rightConstant);

        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            if (leftIsDimension && rightIsConstant)
            {
                complexity = ComplexityFactory.Linear(leftVariable!);
                return true;
            }

            if (rightIsDimension && leftIsConstant)
            {
                complexity = ComplexityFactory.Linear(rightVariable!);
                return true;
            }
        }

        if (binary.IsKind(SyntaxKind.SubtractExpression)
            && leftIsDimension
            && rightIsConstant)
        {
            complexity = ComplexityFactory.Linear(leftVariable!);
            return true;
        }

        if (binary.IsKind(SyntaxKind.MultiplyExpression))
        {
            if (leftIsDimension && rightIsConstant)
            {
                complexity = rightConstant == 0
                    ? ComplexityFactory.Constant()
                    : ComplexityFactory.Linear(leftVariable!);
                return true;
            }

            if (rightIsDimension && leftIsConstant)
            {
                complexity = leftConstant == 0
                    ? ComplexityFactory.Constant()
                    : ComplexityFactory.Linear(rightVariable!);
                return true;
            }
        }

        if (binary.IsKind(SyntaxKind.DivideExpression)
            && leftIsDimension
            && rightIsConstant
            && rightConstant != 0)
        {
            complexity = ComplexityFactory.Linear(leftVariable!);
            return true;
        }

        return false;
    }

    private static bool TryResolveCallerDimension(
        ExpressionSyntax expression,
        MethodAnalysisContext callerContext,
        CancellationToken cancellationToken,
        out ComplexityVariable? variable)
    {
        cancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        variable = null;

        if (expression is IdentifierNameSyntax)
        {
            SymbolInfo symbolInfo = callerContext.SemanticModel.GetSymbolInfo(expression, cancellationToken);
            if (symbolInfo.Symbol is not null
                && callerContext.TryGetInputSizeVariable(symbolInfo.Symbol, out ComplexityVariable inputVariable))
            {
                variable = inputVariable;
                return true;
            }

            if (symbolInfo.Symbol is not null
                && callerContext.TryGetLocalLoopBound(symbolInfo.Symbol, out LoopBoundExpression localBound)
                && localBound.IsVariable)
            {
                variable = localBound.Variable;
                return true;
            }

            return false;
        }

        return expression is MemberAccessExpressionSyntax memberAccess
            && TryResolveKnownSizeProperty(memberAccess, callerContext, cancellationToken, out variable);
    }

    private static bool TryResolveKnownSizeProperty(
        MemberAccessExpressionSyntax memberAccess,
        MethodAnalysisContext callerContext,
        CancellationToken cancellationToken,
        out ComplexityVariable? variable)
    {
        cancellationToken.ThrowIfCancellationRequested();

        variable = null;
        ISymbol? symbol = callerContext.SemanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
        if (symbol is not IPropertySymbol propertySymbol
            || !KnownOperationResolver.TryResolve(propertySymbol, cancellationToken, out KnownOperationMapping mapping)
            || mapping.Complexity is not ConstantComplexity
            || mapping.Metadata.EnumeratesReceiver)
        {
            return false;
        }

        string operationFamily = mapping.Metadata.OperationFamily;
        if (!StringComparer.Ordinal.Equals(operationFamily, "array-length")
            && !StringComparer.Ordinal.Equals(operationFamily, "string-length")
            && !StringComparer.Ordinal.Equals(operationFamily, "list-count"))
        {
            return false;
        }

        return TryResolveCallerDimension(memberAccess.Expression, callerContext, cancellationToken, out variable)
            && variable is not null;
    }

    private static bool TryGetIntegerConstant(
        ExpressionSyntax expression,
        MethodAnalysisContext callerContext,
        CancellationToken cancellationToken,
        out long value)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Optional<object?> constantValue = callerContext.SemanticModel.GetConstantValue(
            UnwrapParentheses(expression),
            cancellationToken);

        if (constantValue.HasValue
            && constantValue.Value is not null
            && TryConvertIntegerConstant(constantValue.Value, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryConvertIntegerConstant(object value, out long integer)
    {
        switch (value)
        {
            case sbyte sbyteValue:
                integer = sbyteValue;
                return true;
            case byte byteValue:
                integer = byteValue;
                return true;
            case short shortValue:
                integer = shortValue;
                return true;
            case ushort ushortValue:
                integer = ushortValue;
                return true;
            case int intValue:
                integer = intValue;
                return true;
            case uint uintValue:
                integer = uintValue;
                return true;
            case long longValue:
                integer = longValue;
                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                integer = (long)ulongValue;
                return true;
            default:
                integer = default;
                return false;
        }
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

}

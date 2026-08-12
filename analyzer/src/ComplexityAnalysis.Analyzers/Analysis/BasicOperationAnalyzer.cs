using System;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class BasicOperationAnalyzer
{
    private readonly MethodAnalysisContext context;

    internal BasicOperationAnalyzer(MethodAnalysisContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    internal ComplexityExpression AnalyzeStatement(StatementSyntax statement)
    {
        _ = statement ?? throw new ArgumentNullException(nameof(statement));

        CancellationToken cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        return statement switch
        {
            EmptyStatementSyntax => ComplexityFactory.Constant(),
            BreakStatementSyntax => ComplexityFactory.Constant(),
            LocalDeclarationStatementSyntax localDeclaration => AnalyzeLocalDeclaration(localDeclaration),
            ExpressionStatementSyntax expressionStatement => AnalyzeExpressionStatement(expressionStatement),
            ReturnStatementSyntax returnStatement => AnalyzeReturn(returnStatement),
            _ => ComplexityFactory.Unknown(),
        };
    }

    internal ComplexityExpression AnalyzeExpression(ExpressionSyntax expression)
    {
        _ = expression ?? throw new ArgumentNullException(nameof(expression));

        CancellationToken cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        return expression switch
        {
            LiteralExpressionSyntax => ComplexityFactory.Constant(),
            IdentifierNameSyntax identifier => IsSimpleValueAccess(identifier)
                ? ComplexityFactory.Constant()
                : ComplexityFactory.Unknown(),
            ParenthesizedExpressionSyntax parenthesized => AnalyzeExpression(parenthesized.Expression),
            BinaryExpressionSyntax binary => AnalyzeBinaryExpression(binary),
            AssignmentExpressionSyntax assignment => AnalyzeAssignment(assignment),
            PrefixUnaryExpressionSyntax prefixUnary => AnalyzePrefixUnary(prefixUnary),
            PostfixUnaryExpressionSyntax postfixUnary => AnalyzePostfixUnary(postfixUnary),
            MemberAccessExpressionSyntax memberAccess => AnalyzeMemberAccess(memberAccess),
            ElementAccessExpressionSyntax elementAccess => AnalyzeElementAccess(elementAccess),
            _ => ComplexityFactory.Unknown(),
        };
    }

    private ComplexityExpression AnalyzeLocalDeclaration(LocalDeclarationStatementSyntax localDeclaration)
    {
        ComplexityExpression complexity = ComplexityFactory.Constant();

        foreach (VariableDeclaratorSyntax variable in localDeclaration.Declaration.Variables)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            ComplexityExpression initializerComplexity = variable.Initializer is null
                ? ComplexityFactory.Constant()
                : AnalyzeExpression(variable.Initializer.Value);

            complexity = ComplexityComposer.Sequential(complexity, initializerComplexity);
            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
    }

    private ComplexityExpression AnalyzeExpressionStatement(ExpressionStatementSyntax expressionStatement)
    {
        return AnalyzeExpression(expressionStatement.Expression);
    }

    private ComplexityExpression AnalyzeReturn(ReturnStatementSyntax returnStatement)
    {
        return returnStatement.Expression is null
            ? ComplexityFactory.Constant()
            : AnalyzeExpression(returnStatement.Expression);
    }

    private ComplexityExpression AnalyzeBinaryExpression(BinaryExpressionSyntax binary)
    {
        if (!IsPrimitiveArithmetic(binary) && !IsPrimitiveComparison(binary))
        {
            return ComplexityFactory.Unknown();
        }

        ComplexityExpression left = AnalyzeExpression(binary.Left);
        ComplexityExpression right = AnalyzeExpression(binary.Right);

        return ComplexityComposer.Sequential(left, right);
    }

    private ComplexityExpression AnalyzeAssignment(AssignmentExpressionSyntax assignment)
    {
        if (!IsSimpleAssignmentTarget(assignment.Left))
        {
            return ComplexityFactory.Unknown();
        }

        if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return AnalyzeExpression(assignment.Right);
        }

        return IsPrimitiveCompoundAssignment(assignment)
            ? AnalyzeExpression(assignment.Right)
            : ComplexityFactory.Unknown();
    }

    private ComplexityExpression AnalyzePrefixUnary(PrefixUnaryExpressionSyntax prefixUnary)
    {
        SyntaxKind kind = prefixUnary.Kind();
        bool isIncrementOrDecrement = kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression;
        bool isPrimitiveUnaryArithmetic = (kind is SyntaxKind.UnaryPlusExpression or SyntaxKind.UnaryMinusExpression)
            && IsPrimitiveNumericType(GetExpressionType(prefixUnary.Operand));
        bool isPrimitiveLogicalNot = kind == SyntaxKind.LogicalNotExpression
            && IsBooleanType(GetExpressionType(prefixUnary.Operand));

        return isIncrementOrDecrement
            ? IsSimpleAssignmentTarget(prefixUnary.Operand)
                ? ComplexityFactory.Constant()
                : ComplexityFactory.Unknown()
            : isPrimitiveUnaryArithmetic || isPrimitiveLogicalNot
            ? AnalyzeExpression(prefixUnary.Operand)
            : ComplexityFactory.Unknown();
    }

    private ComplexityExpression AnalyzePostfixUnary(PostfixUnaryExpressionSyntax postfixUnary)
    {
        return (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression)
                || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression))
            && IsSimpleAssignmentTarget(postfixUnary.Operand)
            ? ComplexityFactory.Constant()
            : ComplexityFactory.Unknown();
    }

    private ComplexityExpression AnalyzeMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        if (!StringComparer.Ordinal.Equals(memberAccess.Name.Identifier.ValueText, "Length"))
        {
            return ComplexityFactory.Unknown();
        }

        if (!IsSimpleValueAccess(memberAccess.Expression))
        {
            return ComplexityFactory.Unknown();
        }

        ITypeSymbol? receiverType = GetExpressionType(memberAccess.Expression);
        return IsArrayType(receiverType) || IsStringType(receiverType)
            ? ComplexityFactory.Constant()
            : ComplexityFactory.Unknown();
    }

    private ComplexityExpression AnalyzeElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        if (!IsSimpleValueAccess(elementAccess.Expression)
            || !IsArrayType(GetExpressionType(elementAccess.Expression)))
        {
            return ComplexityFactory.Unknown();
        }

        ComplexityExpression complexity = ComplexityFactory.Constant();
        foreach (ArgumentSyntax argument in elementAccess.ArgumentList.Arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            complexity = ComplexityComposer.Sequential(complexity, AnalyzeExpression(argument.Expression));
            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
    }

    private bool IsPrimitiveArithmetic(BinaryExpressionSyntax binary)
    {
        return (binary.IsKind(SyntaxKind.AddExpression)
                || binary.IsKind(SyntaxKind.SubtractExpression)
                || binary.IsKind(SyntaxKind.MultiplyExpression)
                || binary.IsKind(SyntaxKind.DivideExpression)
                || binary.IsKind(SyntaxKind.ModuloExpression))
            && IsPrimitiveNumericType(GetExpressionType(binary.Left))
            && IsPrimitiveNumericType(GetExpressionType(binary.Right))
            && IsPrimitiveNumericType(GetExpressionType(binary));
    }

    private bool IsPrimitiveComparison(BinaryExpressionSyntax binary)
    {
        ITypeSymbol? leftType = GetExpressionType(binary.Left);
        ITypeSymbol? rightType = GetExpressionType(binary.Right);

        return (binary.IsKind(SyntaxKind.LessThanExpression)
                || binary.IsKind(SyntaxKind.LessThanOrEqualExpression)
                || binary.IsKind(SyntaxKind.GreaterThanExpression)
                || binary.IsKind(SyntaxKind.GreaterThanOrEqualExpression)
                || binary.IsKind(SyntaxKind.EqualsExpression)
                || binary.IsKind(SyntaxKind.NotEqualsExpression))
            && IsPrimitiveComparableType(leftType)
            && IsPrimitiveComparableType(rightType)
            && IsBooleanType(GetExpressionType(binary));
    }

    private bool IsPrimitiveCompoundAssignment(AssignmentExpressionSyntax assignment)
    {
        ITypeSymbol? leftType = GetExpressionType(assignment.Left);
        ITypeSymbol? rightType = GetExpressionType(assignment.Right);

        return (assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression)
            || assignment.IsKind(SyntaxKind.MultiplyAssignmentExpression)
            || assignment.IsKind(SyntaxKind.DivideAssignmentExpression)
            || assignment.IsKind(SyntaxKind.ModuloAssignmentExpression))
            && IsPrimitiveNumericType(leftType)
            && IsPrimitiveNumericType(rightType);
    }

    private bool IsSimpleAssignmentTarget(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        return expression switch
        {
            IdentifierNameSyntax identifier => IsSimpleValueAccess(identifier),
            ElementAccessExpressionSyntax elementAccess => AnalyzeElementAccess(elementAccess) is not UnknownComplexity,
            ParenthesizedExpressionSyntax parenthesized => IsSimpleAssignmentTarget(parenthesized.Expression),
            _ => false,
        };
    }

    private bool IsSimpleValueAccess(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            return IsSimpleValueAccess(parenthesized.Expression);
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken);
        ISymbol? symbol = symbolInfo.Symbol;

        return symbol is not null
            && (symbol.Kind == SymbolKind.Local || symbol.Kind == SymbolKind.Parameter);
    }

    private ITypeSymbol? GetExpressionType(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
        return typeInfo.Type;
    }

    private static bool IsArrayType(ITypeSymbol? type)
    {
        return type?.TypeKind == TypeKind.Array;
    }

    private static bool IsStringType(ITypeSymbol? type)
    {
        return type?.SpecialType == SpecialType.System_String;
    }

    private static bool IsBooleanType(ITypeSymbol? type)
    {
        return type?.SpecialType == SpecialType.System_Boolean;
    }

    private static bool IsPrimitiveComparableType(ITypeSymbol? type)
    {
        return IsBooleanType(type)
            || IsPrimitiveNumericType(type)
            || type?.SpecialType == SpecialType.System_Char
            || type?.TypeKind == TypeKind.Enum;
    }

    private static bool IsPrimitiveNumericType(ITypeSymbol? type)
    {
        return type?.SpecialType is SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;
    }
}

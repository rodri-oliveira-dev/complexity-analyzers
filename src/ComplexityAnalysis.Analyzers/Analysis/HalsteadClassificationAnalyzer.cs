using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

#pragma warning disable IDE0010, IDE0072 // The classifier deliberately maps only supported SyntaxKind subsets.
#pragma warning disable IDE0046 // Explicit branches keep literal type canonicalization easy to scan.
#pragma warning disable S1479 // The wide switch is the explicit SyntaxKind-to-Halstead classification table.

internal static class HalsteadClassificationAnalyzer
{
    internal static bool TryAnalyze(
        ExecutableMember member,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out HalsteadClassificationResult result)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (!member.Body.HasBody)
        {
            result = HalsteadClassificationResult.Empty;
            return false;
        }

        List<HalsteadElement> elements = [];
        AddExecutableRootHeader(member, semanticModel, elements, cancellationToken);

        foreach (SyntaxNode node in ExecutableMemberSyntax.DescendantNodesInOwnBody<SyntaxNode>(member))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClassifyNode(node, semanticModel, elements, cancellationToken);
        }

        result = new HalsteadClassificationResult(elements);
        return true;
    }

    private static void AddExecutableRootHeader(
        ExecutableMember member,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        if (member.Body.Expression is not null)
        {
            AddOperator(elements, HalsteadOperatorKind.LambdaOrExpressionBody);
        }

        if (member.Declaration is LambdaExpressionSyntax lambda)
        {
            AddLambdaParameters(lambda, semanticModel, elements, cancellationToken);
        }
    }

    private static void ClassifyNode(
        SyntaxNode node,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        switch (node)
        {
            case BinaryExpressionSyntax binary:
                ClassifyBinaryExpression(binary, elements);
                break;
            case AssignmentExpressionSyntax assignment:
                ClassifyAssignmentExpression(assignment, elements);
                break;
            case PrefixUnaryExpressionSyntax prefixUnary:
                ClassifyPrefixUnaryExpression(prefixUnary, elements);
                break;
            case PostfixUnaryExpressionSyntax postfixUnary:
                ClassifyPostfixUnaryExpression(postfixUnary, elements);
                break;
            case ConditionalExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.Conditional);
                break;
            case ConditionalAccessExpressionSyntax conditionalAccess:
                ClassifyConditionalAccessExpression(conditionalAccess, elements);
                break;
            case LambdaExpressionSyntax lambda:
                AddOperator(elements, HalsteadOperatorKind.LambdaOrExpressionBody);
                AddLambdaParameters(lambda, semanticModel, elements, cancellationToken);
                break;
            case LocalFunctionStatementSyntax localFunction:
                ClassifyLocalFunctionHeader(localFunction, semanticModel, elements, cancellationToken);
                break;
            case InvocationExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.Invocation);
                break;
            case ElementAccessExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.ElementAccess);
                break;
            case MemberAccessExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.MemberAccess);
                break;
            case MemberBindingExpressionSyntax memberBinding:
                AddSymbolOrIdentifierOperand(memberBinding.Name, semanticModel, elements, cancellationToken);
                break;
            case GenericNameSyntax genericName when !IsSimpleNameInTypeSyntax(genericName):
                ClassifyGenericName(genericName, semanticModel, elements, cancellationToken);
                break;
            case ObjectCreationExpressionSyntax objectCreation:
                AddOperator(elements, HalsteadOperatorKind.ObjectCreation);
                AddTypeOperand(objectCreation.Type, semanticModel, elements, cancellationToken);
                break;
            case ImplicitObjectCreationExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.ImplicitObjectCreation);
                break;
            case ArrayCreationExpressionSyntax arrayCreation:
                AddOperator(elements, HalsteadOperatorKind.ArrayCreation);
                AddTypeOperand(arrayCreation.Type.ElementType, semanticModel, elements, cancellationToken);
                break;
            case ImplicitArrayCreationExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.ArrayCreation);
                break;
            case StackAllocArrayCreationExpressionSyntax stackAllocArrayCreation:
                ClassifyStackAllocArrayCreation(stackAllocArrayCreation, semanticModel, elements, cancellationToken);
                break;
            case ImplicitStackAllocArrayCreationExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.ArrayCreation);
                break;
            case EqualsValueClauseSyntax equalsValue when equalsValue.Parent is VariableDeclaratorSyntax:
                AddOperator(elements, HalsteadOperatorKind.SimpleAssignment);
                break;
            case RangeExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.Range);
                break;
            case IsPatternExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.Is);
                break;
            case UnaryPatternSyntax unaryPattern when unaryPattern.IsKind(SyntaxKind.NotPattern):
                AddOperator(elements, HalsteadOperatorKind.PatternNot);
                break;
            case BinaryPatternSyntax binaryPattern:
                ClassifyBinaryPattern(binaryPattern, elements);
                break;
            case RelationalPatternSyntax relationalPattern:
                ClassifyRelationalPattern(relationalPattern, elements);
                break;
            case AwaitExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.Await);
                break;
            case YieldStatementSyntax yieldStatement:
                AddOperator(
                    elements,
                    yieldStatement.IsKind(SyntaxKind.YieldBreakStatement)
                        ? HalsteadOperatorKind.YieldBreak
                        : HalsteadOperatorKind.YieldReturn);
                break;
            case ThrowStatementSyntax:
            case ThrowExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.Throw);
                break;
            case ReturnStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Return);
                break;
            case IfStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.If);
                break;
            case ElseClauseSyntax:
                AddOperator(elements, HalsteadOperatorKind.Else);
                break;
            case ForStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.For);
                break;
            case ForEachStatementSyntax forEachStatement:
                AddOperator(elements, HalsteadOperatorKind.Foreach);
                AddDeclaredSymbolOperand(forEachStatement, semanticModel, elements, cancellationToken);
                break;
            case ForEachVariableStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Foreach);
                break;
            case WhileStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.While);
                break;
            case DoStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Do);
                break;
            case SwitchStatementSyntax:
            case SwitchExpressionSyntax:
                AddOperator(elements, HalsteadOperatorKind.Switch);
                break;
            case SwitchExpressionArmSyntax:
                AddOperator(elements, HalsteadOperatorKind.SwitchArm);
                break;
            case CaseSwitchLabelSyntax:
                AddOperator(elements, HalsteadOperatorKind.Case);
                break;
            case DefaultSwitchLabelSyntax:
                AddOperator(elements, HalsteadOperatorKind.Default);
                break;
            case WhenClauseSyntax:
                AddOperator(elements, HalsteadOperatorKind.When);
                break;
            case TryStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Try);
                break;
            case CatchClauseSyntax:
                AddOperator(elements, HalsteadOperatorKind.Catch);
                break;
            case CatchDeclarationSyntax catchDeclaration:
                ClassifyCatchDeclaration(catchDeclaration, semanticModel, elements, cancellationToken);
                break;
            case FinallyClauseSyntax:
                AddOperator(elements, HalsteadOperatorKind.Finally);
                break;
            case BreakStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Break);
                break;
            case ContinueStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Continue);
                break;
            case GotoStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Goto);
                break;
            case UsingStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Using);
                break;
            case LocalDeclarationStatementSyntax localDeclaration when localDeclaration.UsingKeyword.RawKind != 0:
                AddOperator(elements, HalsteadOperatorKind.Using);
                break;
            case LockStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Lock);
                break;
            case FixedStatementSyntax:
                AddOperator(elements, HalsteadOperatorKind.Fixed);
                break;
            case CheckedStatementSyntax checkedStatement:
                ClassifyCheckedOrUnchecked(checkedStatement, elements);
                break;
            case CheckedExpressionSyntax checkedExpression:
                ClassifyCheckedOrUnchecked(checkedExpression, elements);
                break;
            case VariableDeclarationSyntax variableDeclaration:
                ClassifyVariableDeclaration(variableDeclaration, semanticModel, elements, cancellationToken);
                break;
            case VariableDeclaratorSyntax variableDeclarator:
                AddDeclaredSymbolOperand(variableDeclarator, semanticModel, elements, cancellationToken);
                break;
            case DeclarationPatternSyntax declarationPattern:
                AddTypeOperand(declarationPattern.Type, semanticModel, elements, cancellationToken);
                break;
            case TypePatternSyntax typePattern:
                AddTypeOperand(typePattern.Type, semanticModel, elements, cancellationToken);
                break;
            case SingleVariableDesignationSyntax designation:
                AddDesignationOperand(designation, semanticModel, elements, cancellationToken);
                break;
            case DiscardDesignationSyntax:
            case DiscardPatternSyntax:
                AddOperand(elements, HalsteadOperandKind.Discard, "discard:_");
                break;
            case LiteralExpressionSyntax literal:
                ClassifyLiteral(literal, semanticModel, elements, cancellationToken);
                break;
            case InterpolatedStringExpressionSyntax interpolatedString:
                ClassifyInterpolatedString(interpolatedString, elements);
                break;
            case IdentifierNameSyntax identifierName when !IsSimpleNameInTypeSyntax(identifierName):
                AddSymbolOrIdentifierOperand(identifierName, semanticModel, elements, cancellationToken);
                break;
            case NameColonSyntax nameColon:
                AddOperand(elements, HalsteadOperandKind.Property, "property:" + nameColon.Name.Identifier.ValueText);
                break;
            case TypeOfExpressionSyntax typeOfExpression:
                AddTypeOperand(typeOfExpression.Type, semanticModel, elements, cancellationToken);
                break;
            case SizeOfExpressionSyntax sizeOfExpression:
                AddTypeOperand(sizeOfExpression.Type, semanticModel, elements, cancellationToken);
                break;
            case DefaultExpressionSyntax defaultExpression:
                AddTypeOperand(defaultExpression.Type, semanticModel, elements, cancellationToken);
                break;
            case CastExpressionSyntax castExpression:
                AddTypeOperand(castExpression.Type, semanticModel, elements, cancellationToken);
                break;
            default:
                break;
        }

        ClassifyBySyntaxKind(node, elements);
    }

    private static void ClassifyBySyntaxKind(
        SyntaxNode node,
        List<HalsteadElement> elements)
    {
        switch (node.Kind())
        {
            case SyntaxKind.CollectionExpression:
                AddOperator(elements, HalsteadOperatorKind.CollectionExpression);
                break;
            case SyntaxKind.SpreadElement:
                AddOperator(elements, HalsteadOperatorKind.CollectionSpread);
                break;
            default:
                break;
        }
    }

    private static void ClassifyGenericName(
        GenericNameSyntax genericName,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        AddSymbolOrIdentifierOperand(genericName, semanticModel, elements, cancellationToken);
        foreach (TypeSyntax typeArgument in genericName.TypeArgumentList.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddTypeOperand(typeArgument, semanticModel, elements, cancellationToken);
        }
    }

    private static void ClassifyBinaryExpression(
        BinaryExpressionSyntax binary,
        List<HalsteadElement> elements)
    {
        switch (binary.Kind())
        {
            case SyntaxKind.AddExpression:
                AddOperator(elements, HalsteadOperatorKind.Add);
                break;
            case SyntaxKind.SubtractExpression:
                AddOperator(elements, HalsteadOperatorKind.Subtract);
                break;
            case SyntaxKind.MultiplyExpression:
                AddOperator(elements, HalsteadOperatorKind.Multiply);
                break;
            case SyntaxKind.DivideExpression:
                AddOperator(elements, HalsteadOperatorKind.Divide);
                break;
            case SyntaxKind.ModuloExpression:
                AddOperator(elements, HalsteadOperatorKind.Modulo);
                break;
            case SyntaxKind.EqualsExpression:
                AddOperator(elements, HalsteadOperatorKind.Equal);
                break;
            case SyntaxKind.NotEqualsExpression:
                AddOperator(elements, HalsteadOperatorKind.NotEqual);
                break;
            case SyntaxKind.LessThanExpression:
                AddOperator(elements, HalsteadOperatorKind.LessThan);
                break;
            case SyntaxKind.LessThanOrEqualExpression:
                AddOperator(elements, HalsteadOperatorKind.LessThanOrEqual);
                break;
            case SyntaxKind.GreaterThanExpression:
                AddOperator(elements, HalsteadOperatorKind.GreaterThan);
                break;
            case SyntaxKind.GreaterThanOrEqualExpression:
                AddOperator(elements, HalsteadOperatorKind.GreaterThanOrEqual);
                break;
            case SyntaxKind.LogicalAndExpression:
                AddOperator(elements, HalsteadOperatorKind.LogicalAnd);
                break;
            case SyntaxKind.LogicalOrExpression:
                AddOperator(elements, HalsteadOperatorKind.LogicalOr);
                break;
            case SyntaxKind.BitwiseAndExpression:
                AddOperator(elements, HalsteadOperatorKind.BitwiseAnd);
                break;
            case SyntaxKind.BitwiseOrExpression:
                AddOperator(elements, HalsteadOperatorKind.BitwiseOr);
                break;
            case SyntaxKind.ExclusiveOrExpression:
                AddOperator(elements, HalsteadOperatorKind.ExclusiveOr);
                break;
            case SyntaxKind.LeftShiftExpression:
                AddOperator(elements, HalsteadOperatorKind.LeftShift);
                break;
            case SyntaxKind.RightShiftExpression:
                AddOperator(elements, HalsteadOperatorKind.RightShift);
                break;
            case SyntaxKind.UnsignedRightShiftExpression:
                AddOperator(elements, HalsteadOperatorKind.UnsignedRightShift);
                break;
            case SyntaxKind.CoalesceExpression:
                AddOperator(elements, HalsteadOperatorKind.NullCoalescing);
                break;
            case SyntaxKind.IsExpression:
                AddOperator(elements, HalsteadOperatorKind.Is);
                break;
            default:
                break;
        }
    }

    private static void ClassifyAssignmentExpression(
        AssignmentExpressionSyntax assignment,
        List<HalsteadElement> elements)
    {
        switch (assignment.Kind())
        {
            case SyntaxKind.SimpleAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.SimpleAssignment);
                break;
            case SyntaxKind.AddAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.AddAssignment);
                break;
            case SyntaxKind.SubtractAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.SubtractAssignment);
                break;
            case SyntaxKind.MultiplyAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.MultiplyAssignment);
                break;
            case SyntaxKind.DivideAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.DivideAssignment);
                break;
            case SyntaxKind.ModuloAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.ModuloAssignment);
                break;
            case SyntaxKind.AndAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.AndAssignment);
                break;
            case SyntaxKind.OrAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.OrAssignment);
                break;
            case SyntaxKind.ExclusiveOrAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.ExclusiveOrAssignment);
                break;
            case SyntaxKind.LeftShiftAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.LeftShiftAssignment);
                break;
            case SyntaxKind.RightShiftAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.RightShiftAssignment);
                break;
            case SyntaxKind.UnsignedRightShiftAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.UnsignedRightShiftAssignment);
                break;
            case SyntaxKind.CoalesceAssignmentExpression:
                AddOperator(elements, HalsteadOperatorKind.NullCoalescingAssignment);
                break;
            default:
                break;
        }
    }

    private static void ClassifyPrefixUnaryExpression(
        PrefixUnaryExpressionSyntax prefixUnary,
        List<HalsteadElement> elements)
    {
        switch (prefixUnary.Kind())
        {
            case SyntaxKind.UnaryPlusExpression:
                AddOperator(elements, HalsteadOperatorKind.UnaryPlus);
                break;
            case SyntaxKind.UnaryMinusExpression:
                AddOperator(elements, HalsteadOperatorKind.UnaryMinus);
                break;
            case SyntaxKind.LogicalNotExpression:
                AddOperator(elements, HalsteadOperatorKind.LogicalNot);
                break;
            case SyntaxKind.BitwiseNotExpression:
                AddOperator(elements, HalsteadOperatorKind.BitwiseNot);
                break;
            case SyntaxKind.PreIncrementExpression:
                AddOperator(elements, HalsteadOperatorKind.PreIncrement);
                break;
            case SyntaxKind.PreDecrementExpression:
                AddOperator(elements, HalsteadOperatorKind.PreDecrement);
                break;
            case SyntaxKind.IndexExpression:
                AddOperator(elements, HalsteadOperatorKind.Index);
                break;
            default:
                break;
        }
    }

    private static void ClassifyPostfixUnaryExpression(
        PostfixUnaryExpressionSyntax postfixUnary,
        List<HalsteadElement> elements)
    {
        switch (postfixUnary.Kind())
        {
            case SyntaxKind.PostIncrementExpression:
                AddOperator(elements, HalsteadOperatorKind.PostIncrement);
                break;
            case SyntaxKind.PostDecrementExpression:
                AddOperator(elements, HalsteadOperatorKind.PostDecrement);
                break;
            default:
                break;
        }
    }

    private static void ClassifyConditionalAccessExpression(
        ConditionalAccessExpressionSyntax conditionalAccess,
        List<HalsteadElement> elements)
    {
        AddOperator(
            elements,
            conditionalAccess.WhenNotNull is ElementBindingExpressionSyntax
                ? HalsteadOperatorKind.ConditionalElementAccess
                : HalsteadOperatorKind.ConditionalAccess);
    }

    private static void ClassifyBinaryPattern(
        BinaryPatternSyntax binaryPattern,
        List<HalsteadElement> elements)
    {
        AddOperator(
            elements,
            binaryPattern.IsKind(SyntaxKind.AndPattern)
                ? HalsteadOperatorKind.PatternAnd
                : HalsteadOperatorKind.PatternOr);
    }

    private static void ClassifyRelationalPattern(
        RelationalPatternSyntax relationalPattern,
        List<HalsteadElement> elements)
    {
        switch (relationalPattern.OperatorToken.Kind())
        {
            case SyntaxKind.LessThanToken:
                AddOperator(elements, HalsteadOperatorKind.LessThan);
                break;
            case SyntaxKind.LessThanEqualsToken:
                AddOperator(elements, HalsteadOperatorKind.LessThanOrEqual);
                break;
            case SyntaxKind.GreaterThanToken:
                AddOperator(elements, HalsteadOperatorKind.GreaterThan);
                break;
            case SyntaxKind.GreaterThanEqualsToken:
                AddOperator(elements, HalsteadOperatorKind.GreaterThanOrEqual);
                break;
            default:
                break;
        }
    }

    private static void ClassifyCheckedOrUnchecked(
        SyntaxNode node,
        List<HalsteadElement> elements)
    {
        AddOperator(
            elements,
            node.IsKind(SyntaxKind.UncheckedStatement) || node.IsKind(SyntaxKind.UncheckedExpression)
                ? HalsteadOperatorKind.Unchecked
                : HalsteadOperatorKind.Checked);
    }

    private static void ClassifyVariableDeclaration(
        VariableDeclarationSyntax variableDeclaration,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        if (!variableDeclaration.Type.IsVar)
        {
            AddTypeOperand(variableDeclaration.Type, semanticModel, elements, cancellationToken);
        }
    }

    private static void ClassifyStackAllocArrayCreation(
        StackAllocArrayCreationExpressionSyntax stackAllocArrayCreation,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        AddOperator(elements, HalsteadOperatorKind.ArrayCreation);
        if (stackAllocArrayCreation.Type is ArrayTypeSyntax arrayType)
        {
            AddTypeOperand(arrayType.ElementType, semanticModel, elements, cancellationToken);
            return;
        }

        AddTypeOperand(stackAllocArrayCreation.Type, semanticModel, elements, cancellationToken);
    }

    private static void ClassifyCatchDeclaration(
        CatchDeclarationSyntax catchDeclaration,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        AddTypeOperand(catchDeclaration.Type, semanticModel, elements, cancellationToken);
        if (catchDeclaration.Identifier.RawKind != 0)
        {
            AddDeclaredSymbolOperand(catchDeclaration, semanticModel, elements, cancellationToken);
        }
    }

    private static void ClassifyLocalFunctionHeader(
        LocalFunctionStatementSyntax localFunction,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        if (localFunction.ExpressionBody is not null)
        {
            AddOperator(elements, HalsteadOperatorKind.LambdaOrExpressionBody);
        }

        AddDeclaredSymbolOperand(localFunction, semanticModel, elements, cancellationToken);
        foreach (ParameterSyntax parameter in localFunction.ParameterList.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddDeclaredSymbolOperand(parameter, semanticModel, elements, cancellationToken);
        }
    }

    private static void AddLambdaParameters(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        if (lambda is SimpleLambdaExpressionSyntax simpleLambda)
        {
            AddDeclaredSymbolOperand(simpleLambda.Parameter, semanticModel, elements, cancellationToken);
            return;
        }

        if (lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
        {
            foreach (ParameterSyntax parameter in parenthesizedLambda.ParameterList.Parameters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddDeclaredSymbolOperand(parameter, semanticModel, elements, cancellationToken);
            }
        }
    }

    private static void ClassifyLiteral(
        LiteralExpressionSyntax literal,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        switch (literal.Kind())
        {
            case SyntaxKind.NumericLiteralExpression:
                AddOperand(elements, HalsteadOperandKind.NumericLiteral, GetNumericLiteralIdentity(literal, semanticModel, cancellationToken));
                break;
            case SyntaxKind.StringLiteralExpression:
            case SyntaxKind.Utf8StringLiteralExpression:
                AddOperand(elements, HalsteadOperandKind.StringLiteral, "string:" + literal.Token.ValueText);
                break;
            case SyntaxKind.CharacterLiteralExpression:
                AddOperand(elements, HalsteadOperandKind.CharacterLiteral, "char:" + literal.Token.ValueText);
                break;
            case SyntaxKind.TrueLiteralExpression:
                AddOperand(elements, HalsteadOperandKind.BooleanLiteral, "bool:true");
                break;
            case SyntaxKind.FalseLiteralExpression:
                AddOperand(elements, HalsteadOperandKind.BooleanLiteral, "bool:false");
                break;
            case SyntaxKind.NullLiteralExpression:
                AddOperand(elements, HalsteadOperandKind.NullLiteral, "null");
                break;
            default:
                break;
        }
    }

    private static string GetNumericLiteralIdentity(
        LiteralExpressionSyntax literal,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        Optional<object?> constant = semanticModel.GetConstantValue(literal, cancellationToken);
        TypeInfo typeInfo = semanticModel.GetTypeInfo(literal, cancellationToken);
        string typeName = FormatLiteralType(typeInfo.ConvertedType ?? typeInfo.Type);
        return constant.HasValue && constant.Value is not null
            ? typeName + ":" + Convert.ToString(constant.Value, CultureInfo.InvariantCulture)
            : typeName + ":" + literal.Token.ValueText;
    }

    private static string FormatLiteralType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null)
        {
            return "numeric";
        }

        SpecialType specialType = typeSymbol.SpecialType;
        if (specialType == SpecialType.System_Byte)
        {
            return "byte";
        }

        if (specialType == SpecialType.System_SByte)
        {
            return "sbyte";
        }

        if (specialType == SpecialType.System_Int16)
        {
            return "short";
        }

        if (specialType == SpecialType.System_UInt16)
        {
            return "ushort";
        }

        if (specialType == SpecialType.System_Int32)
        {
            return "int";
        }

        if (specialType == SpecialType.System_UInt32)
        {
            return "uint";
        }

        if (specialType == SpecialType.System_Int64)
        {
            return "long";
        }

        if (specialType == SpecialType.System_UInt64)
        {
            return "ulong";
        }

        if (specialType == SpecialType.System_Single)
        {
            return "float";
        }

        if (specialType == SpecialType.System_Double)
        {
            return "double";
        }

        return specialType == SpecialType.System_Decimal
            ? "decimal"
            : "numeric";
    }

    private static void ClassifyInterpolatedString(
        InterpolatedStringExpressionSyntax interpolatedString,
        List<HalsteadElement> elements)
    {
        StringBuilder literalText = new();
        foreach (InterpolatedStringContentSyntax content in interpolatedString.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
            {
                _ = literalText.Append(text.TextToken.ValueText);
            }
        }

        AddOperand(elements, HalsteadOperandKind.StringLiteral, "string:" + literalText.ToString());
    }

    private static void AddTypeOperand(
        TypeSyntax type,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));

        cancellationToken.ThrowIfCancellationRequested();

        ITypeSymbol? typeSymbol = semanticModel.GetTypeInfo(type, cancellationToken).Type;
        string canonicalValue = typeSymbol is not null
            ? "type:" + typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : "type:" + type.ToString();
        AddOperand(elements, HalsteadOperandKind.TypeName, canonicalValue);
    }

    private static void AddDeclaredSymbolOperand(
        SyntaxNode declaration,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ISymbol? symbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
        if (symbol is not null)
        {
            AddSymbolOperand(symbol, elements);
        }
    }

    private static void AddDesignationOperand(
        SingleVariableDesignationSyntax designation,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ISymbol? symbol = semanticModel.GetDeclaredSymbol(designation, cancellationToken);
        if (symbol is not null)
        {
            AddOperand(elements, HalsteadOperandKind.PatternVariable, "pattern:" + symbol.Name);
            return;
        }

        AddOperand(elements, HalsteadOperandKind.PatternVariable, "pattern:" + designation.Identifier.ValueText);
    }

    private static void AddSymbolOrIdentifierOperand(
        SimpleNameSyntax name,
        SemanticModel semanticModel,
        List<HalsteadElement> elements,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ISymbol? symbol = semanticModel.GetSymbolInfo(name, cancellationToken).Symbol;
        if (symbol is not null)
        {
            AddSymbolOperand(symbol, elements);
            return;
        }

        AddOperand(elements, HalsteadOperandKind.Identifier, "identifier:" + name.Identifier.ValueText);
    }

    private static void AddSymbolOperand(
        ISymbol symbol,
        List<HalsteadElement> elements)
    {
        switch (symbol)
        {
            case IParameterSymbol:
                AddOperand(elements, HalsteadOperandKind.Parameter, "parameter:" + symbol.Name);
                break;
            case ILocalSymbol local:
                AddOperand(
                    elements,
                    local.IsConst ? HalsteadOperandKind.Constant : HalsteadOperandKind.Local,
                    (local.IsConst ? "constant:" : "local:") + local.Name);
                break;
            case IFieldSymbol field:
                AddOperand(
                    elements,
                    field.HasConstantValue ? HalsteadOperandKind.Constant : HalsteadOperandKind.Field,
                    (field.HasConstantValue ? "constant:" : "field:") + field.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                break;
            case IPropertySymbol property:
                AddOperand(elements, HalsteadOperandKind.Property, "property:" + property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                break;
            case IMethodSymbol method:
                AddOperand(elements, HalsteadOperandKind.Method, "method:" + method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                break;
            case IEventSymbol eventSymbol:
                AddOperand(elements, HalsteadOperandKind.Event, "event:" + eventSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                break;
            case ITypeSymbol typeSymbol:
                AddOperand(elements, HalsteadOperandKind.TypeName, "type:" + typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                break;
            default:
                AddOperand(elements, HalsteadOperandKind.Identifier, "identifier:" + symbol.Name);
                break;
        }
    }

    private static bool IsSimpleNameInTypeSyntax(SimpleNameSyntax simpleName)
    {
        for (SyntaxNode? current = simpleName.Parent; current is not null; current = current.Parent)
        {
            if (current is TypeSyntax)
            {
                return true;
            }

            if (current is ExpressionSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static void AddOperator(
        List<HalsteadElement> elements,
        HalsteadOperatorKind kind)
    {
        elements.Add(HalsteadElement.Operator(kind));
    }

    private static void AddOperand(
        List<HalsteadElement> elements,
        HalsteadOperandKind kind,
        string canonicalValue)
    {
        elements.Add(HalsteadElement.Operand(kind, canonicalValue));
    }
}

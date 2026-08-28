using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class CognitiveComplexityCalculator
{
    private int score;
    private bool directSelfRecursionCounted;
    private ExecutableMember? member;
    private SemanticModel? semanticModel;
    private CancellationToken cancellationToken;

    internal bool TryCalculate(
        ExecutableMember member,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CognitiveComplexity complexity)
    {
        this.member = member ?? throw new ArgumentNullException(nameof(member));
        this.semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (!member.Body.HasBody)
        {
            complexity = default;
            return false;
        }

        score = 0;
        directSelfRecursionCounted = false;
        this.cancellationToken = cancellationToken;

        SyntaxNode? bodyRoot = member.Body.Block ?? (SyntaxNode?)member.Body.Expression;
        AnalyzeNode(bodyRoot, currentNesting: 0);

        complexity = new CognitiveComplexity(score);
        return true;
    }

    private void AnalyzeNode(
        SyntaxNode? node,
        int currentNesting)
    {
        if (node is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (IsNestedExecutableBoundary(node))
        {
            return;
        }

        switch (node)
        {
            case IfStatementSyntax ifStatement:
                AnalyzeIfStatement(ifStatement, currentNesting);
                break;
            case ForStatementSyntax forStatement:
                AnalyzeForStatement(forStatement, currentNesting);
                break;
            case ForEachStatementSyntax forEachStatement:
                AnalyzeForeachStatement(forEachStatement.Expression, forEachStatement.Statement, currentNesting);
                break;
            case ForEachVariableStatementSyntax forEachVariableStatement:
                AnalyzeForeachStatement(forEachVariableStatement.Expression, forEachVariableStatement.Statement, currentNesting);
                break;
            case WhileStatementSyntax whileStatement:
                AddStructuralIncrement(currentNesting);
                AnalyzeExpression(whileStatement.Condition, currentNesting);
                AnalyzeNode(whileStatement.Statement, EnterControlFlow(currentNesting));
                break;
            case DoStatementSyntax doStatement:
                AddStructuralIncrement(currentNesting);
                AnalyzeNode(doStatement.Statement, EnterControlFlow(currentNesting));
                AnalyzeExpression(doStatement.Condition, currentNesting);
                break;
            case SwitchStatementSyntax switchStatement:
                AnalyzeSwitchStatement(switchStatement, currentNesting);
                break;
            case SwitchExpressionSyntax switchExpression:
                AnalyzeSwitchExpression(switchExpression, currentNesting);
                break;
            case TryStatementSyntax tryStatement:
                AnalyzeTryStatement(tryStatement, currentNesting);
                break;
            case ConditionalExpressionSyntax conditionalExpression:
                AnalyzeConditionalExpression(conditionalExpression, currentNesting);
                break;
            case BinaryExpressionSyntax binaryExpression
                when IsLogicalBinaryExpression(binaryExpression):
                AnalyzeLogicalBinaryExpression(binaryExpression, currentNesting);
                break;
            case IsPatternExpressionSyntax patternExpression:
                AnalyzeExpression(patternExpression.Expression, currentNesting);
                AnalyzePattern(patternExpression.Pattern, currentNesting);
                break;
            case InvocationExpressionSyntax invocationExpression:
                TryCountDirectSelfRecursion(invocationExpression);
                AnalyzeChildren(invocationExpression, currentNesting);
                break;
            case BinaryExpressionSyntax binaryExpression:
                TryCountDirectSelfRecursion(binaryExpression);
                AnalyzeChildren(binaryExpression, currentNesting);
                break;
            case AssignmentExpressionSyntax assignmentExpression:
                TryCountDirectSelfRecursion(assignmentExpression);
                AnalyzeChildren(assignmentExpression, currentNesting);
                break;
            case PrefixUnaryExpressionSyntax prefixUnaryExpression:
                TryCountDirectSelfRecursion(prefixUnaryExpression);
                AnalyzeChildren(prefixUnaryExpression, currentNesting);
                break;
            case PostfixUnaryExpressionSyntax postfixUnaryExpression:
                TryCountDirectSelfRecursion(postfixUnaryExpression);
                AnalyzeChildren(postfixUnaryExpression, currentNesting);
                break;
            case CastExpressionSyntax castExpression:
                TryCountDirectSelfRecursion(castExpression);
                AnalyzeChildren(castExpression, currentNesting);
                break;
            case IdentifierNameSyntax identifierName:
                TryCountDirectSelfRecursion(identifierName);
                AnalyzeChildren(identifierName, currentNesting);
                break;
            case MemberAccessExpressionSyntax memberAccess:
                TryCountDirectSelfRecursion(memberAccess);
                AnalyzeChildren(memberAccess, currentNesting);
                break;
            case ElementAccessExpressionSyntax elementAccess:
                TryCountDirectSelfRecursion(elementAccess);
                AnalyzeChildren(elementAccess, currentNesting);
                break;
            case BreakStatementSyntax
                or ContinueStatementSyntax
                or GotoStatementSyntax:
                AddStructuralIncrementWithoutNesting();
                AnalyzeChildren(node, currentNesting);
                break;
            default:
                AnalyzeChildren(node, currentNesting);
                break;
        }
    }

    private void AnalyzeIfStatement(
        IfStatementSyntax ifStatement,
        int currentNesting)
    {
        AddStructuralIncrement(currentNesting);
        AnalyzeExpression(ifStatement.Condition, currentNesting);

        int branchNesting = EnterControlFlow(currentNesting);
        AnalyzeNode(ifStatement.Statement, branchNesting);

        if (ifStatement.Else is null)
        {
            return;
        }

        if (ifStatement.Else.Statement is IfStatementSyntax elseIf)
        {
            AnalyzeIfStatement(elseIf, currentNesting);
            return;
        }

        AddStructuralIncrementWithoutNesting();
        AnalyzeNode(ifStatement.Else.Statement, branchNesting);
    }

    private void AnalyzeForStatement(
        ForStatementSyntax forStatement,
        int currentNesting)
    {
        AddStructuralIncrement(currentNesting);
        AnalyzeNode(forStatement.Declaration, currentNesting);

        foreach (ExpressionSyntax initializer in forStatement.Initializers)
        {
            AnalyzeExpression(initializer, currentNesting);
        }

        AnalyzeExpression(forStatement.Condition, currentNesting);

        foreach (ExpressionSyntax incrementor in forStatement.Incrementors)
        {
            AnalyzeExpression(incrementor, currentNesting);
        }

        AnalyzeNode(forStatement.Statement, EnterControlFlow(currentNesting));
    }

    private void AnalyzeForeachStatement(
        ExpressionSyntax expression,
        StatementSyntax statement,
        int currentNesting)
    {
        AddStructuralIncrement(currentNesting);
        AnalyzeExpression(expression, currentNesting);
        AnalyzeNode(statement, EnterControlFlow(currentNesting));
    }

    private void AnalyzeSwitchStatement(
        SwitchStatementSyntax switchStatement,
        int currentNesting)
    {
        AddStructuralIncrement(currentNesting);
        AnalyzeExpression(switchStatement.Expression, currentNesting);

        int branchNesting = EnterControlFlow(currentNesting);
        foreach (SwitchSectionSyntax section in switchStatement.Sections)
        {
            foreach (SwitchLabelSyntax label in section.Labels)
            {
                AnalyzeSwitchLabel(label, branchNesting);
            }

            foreach (StatementSyntax statement in section.Statements)
            {
                AnalyzeNode(statement, branchNesting);
            }
        }
    }

    private void AnalyzeSwitchLabel(
        SwitchLabelSyntax label,
        int currentNesting)
    {
        if (label is CaseSwitchLabelSyntax caseLabel)
        {
            AnalyzeExpression(caseLabel.Value, currentNesting);
        }
        else if (label is CasePatternSwitchLabelSyntax patternLabel)
        {
            AnalyzePattern(patternLabel.Pattern, currentNesting);
            AnalyzeWhenClause(patternLabel.WhenClause, currentNesting);
        }
    }

    private void AnalyzeSwitchExpression(
        SwitchExpressionSyntax switchExpression,
        int currentNesting)
    {
        AddStructuralIncrement(currentNesting);
        AnalyzeExpression(switchExpression.GoverningExpression, currentNesting);

        int armNesting = EnterControlFlow(currentNesting);
        foreach (SwitchExpressionArmSyntax arm in switchExpression.Arms)
        {
            AnalyzePattern(arm.Pattern, armNesting);
            AnalyzeWhenClause(arm.WhenClause, armNesting);
            AnalyzeExpression(arm.Expression, armNesting);
        }
    }

    private void AnalyzeTryStatement(
        TryStatementSyntax tryStatement,
        int currentNesting)
    {
        AnalyzeNode(tryStatement.Block, currentNesting);

        foreach (CatchClauseSyntax catchClause in tryStatement.Catches)
        {
            AddStructuralIncrement(currentNesting);
            AnalyzeCatchFilter(catchClause.Filter, currentNesting);
            AnalyzeNode(catchClause.Block, EnterControlFlow(currentNesting));
        }

        AnalyzeNode(tryStatement.Finally?.Block, currentNesting);
    }

    private void AnalyzeConditionalExpression(
        ConditionalExpressionSyntax conditionalExpression,
        int currentNesting)
    {
        AddStructuralIncrement(currentNesting);
        AnalyzeExpression(conditionalExpression.Condition, currentNesting);

        int branchNesting = EnterControlFlow(currentNesting);
        AnalyzeExpression(conditionalExpression.WhenTrue, branchNesting);
        AnalyzeExpression(conditionalExpression.WhenFalse, branchNesting);
    }

    private void AnalyzeWhenClause(
        WhenClauseSyntax? whenClause,
        int currentNesting)
    {
        if (whenClause is null)
        {
            return;
        }

        AddStructuralIncrement(currentNesting);
        AnalyzeExpression(whenClause.Condition, currentNesting);
    }

    private void AnalyzeCatchFilter(
        CatchFilterClauseSyntax? filter,
        int currentNesting)
    {
        if (filter is null)
        {
            return;
        }

        AddStructuralIncrement(currentNesting);
        AnalyzeExpression(filter.FilterExpression, currentNesting);
    }

    private void TryCountDirectSelfRecursion(ExpressionSyntax expression)
    {
        if (directSelfRecursionCounted
            || member is null
            || semanticModel is null
            || !member.SupportsDirectRecursion)
        {
            return;
        }

        ExecutableMember currentMember = member;
        SemanticModel currentSemanticModel = semanticModel;

        if (!ShouldInspectDirectSelfRecursion(expression, currentMember))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IMethodSymbol? referencedSymbol = GetReferencedMethodSymbol(
            expression,
            currentSemanticModel,
            currentMember);
        if (referencedSymbol is null)
        {
            return;
        }

        IMethodSymbol targetSymbol = referencedSymbol.ReducedFrom ?? referencedSymbol;
        if (!SymbolEqualityComparer.Default.Equals(
            targetSymbol.OriginalDefinition,
            currentMember.Symbol.OriginalDefinition))
        {
            return;
        }

        directSelfRecursionCounted = true;
        AddStructuralIncrementWithoutNesting();
    }

    private static bool ShouldInspectDirectSelfRecursion(
        ExpressionSyntax expression,
        ExecutableMember currentMember)
    {
        return expression switch
        {
            InvocationExpressionSyntax => true,
            BinaryExpressionSyntax
                or PrefixUnaryExpressionSyntax
                or PostfixUnaryExpressionSyntax
                or AssignmentExpressionSyntax => currentMember.Symbol.MethodKind == MethodKind.UserDefinedOperator,
            CastExpressionSyntax => currentMember.Symbol.MethodKind == MethodKind.Conversion,
            IdentifierNameSyntax
                or MemberAccessExpressionSyntax
                or ElementAccessExpressionSyntax => IsAccessorMethodKind(currentMember.Symbol.MethodKind),
            _ => false,
        };
    }

    private static bool IsAccessorMethodKind(MethodKind methodKind)
    {
        return methodKind is MethodKind.PropertyGet
            or MethodKind.PropertySet
            or MethodKind.EventAdd
            or MethodKind.EventRemove;
    }

    private IMethodSymbol? GetReferencedMethodSymbol(
        ExpressionSyntax expression,
        SemanticModel currentSemanticModel,
        ExecutableMember currentMember)
    {
        SymbolInfo symbolInfo = currentSemanticModel.GetSymbolInfo(expression, cancellationToken);
        return symbolInfo.Symbol switch
        {
            IMethodSymbol methodSymbol => methodSymbol,
            IPropertySymbol propertySymbol => GetReferencedPropertyAccessor(expression, propertySymbol, currentMember),
            IEventSymbol eventSymbol => GetReferencedEventAccessor(expression, eventSymbol, currentMember),
            _ => null,
        };
    }

    private static IMethodSymbol? GetReferencedPropertyAccessor(
        ExpressionSyntax expression,
        IPropertySymbol propertySymbol,
        ExecutableMember currentMember)
    {
        bool isSimpleWrite = IsLeftSideOfAssignment(expression, allowCompoundAssignment: false);
        bool isReadWrite = IsLeftSideOfAssignment(expression, allowCompoundAssignment: true)
            || IsOperandOfIncrementOrDecrement(expression);

        return isReadWrite
            && IsSameMethod(propertySymbol.GetMethod, currentMember.Symbol)
                ? propertySymbol.GetMethod
                : (isSimpleWrite || isReadWrite)
                    && IsSameMethod(propertySymbol.SetMethod, currentMember.Symbol)
                        ? propertySymbol.SetMethod
                        : !isSimpleWrite
                            && !isReadWrite
                            && IsSameMethod(propertySymbol.GetMethod, currentMember.Symbol)
                                ? propertySymbol.GetMethod
                                : null;
    }

    private static IMethodSymbol? GetReferencedEventAccessor(
        ExpressionSyntax expression,
        IEventSymbol eventSymbol,
        ExecutableMember currentMember)
    {
        return IsLeftSideOfAddAssignment(expression)
            && IsSameMethod(eventSymbol.AddMethod, currentMember.Symbol)
                ? eventSymbol.AddMethod
                : IsLeftSideOfSubtractAssignment(expression)
                    && IsSameMethod(eventSymbol.RemoveMethod, currentMember.Symbol)
                        ? eventSymbol.RemoveMethod
                        : null;
    }

    private static bool IsSameMethod(
        IMethodSymbol? left,
        IMethodSymbol right)
    {
        return left is not null
            && SymbolEqualityComparer.Default.Equals(
                (left.ReducedFrom ?? left).OriginalDefinition,
                (right.ReducedFrom ?? right).OriginalDefinition);
    }

    private static bool IsLeftSideOfAssignment(
        ExpressionSyntax expression,
        bool allowCompoundAssignment)
    {
        return expression.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == expression
            && (allowCompoundAssignment || assignment.IsKind(SyntaxKind.SimpleAssignmentExpression));
    }

    private static bool IsLeftSideOfAddAssignment(ExpressionSyntax expression)
    {
        return expression.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == expression
            && assignment.IsKind(SyntaxKind.AddAssignmentExpression);
    }

    private static bool IsLeftSideOfSubtractAssignment(ExpressionSyntax expression)
    {
        return expression.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == expression
            && assignment.IsKind(SyntaxKind.SubtractAssignmentExpression);
    }

    private static bool IsOperandOfIncrementOrDecrement(ExpressionSyntax expression)
    {
        return expression.Parent switch
        {
            PrefixUnaryExpressionSyntax prefixUnary
                when prefixUnary.Operand == expression
                    && (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression)
                        || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression)) => true,
            PostfixUnaryExpressionSyntax postfixUnary
                when postfixUnary.Operand == expression
                    && (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression)
                        || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression)) => true,
            _ => false,
        };
    }

    private void AnalyzeExpression(
        ExpressionSyntax? expression,
        int currentNesting)
    {
        AnalyzeNode(expression, currentNesting);
    }

    private void AnalyzePattern(
        PatternSyntax? pattern,
        int currentNesting)
    {
        if (pattern is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (pattern is ParenthesizedPatternSyntax parenthesizedPattern)
        {
            AnalyzePattern(parenthesizedPattern.Pattern, currentNesting);
            return;
        }

        if (pattern is BinaryPatternSyntax binaryPattern && IsLogicalPattern(binaryPattern))
        {
            AnalyzeLogicalPattern(binaryPattern, currentNesting);
            return;
        }

        foreach (SyntaxNode child in pattern.ChildNodes())
        {
            if (child is PatternSyntax childPattern)
            {
                AnalyzePattern(childPattern, currentNesting);
            }
            else
            {
                AnalyzeNode(child, currentNesting);
            }
        }
    }

    private void AnalyzeLogicalBinaryExpression(
        BinaryExpressionSyntax binaryExpression,
        int currentNesting)
    {
        List<SyntaxKind> operators = [];
        List<ExpressionSyntax> operands = [];

        FlattenLogicalBinaryExpression(binaryExpression, operators, operands);
        AddLogicalSequenceIncrements(operators);

        foreach (ExpressionSyntax operand in operands)
        {
            AnalyzeExpression(operand, currentNesting);
        }
    }

    private void AnalyzeLogicalPattern(
        BinaryPatternSyntax binaryPattern,
        int currentNesting)
    {
        List<SyntaxKind> operators = [];
        List<PatternSyntax> operands = [];

        FlattenLogicalPattern(binaryPattern, operators, operands);
        AddLogicalSequenceIncrements(operators);

        foreach (PatternSyntax operand in operands)
        {
            AnalyzePattern(operand, currentNesting);
        }
    }

    private void FlattenLogicalBinaryExpression(
        ExpressionSyntax expression,
        List<SyntaxKind> operators,
        List<ExpressionSyntax> operands)
    {
        cancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        if (expression is BinaryExpressionSyntax binaryExpression && IsLogicalBinaryExpression(binaryExpression))
        {
            FlattenLogicalBinaryExpression(binaryExpression.Left, operators, operands);
            operators.Add(binaryExpression.Kind());
            FlattenLogicalBinaryExpression(binaryExpression.Right, operators, operands);
            return;
        }

        operands.Add(expression);
    }

    private void FlattenLogicalPattern(
        PatternSyntax pattern,
        List<SyntaxKind> operators,
        List<PatternSyntax> operands)
    {
        cancellationToken.ThrowIfCancellationRequested();

        pattern = UnwrapParentheses(pattern);
        if (pattern is BinaryPatternSyntax binaryPattern && IsLogicalPattern(binaryPattern))
        {
            FlattenLogicalPattern(binaryPattern.Left, operators, operands);
            operators.Add(binaryPattern.Kind());
            FlattenLogicalPattern(binaryPattern.Right, operators, operands);
            return;
        }

        operands.Add(pattern);
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesizedExpression)
        {
            expression = parenthesizedExpression.Expression;
        }

        return expression;
    }

    private static PatternSyntax UnwrapParentheses(PatternSyntax pattern)
    {
        while (pattern is ParenthesizedPatternSyntax parenthesizedPattern)
        {
            pattern = parenthesizedPattern.Pattern;
        }

        return pattern;
    }

    private void AddLogicalSequenceIncrements(List<SyntaxKind> operators)
    {
        if (operators.Count == 0)
        {
            return;
        }

        AddStructuralIncrementWithoutNesting();
        SyntaxKind previous = operators[0];
        for (int index = 1; index < operators.Count; index++)
        {
            SyntaxKind current = operators[index];
            if (current != previous)
            {
                AddStructuralIncrementWithoutNesting();
                previous = current;
            }
        }
    }

    private void AnalyzeChildren(
        SyntaxNode node,
        int currentNesting)
    {
        foreach (SyntaxNode child in node.ChildNodes())
        {
            AnalyzeNode(child, currentNesting);
        }
    }

    private void AddStructuralIncrement(int currentNesting)
    {
        score = AddSaturating(score, AddSaturating(1, currentNesting));
    }

    private void AddStructuralIncrementWithoutNesting()
    {
        score = AddSaturating(score, 1);
    }

    private static int EnterControlFlow(int currentNesting)
    {
        return currentNesting == int.MaxValue
            ? int.MaxValue
            : currentNesting + 1;
    }

    private static int AddSaturating(
        int current,
        int increment)
    {
        return current > int.MaxValue - increment
            ? int.MaxValue
            : current + increment;
    }

    private static bool IsLogicalBinaryExpression(BinaryExpressionSyntax binaryExpression)
    {
        return binaryExpression.IsKind(SyntaxKind.LogicalAndExpression)
            || binaryExpression.IsKind(SyntaxKind.LogicalOrExpression);
    }

    private static bool IsLogicalPattern(BinaryPatternSyntax binaryPattern)
    {
        return binaryPattern.IsKind(SyntaxKind.AndPattern)
            || binaryPattern.IsKind(SyntaxKind.OrPattern);
    }

    private static bool IsNestedExecutableBoundary(SyntaxNode node)
    {
        return node is LocalFunctionStatementSyntax
            or AnonymousFunctionExpressionSyntax;
    }
}

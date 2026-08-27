using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class MaximumNestingDepthAnalyzer
{
    private int maximumDepth;
    private CancellationToken cancellationToken;

    internal bool TryAnalyze(
        ExecutableMember member,
        CancellationToken cancellationToken,
        out MaximumNestingDepthResult result)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));

        cancellationToken.ThrowIfCancellationRequested();

        if (!member.Body.HasBody)
        {
            result = default;
            return false;
        }

        maximumDepth = 0;
        this.cancellationToken = cancellationToken;

        SyntaxNode? bodyRoot = member.Body.Block ?? (SyntaxNode?)member.Body.Expression;
        AnalyzeNode(bodyRoot, currentDepth: 0);

        result = new MaximumNestingDepthResult(maximumDepth);
        return true;
    }

    private void AnalyzeNode(
        SyntaxNode? node,
        int currentDepth)
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
                AnalyzeIfStatement(ifStatement, currentDepth);
                break;
            case ForStatementSyntax forStatement:
                AnalyzeForStatement(forStatement, currentDepth);
                break;
            case ForEachStatementSyntax forEachStatement:
                AnalyzeForeachStatement(forEachStatement.Expression, forEachStatement.Statement, currentDepth);
                break;
            case ForEachVariableStatementSyntax forEachVariableStatement:
                AnalyzeForeachStatement(forEachVariableStatement.Expression, forEachVariableStatement.Statement, currentDepth);
                break;
            case WhileStatementSyntax whileStatement:
                AnalyzeExpression(whileStatement.Condition, currentDepth);
                AnalyzeNode(whileStatement.Statement, EnterControlFlow(currentDepth));
                break;
            case DoStatementSyntax doStatement:
                AnalyzeNode(doStatement.Statement, EnterControlFlow(currentDepth));
                AnalyzeExpression(doStatement.Condition, currentDepth);
                break;
            case SwitchStatementSyntax switchStatement:
                AnalyzeSwitchStatement(switchStatement, currentDepth);
                break;
            case SwitchExpressionSyntax switchExpression:
                AnalyzeSwitchExpression(switchExpression, currentDepth);
                break;
            case TryStatementSyntax tryStatement:
                AnalyzeTryStatement(tryStatement, currentDepth);
                break;
            case ConditionalExpressionSyntax conditionalExpression:
                AnalyzeConditionalExpression(conditionalExpression, currentDepth);
                break;
            default:
                AnalyzeChildren(node, currentDepth);
                break;
        }
    }

    private void AnalyzeIfStatement(
        IfStatementSyntax ifStatement,
        int currentDepth)
    {
        AnalyzeExpression(ifStatement.Condition, currentDepth);

        int branchDepth = EnterControlFlow(currentDepth);
        AnalyzeNode(ifStatement.Statement, branchDepth);

        if (ifStatement.Else is null)
        {
            return;
        }

        if (ifStatement.Else.Statement is IfStatementSyntax elseIf)
        {
            AnalyzeIfStatement(elseIf, currentDepth);
            return;
        }

        AnalyzeNode(ifStatement.Else.Statement, branchDepth);
    }

    private void AnalyzeForStatement(
        ForStatementSyntax forStatement,
        int currentDepth)
    {
        AnalyzeNode(forStatement.Declaration, currentDepth);

        foreach (ExpressionSyntax initializer in forStatement.Initializers)
        {
            AnalyzeExpression(initializer, currentDepth);
        }

        AnalyzeExpression(forStatement.Condition, currentDepth);

        foreach (ExpressionSyntax incrementor in forStatement.Incrementors)
        {
            AnalyzeExpression(incrementor, currentDepth);
        }

        AnalyzeNode(forStatement.Statement, EnterControlFlow(currentDepth));
    }

    private void AnalyzeForeachStatement(
        ExpressionSyntax expression,
        StatementSyntax statement,
        int currentDepth)
    {
        AnalyzeExpression(expression, currentDepth);
        AnalyzeNode(statement, EnterControlFlow(currentDepth));
    }

    private void AnalyzeSwitchStatement(
        SwitchStatementSyntax switchStatement,
        int currentDepth)
    {
        AnalyzeExpression(switchStatement.Expression, currentDepth);

        int branchDepth = EnterControlFlow(currentDepth);
        foreach (SwitchSectionSyntax section in switchStatement.Sections)
        {
            foreach (SwitchLabelSyntax label in section.Labels)
            {
                AnalyzeNode(label, branchDepth);
            }

            foreach (StatementSyntax statement in section.Statements)
            {
                AnalyzeNode(statement, branchDepth);
            }
        }
    }

    private void AnalyzeSwitchExpression(
        SwitchExpressionSyntax switchExpression,
        int currentDepth)
    {
        AnalyzeExpression(switchExpression.GoverningExpression, currentDepth);

        int armDepth = EnterControlFlow(currentDepth);
        foreach (SwitchExpressionArmSyntax arm in switchExpression.Arms)
        {
            AnalyzeNode(arm.Pattern, armDepth);
            AnalyzeNode(arm.WhenClause, armDepth);
            AnalyzeExpression(arm.Expression, armDepth);
        }
    }

    private void AnalyzeTryStatement(
        TryStatementSyntax tryStatement,
        int currentDepth)
    {
        int branchDepth = EnterControlFlow(currentDepth);
        AnalyzeNode(tryStatement.Block, branchDepth);

        foreach (CatchClauseSyntax catchClause in tryStatement.Catches)
        {
            AnalyzeNode(catchClause.Filter, branchDepth);
            AnalyzeNode(catchClause.Block, branchDepth);
        }

        AnalyzeNode(tryStatement.Finally?.Block, branchDepth);
    }

    private void AnalyzeConditionalExpression(
        ConditionalExpressionSyntax conditionalExpression,
        int currentDepth)
    {
        AnalyzeExpression(conditionalExpression.Condition, currentDepth);

        int branchDepth = EnterControlFlow(currentDepth);
        AnalyzeExpression(conditionalExpression.WhenTrue, branchDepth);
        AnalyzeExpression(conditionalExpression.WhenFalse, branchDepth);
    }

    private void AnalyzeExpression(
        ExpressionSyntax? expression,
        int currentDepth)
    {
        AnalyzeNode(expression, currentDepth);
    }

    private void AnalyzeChildren(
        SyntaxNode node,
        int currentDepth)
    {
        foreach (SyntaxNode child in node.ChildNodes())
        {
            AnalyzeNode(child, currentDepth);
        }
    }

    private int EnterControlFlow(int currentDepth)
    {
        int nextDepth = currentDepth == int.MaxValue
            ? int.MaxValue
            : currentDepth + 1;

        if (nextDepth > maximumDepth)
        {
            maximumDepth = nextDepth;
        }

        return nextDepth;
    }

    private static bool IsNestedExecutableBoundary(SyntaxNode node)
    {
        return node is LocalFunctionStatementSyntax
            or AnonymousFunctionExpressionSyntax;
    }
}

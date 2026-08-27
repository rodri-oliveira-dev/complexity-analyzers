using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class CyclomaticComplexityAnalyzer
{
    internal bool TryAnalyze(
        ExecutableMember member,
        CyclomaticComplexityAnalysisMode mode,
        CancellationToken cancellationToken,
        out CyclomaticComplexityResult result)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));

        cancellationToken.ThrowIfCancellationRequested();

        if (!member.Body.HasBody)
        {
            result = default;
            return false;
        }

        int decisionPoints = 0;
        foreach (SyntaxNode node in ExecutableMemberSyntax.DescendantNodesInOwnBody<SyntaxNode>(member))
        {
            cancellationToken.ThrowIfCancellationRequested();
            decisionPoints = AddDecisionPoints(
                decisionPoints,
                CountDecisionPoints(node, mode));
        }

        result = new CyclomaticComplexityResult(AddDecisionPoints(1, decisionPoints));
        return true;
    }

    private static int CountDecisionPoints(
        SyntaxNode node,
        CyclomaticComplexityAnalysisMode mode)
    {
        return node switch
        {
            IfStatementSyntax
                or ForStatementSyntax
                or ForEachStatementSyntax
                or ForEachVariableStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or CatchClauseSyntax
                or CatchFilterClauseSyntax
                or ConditionalExpressionSyntax => 1,
            BinaryExpressionSyntax binaryExpression
                when binaryExpression.IsKind(SyntaxKind.LogicalAndExpression)
                    || binaryExpression.IsKind(SyntaxKind.LogicalOrExpression) => 1,
            BinaryPatternSyntax binaryPattern
                when binaryPattern.IsKind(SyntaxKind.OrPattern) => 1,
            WhenClauseSyntax => 1,
            SwitchStatementSyntax switchStatement =>
                mode == CyclomaticComplexityAnalysisMode.ModifiedMcCabe
                    && HasDecisionSwitchLabel(switchStatement)
                    ? 1
                    : 0,
            CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax =>
                mode == CyclomaticComplexityAnalysisMode.Standard ? 1 : 0,
            SwitchExpressionSyntax switchExpression =>
                mode == CyclomaticComplexityAnalysisMode.ModifiedMcCabe
                    && HasDecisionSwitchArm(switchExpression)
                    ? 1
                    : 0,
            SwitchExpressionArmSyntax switchExpressionArm =>
                mode == CyclomaticComplexityAnalysisMode.Standard
                    && !IsDefaultSwitchExpressionArm(switchExpressionArm)
                    ? 1
                    : 0,
            _ => 0,
        };
    }

    private static bool HasDecisionSwitchLabel(SwitchStatementSyntax switchStatement)
    {
        foreach (SwitchSectionSyntax section in switchStatement.Sections)
        {
            foreach (SwitchLabelSyntax label in section.Labels)
            {
                if (label is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasDecisionSwitchArm(SwitchExpressionSyntax switchExpression)
    {
        foreach (SwitchExpressionArmSyntax arm in switchExpression.Arms)
        {
            if (!IsDefaultSwitchExpressionArm(arm))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDefaultSwitchExpressionArm(SwitchExpressionArmSyntax arm)
    {
        return arm.Pattern is DiscardPatternSyntax
            && arm.WhenClause is null;
    }

    private static int AddDecisionPoints(
        int current,
        int increment)
    {
        return current > int.MaxValue - increment
            ? int.MaxValue
            : current + increment;
    }
}

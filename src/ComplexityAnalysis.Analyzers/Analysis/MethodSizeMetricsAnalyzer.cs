using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class MethodSizeMetricsAnalyzer
{
    internal bool TryAnalyze(
        ExecutableMember member,
        CancellationToken cancellationToken,
        out MethodSizeMetricsResult result)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));

        cancellationToken.ThrowIfCancellationRequested();

        if (!member.Body.HasBody)
        {
            result = default;
            return false;
        }

        SourceText sourceText = member.SyntaxTree.GetText(cancellationToken);
        HashSet<int> nlocLines = [];
        int statementCount = member.Body.Expression is not null ? 1 : 0;
        int tokenCount = 0;

        foreach (StatementSyntax statement in ExecutableMemberSyntax.DescendantNodesInOwnBody<StatementSyntax>(member))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (statement is not BlockSyntax)
            {
                statementCount = AddSaturating(statementCount, 1);
            }
        }

        foreach (SyntaxToken syntaxToken in ExecutableMemberSyntax.DescendantTokensInOwnBody(member))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (syntaxToken.IsMissing || syntaxToken.IsKind(SyntaxKind.EndOfFileToken))
            {
                continue;
            }

            tokenCount = AddSaturating(tokenCount, 1);

            if (IsNlocRelevantToken(syntaxToken))
            {
                AddTokenLines(nlocLines, sourceText, syntaxToken);
            }
        }

        result = new MethodSizeMetricsResult(
            nlocLines.Count,
            statementCount,
            tokenCount);
        return true;
    }

    private static void AddTokenLines(
        HashSet<int> lines,
        SourceText sourceText,
        SyntaxToken syntaxToken)
    {
        if (syntaxToken.Span.Length == 0)
        {
            _ = lines.Add(sourceText.Lines.IndexOf(syntaxToken.SpanStart));
            return;
        }

        int startLine = sourceText.Lines.IndexOf(syntaxToken.SpanStart);
        int endLine = sourceText.Lines.IndexOf(syntaxToken.Span.End - 1);
        for (int line = startLine; line <= endLine; line++)
        {
            _ = lines.Add(line);
        }
    }

    private static bool IsNlocRelevantToken(SyntaxToken syntaxToken)
    {
        return !syntaxToken.IsKind(SyntaxKind.OpenBraceToken)
            && !syntaxToken.IsKind(SyntaxKind.CloseBraceToken)
            && !syntaxToken.IsKind(SyntaxKind.OpenParenToken)
            && !syntaxToken.IsKind(SyntaxKind.CloseParenToken)
            && !syntaxToken.IsKind(SyntaxKind.OpenBracketToken)
            && !syntaxToken.IsKind(SyntaxKind.CloseBracketToken)
            && !syntaxToken.IsKind(SyntaxKind.SemicolonToken)
            && !syntaxToken.IsKind(SyntaxKind.CommaToken)
            && !syntaxToken.IsKind(SyntaxKind.DotToken)
            && !syntaxToken.IsKind(SyntaxKind.ColonToken)
            && !syntaxToken.IsKind(SyntaxKind.QuestionToken);
    }

    private static int AddSaturating(int current, int increment)
    {
        return current > int.MaxValue - increment
            ? int.MaxValue
            : current + increment;
    }
}

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal static class ExecutableMemberSyntax
{
    internal static IEnumerable<TNode> DescendantNodesInOwnBody<TNode>(ExecutableMember member)
        where TNode : SyntaxNode
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));

        SyntaxNode? bodyRoot = member.Body.Block ?? (SyntaxNode?)member.Body.Expression;
        if (bodyRoot is null)
        {
            yield break;
        }

        if (bodyRoot is TNode typedRoot)
        {
            yield return typedRoot;
        }

        if (IsNestedExecutableBoundary(bodyRoot))
        {
            yield break;
        }

        foreach (SyntaxNode node in bodyRoot.DescendantNodes(descendIntoChildren: ShouldDescendIntoChildren))
        {
            if (node is TNode typedNode)
            {
                yield return typedNode;
            }
        }
    }

    internal static IEnumerable<TNode> DescendantNodesAndSelfExcludingNestedExecutableBodies<TNode>(SyntaxNode node)
        where TNode : SyntaxNode
    {
        _ = node ?? throw new ArgumentNullException(nameof(node));

        if (node is TNode typedSelf)
        {
            yield return typedSelf;
        }

        if (IsNestedExecutableBoundary(node))
        {
            yield break;
        }

        foreach (SyntaxNode descendant in node.DescendantNodes(descendIntoChildren: ShouldDescendIntoChildren))
        {
            if (descendant is TNode typedDescendant)
            {
                yield return typedDescendant;
            }
        }
    }

    internal static IEnumerable<SyntaxToken> DescendantTokensInOwnBody(ExecutableMember member)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));

        SyntaxNode? bodyRoot = member.Body.Block ?? (SyntaxNode?)member.Body.Expression;
        if (bodyRoot is null)
        {
            yield break;
        }

        foreach (SyntaxToken syntaxToken in DescendantTokensExcludingNestedExecutableBodies(bodyRoot))
        {
            yield return syntaxToken;
        }
    }

    private static IEnumerable<SyntaxToken> DescendantTokensExcludingNestedExecutableBodies(SyntaxNode node)
    {
        foreach (SyntaxNodeOrToken child in node.ChildNodesAndTokens())
        {
            if (child.IsToken)
            {
                yield return child.AsToken();
                continue;
            }

            SyntaxNode childNode = child.AsNode()!;
            if (IsNestedExecutableBody(node, childNode))
            {
                continue;
            }

            foreach (SyntaxToken syntaxToken in DescendantTokensExcludingNestedExecutableBodies(childNode))
            {
                yield return syntaxToken;
            }
        }
    }

    private static bool IsNestedExecutableBody(
        SyntaxNode node,
        SyntaxNode childNode)
    {
        return node switch
        {
            LocalFunctionStatementSyntax localFunction =>
                childNode == localFunction.Body
                    || childNode == localFunction.ExpressionBody,
            LambdaExpressionSyntax lambda => childNode == lambda.Body,
            AnonymousMethodExpressionSyntax anonymousMethod => childNode == anonymousMethod.Block,
            _ => false,
        };
    }

    private static bool ShouldDescendIntoChildren(SyntaxNode node)
    {
        return !IsNestedExecutableBoundary(node);
    }

    private static bool IsNestedExecutableBoundary(SyntaxNode node)
    {
        return node is LocalFunctionStatementSyntax
            or AnonymousFunctionExpressionSyntax;
    }
}

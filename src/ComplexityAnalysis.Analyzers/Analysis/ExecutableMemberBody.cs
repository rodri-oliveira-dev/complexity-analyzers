using System;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct ExecutableMemberBody
{
    private ExecutableMemberBody(
        BlockSyntax? block,
        ExpressionSyntax? expression)
    {
        Block = block;
        Expression = expression;
    }

    internal BlockSyntax? Block
    {
        get;
    }

    internal ExpressionSyntax? Expression
    {
        get;
    }

    internal bool HasBody
        => Block is not null || Expression is not null;

    internal static ExecutableMemberBody FromBlock(BlockSyntax block)
    {
        _ = block ?? throw new ArgumentNullException(nameof(block));

        return new ExecutableMemberBody(block, expression: null);
    }

    internal static ExecutableMemberBody FromExpression(ExpressionSyntax expression)
    {
        _ = expression ?? throw new ArgumentNullException(nameof(expression));

        return new ExecutableMemberBody(block: null, expression);
    }

    internal static ExecutableMemberBody None()
    {
        return new ExecutableMemberBody(block: null, expression: null);
    }
}

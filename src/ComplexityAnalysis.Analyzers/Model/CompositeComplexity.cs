using System;

namespace ComplexityAnalysis.Analyzers.Model;

internal sealed class CompositeComplexity : ComplexityExpression, IEquatable<CompositeComplexity>
{
    internal CompositeComplexity(
        ComplexityExpression left,
        ComplexityOperation operation,
        ComplexityExpression right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));

        if (!Enum.IsDefined(typeof(ComplexityOperation), operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown complexity operation.");
        }

        Operation = operation;
    }

    internal ComplexityExpression Left
    {
        get;
    }

    internal ComplexityOperation Operation
    {
        get;
    }

    internal ComplexityExpression Right
    {
        get;
    }

    public bool Equals(CompositeComplexity? other)
    {
        return other is not null
            && Operation == other.Operation
            && Left.Equals(other.Left)
            && Right.Equals(other.Right);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CompositeComplexity);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Left.GetHashCode();
            hash = (hash * 397) ^ Operation.GetHashCode();
            hash = (hash * 397) ^ Right.GetHashCode();
            return hash;
        }
    }

    internal override string ToBigOBody()
    {
        return Operation switch
        {
            ComplexityOperation.Sequential => FormatOperand(Left, Operation) + " + " + FormatOperand(Right, Operation),
            ComplexityOperation.Nested => FormatOperand(Left, Operation) + " \u00b7 " + FormatOperand(Right, Operation),
            ComplexityOperation.Maximum => "max(" + Left.ToBigOBody() + ", " + Right.ToBigOBody() + ")",
            _ => throw new InvalidOperationException("Unknown complexity operation."),
        };
    }

    private static string FormatOperand(ComplexityExpression expression, ComplexityOperation parentOperation)
    {
        return expression is CompositeComplexity composite && NeedsParentheses(composite.Operation, parentOperation)
            ? "(" + expression.ToBigOBody() + ")"
            : expression.ToBigOBody();
    }

    private static bool NeedsParentheses(ComplexityOperation childOperation, ComplexityOperation parentOperation)
    {
        return parentOperation == ComplexityOperation.Nested
            && childOperation != ComplexityOperation.Nested;
    }
}

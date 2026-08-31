using System;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class HalsteadElement
{
    private HalsteadElement(HalsteadElementIdentity identity)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    internal HalsteadElementIdentity Identity
    {
        get;
    }

    internal HalsteadElementRole Role
        => Identity.Role;

    internal static HalsteadElement Operator(HalsteadOperatorKind kind)
    {
        return new HalsteadElement(HalsteadElementIdentity.ForOperator(kind));
    }

    internal static HalsteadElement Operand(
        HalsteadOperandKind kind,
        string canonicalValue)
    {
        return new HalsteadElement(HalsteadElementIdentity.ForOperand(kind, canonicalValue));
    }
}

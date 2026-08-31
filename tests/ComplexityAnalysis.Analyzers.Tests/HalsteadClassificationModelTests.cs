using ComplexityAnalysis.Analyzers.Analysis;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class HalsteadClassificationModelTests
{
    [Fact]
    public void Result_reports_primitive_counts_from_classified_elements()
    {
        HalsteadClassificationResult result = new(
        [
            HalsteadElement.Operator(HalsteadOperatorKind.Add),
            HalsteadElement.Operand(HalsteadOperandKind.Local, "value"),
            HalsteadElement.Operator(HalsteadOperatorKind.Add),
            HalsteadElement.Operand(HalsteadOperandKind.Local, "value"),
            HalsteadElement.Operand(HalsteadOperandKind.Parameter, "right"),
        ]);

        Assert.Equal(1, result.DistinctOperatorCount);
        Assert.Equal(2, result.DistinctOperandCount);
        Assert.Equal(2, result.TotalOperatorCount);
        Assert.Equal(3, result.TotalOperandCount);
    }

    [Fact]
    public void Empty_result_has_zero_primitive_counts()
    {
        HalsteadClassificationResult result = HalsteadClassificationResult.Empty;

        Assert.Equal(0, result.DistinctOperatorCount);
        Assert.Equal(0, result.DistinctOperandCount);
        Assert.Equal(0, result.TotalOperatorCount);
        Assert.Equal(0, result.TotalOperandCount);
        Assert.Empty(result.Elements);
    }

    [Fact]
    public void Repeated_operator_occurrences_share_one_distinct_identity()
    {
        HalsteadElementIdentity first = HalsteadElementIdentity.ForOperator(HalsteadOperatorKind.NullCoalescing);
        HalsteadElementIdentity second = HalsteadElementIdentity.ForOperator(HalsteadOperatorKind.NullCoalescing);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal("Operator:NullCoalescing", first.ToString());
    }

    [Fact]
    public void Renaming_an_operand_changes_operand_identity_without_affecting_operator_identity()
    {
        HalsteadElementIdentity leftName = HalsteadElementIdentity.ForOperand(HalsteadOperandKind.Local, "left");
        HalsteadElementIdentity renamedLeftName = HalsteadElementIdentity.ForOperand(HalsteadOperandKind.Local, "renamedLeft");
        HalsteadElementIdentity addOperator = HalsteadElementIdentity.ForOperator(HalsteadOperatorKind.Add);
        HalsteadElementIdentity renamedAddOperator = HalsteadElementIdentity.ForOperator(HalsteadOperatorKind.Add);

        Assert.NotEqual(leftName, renamedLeftName);
        Assert.Equal(addOperator, renamedAddOperator);
    }

    [Fact]
    public void Different_literal_values_have_different_operand_identities()
    {
        HalsteadElementIdentity one = HalsteadElementIdentity.ForOperand(HalsteadOperandKind.NumericLiteral, "int:1");
        HalsteadElementIdentity two = HalsteadElementIdentity.ForOperand(HalsteadOperandKind.NumericLiteral, "int:2");
        HalsteadElementIdentity oneAgain = HalsteadElementIdentity.ForOperand(HalsteadOperandKind.NumericLiteral, "int:1");

        Assert.Equal(one, oneAgain);
        Assert.NotEqual(one, two);
    }

    [Fact]
    public void Model_represents_modern_csharp_operator_identities()
    {
        HalsteadOperatorKind[] kinds =
        [
            HalsteadOperatorKind.ConditionalAccess,
            HalsteadOperatorKind.ConditionalElementAccess,
            HalsteadOperatorKind.NullCoalescing,
            HalsteadOperatorKind.NullCoalescingAssignment,
            HalsteadOperatorKind.LambdaOrExpressionBody,
            HalsteadOperatorKind.PatternNot,
            HalsteadOperatorKind.PatternAnd,
            HalsteadOperatorKind.PatternOr,
            HalsteadOperatorKind.CollectionExpression,
            HalsteadOperatorKind.CollectionSpread,
            HalsteadOperatorKind.Range,
            HalsteadOperatorKind.Index,
            HalsteadOperatorKind.SwitchArm,
        ];

        foreach (HalsteadOperatorKind kind in kinds)
        {
            HalsteadElement element = HalsteadElement.Operator(kind);

            Assert.Equal(HalsteadElementRole.Operator, element.Role);
            Assert.Equal(kind.ToString(), element.Identity.Kind);
            Assert.Equal(string.Empty, element.Identity.CanonicalValue);
        }
    }

    [Fact]
    public void Model_represents_csharp_operand_identities()
    {
        (HalsteadOperandKind Kind, string CanonicalValue)[] identities =
        [
            (HalsteadOperandKind.Parameter, "parameter:value"),
            (HalsteadOperandKind.Property, "property:Sample.Value"),
            (HalsteadOperandKind.Method, "method:Sample.Calculate(int)"),
            (HalsteadOperandKind.TypeName, "type:System.String"),
            (HalsteadOperandKind.PatternVariable, "pattern:number"),
            (HalsteadOperandKind.StringLiteral, "string:hello"),
            (HalsteadOperandKind.BooleanLiteral, "bool:true"),
            (HalsteadOperandKind.NullLiteral, "null"),
        ];

        foreach ((HalsteadOperandKind kind, string canonicalValue) in identities)
        {
            HalsteadElement element = HalsteadElement.Operand(kind, canonicalValue);

            Assert.Equal(HalsteadElementRole.Operand, element.Role);
            Assert.Equal(kind.ToString(), element.Identity.Kind);
            Assert.Equal(canonicalValue, element.Identity.CanonicalValue);
        }
    }

    [Fact]
    public void Operand_identity_requires_a_canonical_value()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            HalsteadElementIdentity.ForOperand(HalsteadOperandKind.Identifier, string.Empty));
    }

    [Fact]
    public void Identity_object_equality_handles_non_identity_instances()
    {
        HalsteadElementIdentity identity = HalsteadElementIdentity.ForOperator(HalsteadOperatorKind.Return);

        Assert.False(identity.Equals(null));
        Assert.False(identity.Equals("return"));
    }
}

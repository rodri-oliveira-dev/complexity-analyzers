using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceReduction : IEquatable<RecurrenceReduction>
{
    private RecurrenceReduction(RecurrenceReductionKind kind, double value)
    {
        if (!Enum.IsDefined(typeof(RecurrenceReductionKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown recurrence reduction kind.");
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Reduction value must be finite.");
        }

        if (kind == RecurrenceReductionKind.SubtractConstant && value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Constant decrement must be greater than zero.");
        }

        if (kind == RecurrenceReductionKind.Scale && (value <= 0 || value >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Scale reduction must be greater than zero and less than one.");
        }

        Kind = kind;
        Value = value;
    }

    internal RecurrenceReductionKind Kind
    {
        get;
    }

    internal double Value
    {
        get;
    }

    internal static RecurrenceReduction SubtractConstant(double decrement)
    {
        return new RecurrenceReduction(RecurrenceReductionKind.SubtractConstant, decrement);
    }

    internal static RecurrenceReduction Scale(double scale)
    {
        return new RecurrenceReduction(RecurrenceReductionKind.Scale, scale);
    }

    public bool Equals(RecurrenceReduction? other)
    {
        return other is not null
            && Kind == other.Kind
            && Value.CompareTo(other.Value) == 0;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RecurrenceReduction);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Kind.GetHashCode() * 397) ^ Value.GetHashCode();
        }
    }
}

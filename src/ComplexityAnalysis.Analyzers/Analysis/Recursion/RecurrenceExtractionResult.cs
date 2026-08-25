using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceExtractionResult
{
    private RecurrenceExtractionResult(
        RecurrenceExtractionResultKind kind,
        RecurrenceRelation? relation,
        string? reason)
    {
        if (!Enum.IsDefined(typeof(RecurrenceExtractionResultKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown recurrence extraction result kind.");
        }

        if (kind == RecurrenceExtractionResultKind.Extracted && relation is null)
        {
            throw new ArgumentException("An extracted recurrence result must include a recurrence relation.", nameof(relation));
        }

        if (kind != RecurrenceExtractionResultKind.Extracted && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A non-extracted recurrence result must include a reason.", nameof(reason));
        }

        Kind = kind;
        Relation = relation;
        Reason = reason;
    }

    internal RecurrenceExtractionResultKind Kind
    {
        get;
    }

    internal RecurrenceRelation? Relation
    {
        get;
    }

    internal string? Reason
    {
        get;
    }

    internal bool IsExtracted => Kind == RecurrenceExtractionResultKind.Extracted;

    internal static RecurrenceExtractionResult Extracted(RecurrenceRelation relation)
    {
        return new RecurrenceExtractionResult(
            RecurrenceExtractionResultKind.Extracted,
            relation ?? throw new ArgumentNullException(nameof(relation)),
            reason: null);
    }

    internal static RecurrenceExtractionResult Unsupported(string reason)
    {
        return new RecurrenceExtractionResult(
            RecurrenceExtractionResultKind.Unsupported,
            relation: null,
            reason);
    }

    internal static RecurrenceExtractionResult Unknown(string reason)
    {
        return new RecurrenceExtractionResult(
            RecurrenceExtractionResultKind.Unknown,
            relation: null,
            reason);
    }
}

using System;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal sealed class KnownOperationProvenance : IEquatable<KnownOperationProvenance>
{
    private KnownOperationProvenance(KnownOperationProvenanceKind kind, string note)
    {
        Kind = kind;
        Note = note;
    }

    internal KnownOperationProvenanceKind Kind
    {
        get;
    }

    internal string Note
    {
        get;
    }

    internal static KnownOperationProvenance OfficialDocumentation(string note)
    {
        return Create(KnownOperationProvenanceKind.OfficialDocumentation, note);
    }

    internal static KnownOperationProvenance RuntimeSource(string note)
    {
        return Create(KnownOperationProvenanceKind.RuntimeSource, note);
    }

    internal static KnownOperationProvenance Conservative(string note)
    {
        return Create(KnownOperationProvenanceKind.Conservative, note);
    }

    public bool Equals(KnownOperationProvenance? other)
    {
        return other is not null
            && Kind == other.Kind
            && StringComparer.Ordinal.Equals(Note, other.Note);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as KnownOperationProvenance);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Note);
        }
    }

    private static KnownOperationProvenance Create(KnownOperationProvenanceKind kind, string note)
    {
        _ = note ?? throw new ArgumentNullException(nameof(note));

        return note.Length == 0
            ? throw new ArgumentException("Provenance note must not be empty.", nameof(note))
            : new KnownOperationProvenance(kind, note);
    }
}

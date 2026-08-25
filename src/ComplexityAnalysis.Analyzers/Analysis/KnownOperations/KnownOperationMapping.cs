using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal sealed class KnownOperationMapping : IEquatable<KnownOperationMapping>
{
    internal KnownOperationMapping(
        KnownOperationIdentity identity,
        ComplexityExpression complexity,
        KnownOperationExecutionKind executionKind,
        KnownOperationProvenance provenance,
        KnownOperationMetadata metadata,
        KnownOperationComplexityCase complexityCase = KnownOperationComplexityCase.WorstCase)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Complexity = complexity ?? throw new ArgumentNullException(nameof(complexity));
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        ExecutionKind = executionKind;
        ComplexityCase = complexityCase;
    }

    internal KnownOperationIdentity Identity
    {
        get;
    }

    internal ComplexityExpression Complexity
    {
        get;
    }

    internal KnownOperationExecutionKind ExecutionKind
    {
        get;
    }

    internal KnownOperationProvenance Provenance
    {
        get;
    }

    internal KnownOperationMetadata Metadata
    {
        get;
    }

    internal KnownOperationComplexityCase ComplexityCase
    {
        get;
    }

    public bool Equals(KnownOperationMapping? other)
    {
        return other is not null
            && Identity.Equals(other.Identity)
            && Complexity.Equals(other.Complexity)
            && ExecutionKind == other.ExecutionKind
            && Provenance.Equals(other.Provenance)
            && Metadata.Equals(other.Metadata)
            && ComplexityCase == other.ComplexityCase;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as KnownOperationMapping);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Identity.GetHashCode();
            hash = (hash * 397) ^ Complexity.GetHashCode();
            hash = (hash * 397) ^ ExecutionKind.GetHashCode();
            hash = (hash * 397) ^ Provenance.GetHashCode();
            hash = (hash * 397) ^ Metadata.GetHashCode();
            hash = (hash * 397) ^ ComplexityCase.GetHashCode();
            return hash;
        }
    }
}

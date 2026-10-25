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
        KnownOperationMetadata metadata)
        : this(
            identity,
            CreateEstimate(complexity),
            executionKind,
            provenance,
            metadata)
    {
    }

    internal KnownOperationMapping(
        KnownOperationIdentity identity,
        ComplexityEstimate estimate,
        KnownOperationExecutionKind executionKind,
        KnownOperationProvenance provenance,
        KnownOperationMetadata metadata)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Estimate = estimate ?? throw new ArgumentNullException(nameof(estimate));
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        ExecutionKind = executionKind;
    }

    internal KnownOperationIdentity Identity
    {
        get;
    }

    internal ComplexityExpression Complexity => Estimate.Complexity;

    internal ComplexityEstimate Estimate
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

    public bool Equals(KnownOperationMapping? other)
    {
        return other is not null
            && Identity.Equals(other.Identity)
            && Estimate.Equals(other.Estimate)
            && ExecutionKind == other.ExecutionKind
            && Provenance.Equals(other.Provenance)
            && Metadata.Equals(other.Metadata);
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
            hash = (hash * 397) ^ Estimate.GetHashCode();
            hash = (hash * 397) ^ ExecutionKind.GetHashCode();
            hash = (hash * 397) ^ Provenance.GetHashCode();
            hash = (hash * 397) ^ Metadata.GetHashCode();
            return hash;
        }
    }

    private static ComplexityEstimate CreateEstimate(ComplexityExpression complexity)
    {
        _ = complexity ?? throw new ArgumentNullException(nameof(complexity));

        return complexity is UnknownComplexity
            ? ComplexityEstimate.Unknown()
            : ComplexityEstimate.WorstCaseUpperBound(complexity);
    }
}

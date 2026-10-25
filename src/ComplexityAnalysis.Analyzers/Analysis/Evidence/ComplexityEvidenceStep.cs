using System;
using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Evidence;

internal sealed class ComplexityEvidenceStep
{
    internal ComplexityEvidenceStep(
        string identity,
        ComplexityEvidenceKind kind,
        ComplexityEvidenceOrigin origin,
        string operation,
        ComplexityEvidenceLocation location,
        int depth,
        ImmutableArray<string> dimensions,
        ComplexityEstimate estimate,
        ComplexityOperation? composition,
        ComplexityUnknownReason unknownReason)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        Dimensions = dimensions;
        Estimate = estimate ?? throw new ArgumentNullException(nameof(estimate));
        Kind = kind;
        Origin = origin;
        Location = location;
        Depth = depth;
        Composition = composition;
        UnknownReason = unknownReason;
    }

    internal string Identity
    {
        get;
    }

    internal ComplexityEvidenceKind Kind
    {
        get;
    }

    internal ComplexityEvidenceOrigin Origin
    {
        get;
    }

    internal string Operation
    {
        get;
    }

    internal ComplexityEvidenceLocation Location
    {
        get;
    }

    internal int Depth
    {
        get;
    }

    internal ImmutableArray<string> Dimensions
    {
        get;
    }

    internal ComplexityEstimate Estimate
    {
        get;
    }

    internal ComplexityOperation? Composition
    {
        get;
    }

    internal ComplexityUnknownReason UnknownReason
    {
        get;
    }
}

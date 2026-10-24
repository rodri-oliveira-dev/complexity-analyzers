using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Evidence;

internal sealed class ComplexityEvidenceTrace
{
    internal ComplexityEvidenceTrace(
        ComplexityExpression complexity,
        ImmutableArray<ComplexityEvidenceStep> steps,
        bool isTruncated,
        int maximumDepth,
        int maximumSteps)
    {
        Complexity = complexity ?? throw new ArgumentNullException(nameof(complexity));
        Steps = steps;
        IsTruncated = isTruncated;
        MaximumDepth = maximumDepth;
        MaximumSteps = maximumSteps;
    }

    internal ComplexityExpression Complexity
    {
        get;
    }

    internal ImmutableArray<ComplexityEvidenceStep> Steps
    {
        get;
    }

    internal bool IsTruncated
    {
        get;
    }

    internal int MaximumDepth
    {
        get;
    }

    internal int MaximumSteps
    {
        get;
    }

    internal bool HasExplicitUnknownReason
        => Steps.Any(step => step.UnknownReason != ComplexityUnknownReason.None);

    internal ComplexityUnknownReason UnknownReason
    {
        get
        {
            if (Complexity is not UnknownComplexity)
            {
                return ComplexityUnknownReason.None;
            }

            ComplexityUnknownReason explicitReason = Steps
                .Select(step => step.UnknownReason)
                .FirstOrDefault(reason => reason != ComplexityUnknownReason.None);
            if (explicitReason != ComplexityUnknownReason.None)
            {
                return explicitReason;
            }

            return IsTruncated
                ? ComplexityUnknownReason.EvidenceLimitExceeded
                : ComplexityUnknownReason.SourceAnalysisUnknown;
        }
    }

    internal ImmutableArray<string> CreateDeterministicSnapshot()
    {
        return Steps.Select(step => string.Join(
                "|",
                step.Identity,
                step.Kind.ToString(),
                step.Origin.ToString(),
                step.Operation,
                step.Location.RelativeStart.ToString(CultureInfo.InvariantCulture),
                step.Location.Length.ToString(CultureInfo.InvariantCulture),
                step.Depth.ToString(CultureInfo.InvariantCulture),
                string.Join(",", step.Dimensions),
                step.Estimate.Complexity.ToBigONotation(),
                step.Estimate.Guarantee.ToString(),
                step.Composition?.ToString() ?? "None",
                step.UnknownReason.ToString(),
                string.Join(",", step.Estimate.Assumptions.Select(assumption =>
                    assumption.Kind + ":" + assumption.Description))))
            .ToImmutableArray();
    }
}

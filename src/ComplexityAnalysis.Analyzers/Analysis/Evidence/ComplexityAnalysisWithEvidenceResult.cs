using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Evidence;

internal sealed class ComplexityAnalysisWithEvidenceResult
{
    internal ComplexityAnalysisWithEvidenceResult(
        ComplexityExpression complexity,
        ComplexityEvidenceTrace evidence)
    {
        Complexity = complexity ?? throw new ArgumentNullException(nameof(complexity));
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
    }

    internal ComplexityExpression Complexity
    {
        get;
    }

    internal ComplexityEvidenceTrace Evidence
    {
        get;
    }
}

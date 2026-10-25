namespace ComplexityAnalysis.Analyzers.Analysis.Evidence;

internal enum ComplexityUnknownReason
{
    None,
    UnsupportedOperation,
    UnresolvedSymbol,
    UnprovenLoopBound,
    UnresolvedInputDimension,
    SourceAnalysisUnknown,
    InterproceduralAnalysisDisabled,
    BudgetExceeded,
    CycleBoundary,
    EvidenceLimitExceeded,
}

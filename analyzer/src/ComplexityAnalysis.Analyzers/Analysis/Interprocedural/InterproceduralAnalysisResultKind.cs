namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal enum InterproceduralAnalysisResultKind
{
    Known,
    Unknown,
    BudgetExceeded,
    CycleBoundary,
}

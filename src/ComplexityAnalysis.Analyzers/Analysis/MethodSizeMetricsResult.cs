namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct MethodSizeMetricsResult
{
    internal MethodSizeMetricsResult(
        int nloc,
        int statementCount,
        int tokenCount)
    {
        Nloc = nloc;
        StatementCount = statementCount;
        TokenCount = tokenCount;
    }

    internal int Nloc
    {
        get;
    }

    internal int StatementCount
    {
        get;
    }

    internal int TokenCount
    {
        get;
    }
}

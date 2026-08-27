using System;

namespace ComplexityAnalysis.Analyzers.Analysis;

[Flags]
internal enum MethodSizeMetricTargets
{
    None = 0,
    Nloc = 1,
    StatementCount = 2,
    TokenCount = 4,
}

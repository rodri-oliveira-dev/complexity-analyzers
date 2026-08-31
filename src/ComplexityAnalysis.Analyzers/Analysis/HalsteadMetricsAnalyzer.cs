using System;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class HalsteadMetricsAnalyzer
{
    internal bool TryAnalyze(
        ExecutableMember member,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out HalsteadMetrics metrics)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        bool analyzed = new HalsteadClassificationAnalyzer().TryAnalyze(
            member,
            semanticModel,
            cancellationToken,
            out HalsteadClassificationResult classification);
        if (!analyzed)
        {
            metrics = default;
            return false;
        }

        metrics = classification.Metrics;
        return true;
    }
}

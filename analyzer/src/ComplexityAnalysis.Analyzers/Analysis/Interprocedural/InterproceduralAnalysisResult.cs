using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class InterproceduralAnalysisResult
{
    private InterproceduralAnalysisResult(
        InterproceduralAnalysisResultKind kind,
        MethodComplexityTemplate? template,
        string reason)
    {
        Kind = kind;
        Template = template;
        Reason = reason;
    }

    internal InterproceduralAnalysisResultKind Kind
    {
        get;
    }

    internal MethodComplexityTemplate? Template
    {
        get;
    }

    internal string Reason
    {
        get;
    }

    internal ComplexityExpression Complexity
        => Template?.Complexity ?? ComplexityFactory.Unknown();

    internal static InterproceduralAnalysisResult Known(MethodComplexityTemplate template)
    {
        _ = template ?? throw new ArgumentNullException(nameof(template));

        return new InterproceduralAnalysisResult(
            InterproceduralAnalysisResultKind.Known,
            template,
            string.Empty);
    }

    internal static InterproceduralAnalysisResult Unknown(string reason)
    {
        return Boundary(InterproceduralAnalysisResultKind.Unknown, reason);
    }

    internal static InterproceduralAnalysisResult BudgetExceeded(string reason)
    {
        return Boundary(InterproceduralAnalysisResultKind.BudgetExceeded, reason);
    }

    internal static InterproceduralAnalysisResult CycleBoundary(string reason)
    {
        return Boundary(InterproceduralAnalysisResultKind.CycleBoundary, reason);
    }

    private static InterproceduralAnalysisResult Boundary(
        InterproceduralAnalysisResultKind kind,
        string reason)
    {
        return new InterproceduralAnalysisResult(
            kind,
            null,
            reason ?? string.Empty);
    }
}

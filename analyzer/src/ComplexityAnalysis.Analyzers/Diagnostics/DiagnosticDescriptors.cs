using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Diagnostics;

internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor EstimatedAlgorithmicComplexity = new(
        id: "BIG0001",
        title: "Estimated algorithmic complexity",
        messageFormat: "Estimated time complexity: {0}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        description: "Reports the estimated time complexity for a supported method when the estimate is known.");

    internal static readonly DiagnosticDescriptor AnalyzerExecutionProbe = new(
        id: "BIG9000",
        title: "Analyzer execution probe",
        messageFormat: "ComplexityAnalysis.Analyzers execution probe is active",
        category: "Infrastructure",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        description: "Reports once per compilation when explicitly enabled to prove the analyzer executed.",
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);
}

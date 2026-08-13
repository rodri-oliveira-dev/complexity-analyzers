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

    internal static readonly DiagnosticDescriptor LinearLookupInsideIteration = new(
        id: "BIG1001",
        title: "Linear lookup inside iteration",
        messageFormat: "Linear lookup '{0}' is executed inside an iteration estimated as {1}. Estimated combined complexity: {2}. Consider an indexed lookup when appropriate.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports known linear lookup operations executed inside an analyzable iteration.");

    internal static readonly DiagnosticDescriptor MaterializationInsideIteration = new(
        id: "BIG1002",
        title: "Materialization inside iteration",
        messageFormat: "Materialization '{0}' is executed inside an iteration estimated as {1}, repeatedly enumerating the source and allocating results. Estimated combined complexity: {2}.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports known materialization operations executed repeatedly inside an analyzable iteration.");

    internal static readonly DiagnosticDescriptor OrderingInsideIteration = new(
        id: "BIG1003",
        title: "Ordering inside iteration",
        messageFormat: "Ordering '{0}' is consumed inside an iteration estimated as {1}. Estimated combined complexity: {2}.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports known ordering operations whose deferred work is consumed inside an analyzable iteration.");

    internal static readonly DiagnosticDescriptor InputDependentCallInsideIteration = new(
        id: "BIG1004",
        title: "Input-dependent method call inside iteration",
        messageFormat: "Method '{0}' contributes {1} work inside a {2} iteration. Estimated combined complexity: {3}.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports source method calls with known input-dependent complexity executed inside an analyzable iteration.");

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

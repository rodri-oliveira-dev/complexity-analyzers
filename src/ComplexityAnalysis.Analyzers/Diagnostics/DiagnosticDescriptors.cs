using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Diagnostics;

internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor EstimatedAlgorithmicComplexity = new(
        id: "BIG0001",
        title: "Estimated algorithmic complexity",
        messageFormat: "Estimated algorithmic complexity for '{0}' is {1}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        description: "Reports the estimated time complexity for a supported method when the estimate is known.");

    internal static readonly DiagnosticDescriptor LinearLookupInsideIteration = new(
        id: "BIG1001",
        title: "Linear lookup inside iteration",
        messageFormat: "{0} performs a linear lookup with known cost {1} inside an iteration estimated as {2}. Estimated contribution: {3}.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports known linear lookup operations executed inside an analyzable iteration.");

    internal static readonly DiagnosticDescriptor MaterializationInsideIteration = new(
        id: "BIG1002",
        title: "Materialization inside iteration",
        messageFormat: "{0} materializes the sequence with known cost {1} inside an iteration estimated as {2}. Estimated contribution: {3}.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports known materialization operations executed repeatedly inside an analyzable iteration.");

    internal static readonly DiagnosticDescriptor OrderingInsideIteration = new(
        id: "BIG1003",
        title: "Ordering inside iteration",
        messageFormat: "{0} performs ordering with known consumed cost {1} inside an iteration estimated as {2}. Estimated contribution: {3}.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports known ordering operations whose deferred work is consumed inside an analyzable iteration.");

    internal static readonly DiagnosticDescriptor InputDependentCallInsideIteration = new(
        id: "BIG1004",
        title: "Input-dependent method call inside iteration",
        messageFormat: "Method '{0}' has input-dependent complexity {1} and is invoked inside an iteration estimated as {2}. Estimated contribution: {3}.",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports source method calls with known input-dependent complexity executed inside an analyzable iteration.");

    internal static readonly DiagnosticDescriptor ExponentialRecursiveGrowth = new(
        id: "BIG1005",
        title: "Exponential recursive growth",
        messageFormat: "Recursive method '{0}' exhibits exponential growth with estimated complexity {1}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports recursive methods whose supported recurrence solves to exponential time complexity.");

    internal static readonly DiagnosticDescriptor MethodComplexityExceedsConfiguredThreshold = new(
        id: "BIG1006",
        title: "Method complexity exceeds configured threshold",
        messageFormat: "Method '{0}' has estimated complexity {1}, exceeding configured maximum {2}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports methods whose known estimated time complexity exceeds the configured maximum complexity.");

    internal static readonly DiagnosticDescriptor CyclomaticComplexityExceedsConfiguredThreshold = new(
        id: "BIG2001",
        title: "Cyclomatic complexity exceeds configured threshold",
        messageFormat: "Member '{0}' has cyclomatic complexity {1}, exceeding configured maximum {2} ({3} mode)",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports supported executable members whose structural cyclomatic complexity exceeds the configured maximum.");

    internal static readonly DiagnosticDescriptor MaximumNestingDepthExceedsConfiguredThreshold = new(
        id: "BIG2002",
        title: "Maximum nesting depth exceeds configured threshold",
        messageFormat: "Member '{0}' has maximum control-flow nesting depth {1}, exceeding configured maximum {2}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports supported executable members whose maximum control-flow nesting depth exceeds the configured maximum.");

    internal static readonly DiagnosticDescriptor MethodNlocExceedsConfiguredThreshold = new(
        id: "BIG2003",
        title: "Method NLOC exceeds configured threshold",
        messageFormat: "Member '{0}' has NLOC {1}, exceeding configured maximum {2}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports supported executable members whose non-comment logical lines of code exceed the configured maximum.");

    internal static readonly DiagnosticDescriptor StatementCountExceedsConfiguredThreshold = new(
        id: "BIG2004",
        title: "Statement count exceeds configured threshold",
        messageFormat: "Member '{0}' has statement count {1}, exceeding configured maximum {2}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports supported executable members whose structural C# statement count exceeds the configured maximum.");

    internal static readonly DiagnosticDescriptor TokenCountExceedsConfiguredThreshold = new(
        id: "BIG2005",
        title: "Token count exceeds configured threshold",
        messageFormat: "Member '{0}' has token count {1}, exceeding configured maximum {2}",
        category: "Complexity",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports supported executable members whose executable-body syntax token count exceeds the configured maximum.");

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

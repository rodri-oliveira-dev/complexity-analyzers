using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ComplexityAnalysis.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComplexityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } =
        ImmutableArray.Create(DiagnosticDescriptors.AnalyzerExecutionProbe);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.AnalyzerExecutionProbe,
            Location.None));
    }
}

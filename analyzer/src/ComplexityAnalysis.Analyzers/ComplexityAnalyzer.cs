using System.Collections.Immutable;
using System.Linq;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Diagnostics;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ComplexityAnalysis.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComplexityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } =
        ImmutableArray.Create(
            DiagnosticDescriptors.EstimatedAlgorithmicComplexity,
            DiagnosticDescriptors.AnalyzerExecutionProbe);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)context.Node;
        ComplexityExpression complexity = new MethodComplexityExtractor().AnalyzeMethod(
            methodDeclaration,
            context.SemanticModel,
            context.CancellationToken);

        if (complexity is UnknownComplexity)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EstimatedAlgorithmicComplexity,
            methodDeclaration.Identifier.GetLocation(),
            complexity.ToBigONotation()));
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        SyntaxTree? syntaxTree = context.Compilation.SyntaxTrees.FirstOrDefault();
        Location location = syntaxTree is null
            ? Location.None
            : Location.Create(syntaxTree, new TextSpan(0, 0));

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.AnalyzerExecutionProbe,
            location));
    }
}

using System.Collections.Immutable;
using System.Linq;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Configuration;
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
            DiagnosticDescriptors.LinearLookupInsideIteration,
            DiagnosticDescriptors.MaterializationInsideIteration,
            DiagnosticDescriptors.OrderingInsideIteration,
            DiagnosticDescriptors.InputDependentCallInsideIteration,
            DiagnosticDescriptors.ExponentialRecursiveGrowth,
            DiagnosticDescriptors.MethodComplexityExceedsConfiguredThreshold,
            DiagnosticDescriptors.AnalyzerExecutionProbe);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(InitializeCompilationAnalysis);
    }

    private static void InitializeCompilationAnalysis(CompilationStartAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        InterproceduralAnalysisContext interproceduralContext = InterproceduralAnalysisContext.Create(
            context.Compilation,
            context.Options.AnalyzerConfigOptionsProvider,
            context.CancellationToken);

        context.RegisterCompilationEndAction(AnalyzeCompilation);
        context.RegisterSyntaxNodeAction(
            syntaxContext => AnalyzeMethodDeclaration(syntaxContext, interproceduralContext),
            SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethodDeclaration(
        SyntaxNodeAnalysisContext context,
        InterproceduralAnalysisContext interproceduralContext)
    {
        _ = interproceduralContext ?? throw new System.ArgumentNullException(nameof(interproceduralContext));

        context.CancellationToken.ThrowIfCancellationRequested();

        MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)context.Node;
        if (!ExecutableMember.TryCreateOrdinaryMethod(
            methodDeclaration,
            context.SemanticModel,
            context.CancellationToken,
            out ExecutableMember? member)
            || member is null)
        {
            return;
        }

        ComplexityAnalyzerOptions options = interproceduralContext.GetOptions(
            member.SyntaxTree,
            context.CancellationToken);

        foreach (Diagnostic diagnostic in new ActionableComplexityDiagnosticAnalyzer().AnalyzeMember(
            member,
            context.SemanticModel,
            interproceduralContext,
            options,
            context.CancellationToken))
        {
            context.ReportDiagnostic(diagnostic);
        }

        ComplexityExpression complexity = MethodComplexityExtractor.AnalyzeMember(
            member,
            context.SemanticModel,
            interproceduralContext,
            options,
            context.CancellationToken);

        if (complexity is UnknownComplexity)
        {
            return;
        }

        ReportThresholdDiagnosticIfNeeded(context, member, options, complexity);

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EstimatedAlgorithmicComplexity,
            member.DiagnosticLocation,
            CreateProperty(
                DiagnosticPropertyNames.Complexity,
                complexity.ToBigONotation()),
            member.DisplayName,
            complexity.ToBigONotation()));
    }

    private static void ReportThresholdDiagnosticIfNeeded(
        SyntaxNodeAnalysisContext context,
        ExecutableMember member,
        ComplexityAnalyzerOptions options,
        ComplexityExpression actualComplexity)
    {
        if (!options.MaximumComplexity.TryCreateExpression(out ComplexityExpression thresholdComplexity))
        {
            return;
        }

        if (ComplexityGrowthComparer.Compare(actualComplexity, thresholdComplexity) != GrowthComparison.Greater)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MethodComplexityExceedsConfiguredThreshold,
            member.DiagnosticLocation,
            ImmutableDictionary<string, string?>.Empty
                .Add(DiagnosticPropertyNames.Complexity, actualComplexity.ToBigONotation())
                .Add(DiagnosticPropertyNames.Threshold, thresholdComplexity.ToBigONotation()),
            member.DisplayName,
            actualComplexity.ToBigONotation(),
            thresholdComplexity.ToBigONotation()));
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
            location,
            CreateProperty(
                DiagnosticPropertyNames.DiagnosticRole,
                "execution-probe")));
    }

    private static ImmutableDictionary<string, string?> CreateProperty(
        string key,
        string value)
    {
        return ImmutableDictionary<string, string?>.Empty.Add(key, value);
    }
}

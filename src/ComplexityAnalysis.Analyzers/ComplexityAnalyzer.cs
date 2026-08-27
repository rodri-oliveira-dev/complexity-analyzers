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
            DiagnosticDescriptors.CyclomaticComplexityExceedsConfiguredThreshold,
            DiagnosticDescriptors.MaximumNestingDepthExceedsConfiguredThreshold,
            DiagnosticDescriptors.MethodNlocExceedsConfiguredThreshold,
            DiagnosticDescriptors.StatementCountExceedsConfiguredThreshold,
            DiagnosticDescriptors.TokenCountExceedsConfiguredThreshold,
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
            syntaxContext => AnalyzeExecutableMember(syntaxContext, interproceduralContext),
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.GetAccessorDeclaration,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.InitAccessorDeclaration,
            SyntaxKind.AddAccessorDeclaration,
            SyntaxKind.RemoveAccessorDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression,
            SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeExecutableMember(
        SyntaxNodeAnalysisContext context,
        InterproceduralAnalysisContext interproceduralContext)
    {
        _ = interproceduralContext ?? throw new System.ArgumentNullException(nameof(interproceduralContext));

        context.CancellationToken.ThrowIfCancellationRequested();

        if (!ExecutableMember.TryCreate(
            context.Node,
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

        ReportCyclomaticThresholdDiagnosticIfNeeded(context, member, options);
        ReportMaximumNestingDepthThresholdDiagnosticIfNeeded(context, member, options);
        ReportMethodSizeThresholdDiagnosticsIfNeeded(context, member, options);

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

    private static void ReportCyclomaticThresholdDiagnosticIfNeeded(
        SyntaxNodeAnalysisContext context,
        ExecutableMember member,
        ComplexityAnalyzerOptions options)
    {
        if (!options.MaximumCyclomaticComplexity.HasValue)
        {
            return;
        }

        if (!new CyclomaticComplexityAnalyzer().TryAnalyze(
            member,
            options.CyclomaticComplexityMode,
            context.CancellationToken,
            out CyclomaticComplexityResult result))
        {
            return;
        }

        int threshold = options.MaximumCyclomaticComplexity.Value;
        if (result.Value <= threshold)
        {
            return;
        }

        string actualText = result.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string thresholdText = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string modeText = FormatCyclomaticComplexityMode(options.CyclomaticComplexityMode);

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CyclomaticComplexityExceedsConfiguredThreshold,
            member.DiagnosticLocation,
            ImmutableDictionary<string, string?>.Empty
                .Add(DiagnosticPropertyNames.CyclomaticComplexity, actualText)
                .Add(DiagnosticPropertyNames.Threshold, thresholdText)
                .Add(DiagnosticPropertyNames.CyclomaticComplexityMode, modeText),
            member.DisplayName,
            actualText,
            thresholdText,
            modeText));
    }

    private static void ReportMaximumNestingDepthThresholdDiagnosticIfNeeded(
        SyntaxNodeAnalysisContext context,
        ExecutableMember member,
        ComplexityAnalyzerOptions options)
    {
        if (!options.MaximumNestingDepth.HasValue)
        {
            return;
        }

        if (!new MaximumNestingDepthAnalyzer().TryAnalyze(
            member,
            context.CancellationToken,
            out MaximumNestingDepthResult result))
        {
            return;
        }

        int threshold = options.MaximumNestingDepth.Value;
        if (result.Value <= threshold)
        {
            return;
        }

        string actualText = result.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string thresholdText = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture);

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MaximumNestingDepthExceedsConfiguredThreshold,
            member.DiagnosticLocation,
            ImmutableDictionary<string, string?>.Empty
                .Add(DiagnosticPropertyNames.MaximumNestingDepth, actualText)
                .Add(DiagnosticPropertyNames.Threshold, thresholdText),
            member.DisplayName,
            actualText,
            thresholdText));
    }

    private static void ReportMethodSizeThresholdDiagnosticsIfNeeded(
        SyntaxNodeAnalysisContext context,
        ExecutableMember member,
        ComplexityAnalyzerOptions options)
    {
        if (!options.MaximumMethodNloc.HasValue
            && !options.MaximumStatementCount.HasValue
            && !options.MaximumTokenCount.HasValue)
        {
            return;
        }

        if (!new MethodSizeMetricsAnalyzer().TryAnalyze(
            member,
            CreateMethodSizeMetricTargets(options),
            context.CancellationToken,
            out MethodSizeMetricsResult result))
        {
            return;
        }

        ReportIntegerThresholdDiagnosticIfNeeded(
            context,
            member,
            DiagnosticDescriptors.MethodNlocExceedsConfiguredThreshold,
            DiagnosticPropertyNames.MethodNloc,
            result.Nloc,
            options.MaximumMethodNloc);
        ReportIntegerThresholdDiagnosticIfNeeded(
            context,
            member,
            DiagnosticDescriptors.StatementCountExceedsConfiguredThreshold,
            DiagnosticPropertyNames.StatementCount,
            result.StatementCount,
            options.MaximumStatementCount);
        ReportIntegerThresholdDiagnosticIfNeeded(
            context,
            member,
            DiagnosticDescriptors.TokenCountExceedsConfiguredThreshold,
            DiagnosticPropertyNames.TokenCount,
            result.TokenCount,
            options.MaximumTokenCount);
    }

    private static void ReportIntegerThresholdDiagnosticIfNeeded(
        SyntaxNodeAnalysisContext context,
        ExecutableMember member,
        DiagnosticDescriptor descriptor,
        string propertyName,
        int actual,
        int? threshold)
    {
        if (!threshold.HasValue || actual <= threshold.Value)
        {
            return;
        }

        string actualText = actual.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string thresholdText = threshold.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor,
            member.DiagnosticLocation,
            ImmutableDictionary<string, string?>.Empty
                .Add(propertyName, actualText)
                .Add(DiagnosticPropertyNames.Threshold, thresholdText),
            member.DisplayName,
            actualText,
            thresholdText));
    }

    private static MethodSizeMetricTargets CreateMethodSizeMetricTargets(ComplexityAnalyzerOptions options)
    {
        MethodSizeMetricTargets targets = MethodSizeMetricTargets.None;
        if (options.MaximumMethodNloc.HasValue)
        {
            targets |= MethodSizeMetricTargets.Nloc;
        }

        if (options.MaximumStatementCount.HasValue)
        {
            targets |= MethodSizeMetricTargets.StatementCount;
        }

        if (options.MaximumTokenCount.HasValue)
        {
            targets |= MethodSizeMetricTargets.TokenCount;
        }

        return targets;
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

    private static string FormatCyclomaticComplexityMode(CyclomaticComplexityAnalysisMode mode)
    {
        return mode == CyclomaticComplexityAnalysisMode.ModifiedMcCabe
            ? "modified_mccabe"
            : "standard";
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

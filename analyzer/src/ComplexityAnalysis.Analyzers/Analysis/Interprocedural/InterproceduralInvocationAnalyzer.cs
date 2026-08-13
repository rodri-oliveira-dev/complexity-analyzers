using System;
using System.Collections.Immutable;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class InterproceduralInvocationAnalyzer
{
    private readonly MethodAnalysisContext callerContext;

    internal InterproceduralInvocationAnalyzer(MethodAnalysisContext callerContext)
    {
        this.callerContext = callerContext ?? throw new ArgumentNullException(nameof(callerContext));
    }

    internal ComplexityExpression AnalyzeInvocation(InvocationExpressionSyntax invocation)
    {
        _ = invocation ?? throw new ArgumentNullException(nameof(invocation));

        callerContext.CancellationToken.ThrowIfCancellationRequested();

        InterproceduralAnalysisContext? interproceduralContext = callerContext.InterproceduralContext;
        InterproceduralRootAnalysisState? rootState = callerContext.InterproceduralRootState;
        if (interproceduralContext is null || rootState is null)
        {
            return new KnownOperationComplexityAnalyzer(callerContext).AnalyzeInvocation(invocation);
        }

        CallTargetResolution resolution = interproceduralContext.SourceMethodResolver.Resolve(
            invocation,
            callerContext.SemanticModel,
            callerContext.CancellationToken);

        if (resolution.Kind == CallTargetResolutionKind.KnownOperation)
        {
            return AnalyzeKnownOperation(invocation, resolution);
        }

        return resolution.Kind == CallTargetResolutionKind.SourceMethod
            ? AnalyzeSourceMethodInvocation(
                invocation,
                resolution,
                interproceduralContext,
                rootState)
            : ComplexityFactory.Unknown();
    }

    private ComplexityExpression AnalyzeKnownOperation(
        InvocationExpressionSyntax invocation,
        CallTargetResolution resolution)
    {
        return resolution.TargetMethodSymbol is not null
            && resolution.KnownOperationMapping is not null
            ? new KnownOperationComplexityAnalyzer(callerContext).AnalyzeInvocation(
                invocation,
                resolution.TargetMethodSymbol,
                resolution.KnownOperationMapping)
            : ComplexityFactory.Unknown();
    }

    private ComplexityExpression AnalyzeSourceMethodInvocation(
        InvocationExpressionSyntax invocation,
        CallTargetResolution resolution,
        InterproceduralAnalysisContext interproceduralContext,
        InterproceduralRootAnalysisState rootState)
    {
        callerContext.CancellationToken.ThrowIfCancellationRequested();

        if (resolution.TargetMethodSymbol is null
            || resolution.SourceMethodDefinition is null
            || resolution.SourceMethodDeclaration is null)
        {
            return ComplexityFactory.Unknown();
        }

        IMethodSymbol sourceMethodDefinition = resolution.SourceMethodDefinition;
        if (!rootState.TryEnterMethod(
            sourceMethodDefinition,
            out InterproceduralRootAnalysisState calleeState,
            out InterproceduralAnalysisResult boundaryResult))
        {
            return boundaryResult.Complexity;
        }

        InterproceduralAnalysisResult calleeResult = GetOrAnalyzeCallee(
            sourceMethodDefinition,
            resolution.SourceMethodDeclaration,
            interproceduralContext,
            calleeState);

        return SubstituteCallSiteArguments(
            invocation,
            resolution.TargetMethodSymbol,
            calleeResult);
    }

    private InterproceduralAnalysisResult GetOrAnalyzeCallee(
        IMethodSymbol sourceMethodDefinition,
        MethodDeclarationSyntax sourceMethodDeclaration,
        InterproceduralAnalysisContext interproceduralContext,
        InterproceduralRootAnalysisState calleeState)
    {
        callerContext.CancellationToken.ThrowIfCancellationRequested();

        if (interproceduralContext.TemplateCache.TryGetCompleted(
            sourceMethodDefinition,
            callerContext.CancellationToken,
            out InterproceduralAnalysisResult cachedResult))
        {
            return cachedResult;
        }

        if (!interproceduralContext.TemplateCache.TryReserveAnalysis(
            sourceMethodDefinition,
            callerContext.CancellationToken,
            out InterproceduralAnalysisResult? completedResult))
        {
            return completedResult
                ?? InterproceduralAnalysisResult.Unknown("The source method is already being analyzed.");
        }

        bool stored = false;
        try
        {
            SemanticModel calleeSemanticModel = interproceduralContext.Compilation.GetSemanticModel(
                sourceMethodDeclaration.SyntaxTree);
            InterproceduralAnalysisResult analyzedResult = new MethodComplexityExtractor()
                .AnalyzeSourceMethod(
                    sourceMethodDeclaration,
                    sourceMethodDefinition,
                    calleeSemanticModel,
                    interproceduralContext,
                    calleeState,
                    callerContext.CancellationToken);

            interproceduralContext.TemplateCache.StoreCompleted(
                sourceMethodDefinition,
                analyzedResult,
                callerContext.CancellationToken);
            stored = true;
            return analyzedResult;
        }
        finally
        {
            if (!stored)
            {
                _ = interproceduralContext.TemplateCache.AbandonAnalysis(
                    sourceMethodDefinition,
                    CancellationToken.None);
            }
        }
    }

    private ComplexityExpression SubstituteCallSiteArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol targetMethodSymbol,
        InterproceduralAnalysisResult calleeResult)
    {
        callerContext.CancellationToken.ThrowIfCancellationRequested();

        if (calleeResult.Kind != InterproceduralAnalysisResultKind.Known
            || calleeResult.Template is null)
        {
            return ComplexityFactory.Unknown();
        }

        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings =
            new ArgumentComplexityBinder().Bind(
                invocation,
                targetMethodSymbol,
                callerContext,
                callerContext.SemanticModel,
                callerContext.CancellationToken);

        return ComplexitySubstitution.Substitute(
            calleeResult.Template.Complexity,
            bindings,
            callerContext.CancellationToken);
    }
}

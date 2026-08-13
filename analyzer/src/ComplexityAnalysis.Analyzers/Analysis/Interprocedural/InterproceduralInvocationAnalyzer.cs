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
        if (rootState.ContainsActiveMethod(sourceMethodDefinition))
        {
            return InterproceduralAnalysisResult
                .CycleBoundary("The method is already active in the current root call path.")
                .Complexity;
        }

        if (interproceduralContext.TemplateCache.TryGetCompleted(
            sourceMethodDefinition,
            callerContext.CancellationToken,
            out InterproceduralAnalysisResult cachedResult))
        {
            return SubstituteCallSiteArguments(
                invocation,
                resolution.TargetMethodSymbol,
                cachedResult);
        }

        if (!rootState.TryEnterMethod(
            sourceMethodDefinition,
            out InterproceduralRootAnalysisState calleeState,
            out InterproceduralAnalysisResult boundaryResult))
        {
            return boundaryResult.Complexity;
        }

        InterproceduralAnalysisResult calleeResult;
        try
        {
            calleeResult = GetOrAnalyzeCallee(
                sourceMethodDefinition,
                resolution.SourceMethodDeclaration,
                interproceduralContext,
                calleeState);
        }
        finally
        {
            _ = calleeState.ExitMethod(sourceMethodDefinition);
        }

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

        if (!interproceduralContext.TemplateCache.TryReserveAnalysis(
            sourceMethodDefinition,
            callerContext.CancellationToken,
            out InterproceduralAnalysisResult? completedResult))
        {
            return completedResult
                ?? AnalyzeCalleeWithoutCaching(
                    sourceMethodDefinition,
                    sourceMethodDeclaration,
                    interproceduralContext,
                    calleeState);
        }

        bool stored = false;
        try
        {
            InterproceduralAnalysisResult analyzedResult = AnalyzeCalleeWithoutCaching(
                sourceMethodDefinition,
                sourceMethodDeclaration,
                interproceduralContext,
                calleeState);

            if (IsCacheable(analyzedResult))
            {
                interproceduralContext.TemplateCache.StoreCompleted(
                    sourceMethodDefinition,
                    analyzedResult,
                    callerContext.CancellationToken);
                stored = true;
            }

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

    private InterproceduralAnalysisResult AnalyzeCalleeWithoutCaching(
        IMethodSymbol sourceMethodDefinition,
        MethodDeclarationSyntax sourceMethodDeclaration,
        InterproceduralAnalysisContext interproceduralContext,
        InterproceduralRootAnalysisState calleeState)
    {
        callerContext.CancellationToken.ThrowIfCancellationRequested();

        SemanticModel calleeSemanticModel = interproceduralContext.Compilation.GetSemanticModel(
            sourceMethodDeclaration.SyntaxTree);
        return new MethodComplexityExtractor()
            .AnalyzeSourceMethod(
                sourceMethodDeclaration,
                sourceMethodDefinition,
                calleeSemanticModel,
                interproceduralContext,
                calleeState,
                callerContext.CancellationToken);
    }

    private static bool IsCacheable(InterproceduralAnalysisResult result)
    {
        return result.Kind == InterproceduralAnalysisResultKind.Known;
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

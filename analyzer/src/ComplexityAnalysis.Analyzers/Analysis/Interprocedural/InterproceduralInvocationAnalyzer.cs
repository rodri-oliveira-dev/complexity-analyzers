using System;
using System.Collections.Immutable;
using System.Threading;

using ComplexityAnalysis.Analyzers.Configuration;
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

        if (callerContext.TreatsDirectRecursiveInvocationsAsConstant
            && IsDirectRecursiveInvocation(invocation))
        {
            return ComplexityFactory.Constant();
        }

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

        return resolution.Kind switch
        {
            CallTargetResolutionKind.KnownOperation => AnalyzeKnownOperation(invocation, resolution),
            CallTargetResolutionKind.SourceMethod when callerContext.Options.InterproceduralAnalysisEnabled =>
                AnalyzeSourceMethodInvocation(
                    invocation,
                    resolution,
                    interproceduralContext,
                    rootState),
            CallTargetResolutionKind.SourceMethod or CallTargetResolutionKind.Unsupported => ComplexityFactory.Unknown(),
            _ => ComplexityFactory.Unknown(),
        };
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
        ComplexityAnalyzerOptions calleeOptions = interproceduralContext.GetAnalysisOptions(
            resolution.SourceMethodDeclaration.SyntaxTree,
            rootState.Budget,
            callerContext.CancellationToken);
        if (rootState.ContainsActiveMethod(sourceMethodDefinition))
        {
            return InterproceduralAnalysisResult
                .CycleBoundary("The method is already active in the current root call path.")
                .Complexity;
        }

        if (interproceduralContext.TemplateCache.TryGetCompleted(
            sourceMethodDefinition,
            calleeOptions,
            callerContext.CancellationToken,
            out InterproceduralAnalysisResult cachedResult))
        {
            return SubstituteCallSiteArguments(
                invocation,
                resolution.TargetMethodSymbol,
                sourceMethodDefinition,
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
                calleeOptions,
                calleeState);
        }
        finally
        {
            _ = calleeState.ExitMethod(sourceMethodDefinition);
        }

        return SubstituteCallSiteArguments(
            invocation,
            resolution.TargetMethodSymbol,
            sourceMethodDefinition,
            calleeResult);
    }

    private InterproceduralAnalysisResult GetOrAnalyzeCallee(
        IMethodSymbol sourceMethodDefinition,
        MethodDeclarationSyntax sourceMethodDeclaration,
        InterproceduralAnalysisContext interproceduralContext,
        ComplexityAnalyzerOptions calleeOptions,
        InterproceduralRootAnalysisState calleeState)
    {
        callerContext.CancellationToken.ThrowIfCancellationRequested();

        if (!interproceduralContext.TemplateCache.TryReserveAnalysis(
            sourceMethodDefinition,
            calleeOptions,
            callerContext.CancellationToken,
            out InterproceduralAnalysisResult? completedResult))
        {
            return completedResult
                ?? AnalyzeCalleeWithoutCaching(
                    sourceMethodDefinition,
                    sourceMethodDeclaration,
                    interproceduralContext,
                    calleeOptions,
                    calleeState);
        }

        bool stored = false;
        try
        {
            InterproceduralAnalysisResult analyzedResult = AnalyzeCalleeWithoutCaching(
                sourceMethodDefinition,
                sourceMethodDeclaration,
                interproceduralContext,
                calleeOptions,
                calleeState);

            if (IsCacheable(analyzedResult))
            {
                interproceduralContext.TemplateCache.StoreCompleted(
                    sourceMethodDefinition,
                    calleeOptions,
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
                    calleeOptions,
                    CancellationToken.None);
            }
        }
    }

    private InterproceduralAnalysisResult AnalyzeCalleeWithoutCaching(
        IMethodSymbol sourceMethodDefinition,
        MethodDeclarationSyntax sourceMethodDeclaration,
        InterproceduralAnalysisContext interproceduralContext,
        ComplexityAnalyzerOptions calleeOptions,
        InterproceduralRootAnalysisState calleeState)
    {
        callerContext.CancellationToken.ThrowIfCancellationRequested();

        SemanticModel calleeSemanticModel = interproceduralContext.GetSemanticModel(
            sourceMethodDeclaration.SyntaxTree,
            callerContext.CancellationToken);
        return new MethodComplexityExtractor()
            .AnalyzeSourceMethod(
                sourceMethodDeclaration,
                sourceMethodDefinition,
                calleeSemanticModel,
                interproceduralContext,
                calleeState,
                calleeOptions,
                callerContext.CancellationToken);
    }

    private static bool IsCacheable(InterproceduralAnalysisResult result)
    {
        return result.Kind == InterproceduralAnalysisResultKind.Known;
    }

    private ComplexityExpression SubstituteCallSiteArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol targetMethodSymbol,
        IMethodSymbol sourceMethodDefinition,
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
                sourceMethodDefinition,
                calleeResult.Template,
                callerContext,
                callerContext.CancellationToken);

        ComplexityExpression callSiteEvaluationComplexity = AnalyzeCallSiteEvaluation(
            invocation,
            targetMethodSymbol);
        if (callSiteEvaluationComplexity is UnknownComplexity)
        {
            return callSiteEvaluationComplexity;
        }

        ComplexityExpression substitutedCalleeComplexity = ComplexitySubstitution.Substitute(
            calleeResult.Template.Complexity,
            bindings,
            callerContext.CancellationToken);

        return ComplexityComposer.Sequential(
            callSiteEvaluationComplexity,
            substitutedCalleeComplexity);
    }

    private bool IsDirectRecursiveInvocation(InvocationExpressionSyntax invocation)
    {
        SymbolInfo symbolInfo = callerContext.SemanticModel.GetSymbolInfo(
            invocation,
            callerContext.CancellationToken);
        return symbolInfo.Symbol is IMethodSymbol methodSymbol
            && SymbolEqualityComparer.Default.Equals(
                GetMethodDefinition(methodSymbol),
                GetMethodDefinition(callerContext.MethodSymbol));
    }

    private static IMethodSymbol GetMethodDefinition(IMethodSymbol methodSymbol)
    {
        return (methodSymbol.ReducedFrom ?? methodSymbol).OriginalDefinition;
    }

    private ComplexityExpression AnalyzeCallSiteEvaluation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol targetMethodSymbol)
    {
        ComplexityExpression complexity = AnalyzeReceiverEvaluation(
            invocation,
            targetMethodSymbol);
        if (complexity is UnknownComplexity)
        {
            return complexity;
        }

        BasicOperationAnalyzer operationAnalyzer = new(callerContext);
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            callerContext.CancellationToken.ThrowIfCancellationRequested();

            ComplexityExpression argumentComplexity = argument.Expression is LambdaExpressionSyntax
                ? ComplexityFactory.Constant()
                : operationAnalyzer.AnalyzeExpression(argument.Expression);
            complexity = ComplexityComposer.Sequential(complexity, argumentComplexity);
            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
    }

    private ComplexityExpression AnalyzeReceiverEvaluation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol targetMethodSymbol)
    {
        callerContext.CancellationToken.ThrowIfCancellationRequested();

        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && (!targetMethodSymbol.IsStatic || targetMethodSymbol.ReducedFrom is not null)
                ? memberAccess.Expression is ThisExpressionSyntax or BaseExpressionSyntax
                    ? ComplexityFactory.Constant()
                    : new BasicOperationAnalyzer(callerContext).AnalyzeExpression(memberAccess.Expression)
                : ComplexityFactory.Constant();
    }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Diagnostics;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class ActionableComplexityDiagnosticAnalyzer
{
    private static readonly KnownOperationResolver Resolver = new(KnownOperationRegistry.Default);

    internal ImmutableArray<Diagnostic> AnalyzeMethod(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = interproceduralContext ?? throw new ArgumentNullException(nameof(interproceduralContext));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        return ExecutableMember.TryCreateOrdinaryMethod(
            methodDeclaration,
            semanticModel,
            cancellationToken,
            out ExecutableMember? member)
            && member is not null
            ? AnalyzeMember(member, semanticModel, interproceduralContext, options, cancellationToken)
            : throw new InvalidOperationException("The method declaration must resolve to a method symbol.");
    }

    internal ImmutableArray<Diagnostic> AnalyzeMember(
        ExecutableMember member,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = interproceduralContext ?? throw new ArgumentNullException(nameof(interproceduralContext));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        InterproceduralRootAnalysisState rootState = InterproceduralAnalysisContext.CreateRootState(
            member.Symbol,
            options,
            cancellationToken);
        MethodAnalysisContext context = MethodAnalysisContext.Create(
            semanticModel,
            member.Symbol,
            options.WithAnalysisBudget(rootState.Budget),
            interproceduralContext,
            rootState,
            cancellationToken);
        ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        AnalyzeRecursiveMember(member, context, diagnostics);

        foreach (InvocationExpressionSyntax invocation in ExecutableMemberSyntax.DescendantNodesInOwnBody<InvocationExpressionSyntax>(member))
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnalyzeInvocation(invocation, member, context, diagnostics);
        }

        return diagnostics.ToImmutable();
    }

    private static void AnalyzeRecursiveMember(
        ExecutableMember member,
        MethodAnalysisContext context,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (MethodComplexityExtractor.TrySolveDirectRecurrence(
            member,
            context,
            out ComplexityExpression? complexity)
            && complexity is ExponentialComplexity)
        {
            string formattedComplexity = complexity.ToBigONotation();

            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.ExponentialRecursiveGrowth,
                member.DiagnosticLocation,
                ImmutableDictionary<string, string?>.Empty
                    .Add(DiagnosticPropertyNames.Complexity, formattedComplexity)
                    .Add(DiagnosticPropertyNames.RecurrenceClass, "exponential"),
                FormatOperation(context.MethodSymbol),
                formattedComplexity));
        }
    }

    private static void AnalyzeInvocation(
        InvocationExpressionSyntax invocation,
        ExecutableMember member,
        MethodAnalysisContext context,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (!TryGetContainingIterationComplexity(invocation, member, context, out ComplexityExpression iterationComplexity))
        {
            return;
        }

        CallTargetResolution resolution = context.InterproceduralContext?.SourceMethodResolver.Resolve(
            invocation,
            context.SemanticModel,
            context.CancellationToken)
            ?? CallTargetResolution.Unsupported();
        if (resolution.Kind == CallTargetResolutionKind.KnownOperation)
        {
            AnalyzeKnownOperationInvocation(invocation, context, iterationComplexity, resolution, diagnostics);
            return;
        }

        if (resolution.Kind == CallTargetResolutionKind.SourceMethod
            && context.Options.InterproceduralAnalysisEnabled)
        {
            AnalyzeSourceMethodInvocation(invocation, context, iterationComplexity, resolution, diagnostics);
        }
    }

    private static void AnalyzeKnownOperationInvocation(
        InvocationExpressionSyntax invocation,
        MethodAnalysisContext context,
        ComplexityExpression iterationComplexity,
        CallTargetResolution resolution,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        IMethodSymbol? methodSymbol = resolution.TargetMethodSymbol;
        KnownOperationMapping? mapping = resolution.KnownOperationMapping;
        if (methodSymbol is null || mapping is null)
        {
            return;
        }

        KnownOperationComplexityAnalyzer operationAnalyzer = new(context);
        ComplexityExpression invocationComplexity = operationAnalyzer.AnalyzeInvocation(
            invocation,
            methodSymbol,
            mapping);
        if (invocationComplexity is UnknownComplexity)
        {
            return;
        }

        if (IsActionableLinearLookup(mapping, invocationComplexity))
        {
            ReportLinearLookup(invocation, methodSymbol, iterationComplexity, invocationComplexity, diagnostics);
            return;
        }

        if (IsActionableMaterialization(mapping))
        {
            ReportMaterialization(invocation, methodSymbol, iterationComplexity, invocationComplexity, diagnostics);
            return;
        }

        if (IsActionableOrdering(
            invocation,
            mapping,
            context,
            out InvocationExpressionSyntax? consumerInvocation,
            out IMethodSymbol? consumerMethodSymbol,
            out KnownOperationMapping? consumerMapping)
            && consumerInvocation is not null
            && consumerMethodSymbol is not null
            && consumerMapping is not null)
        {
            ComplexityExpression consumedComplexity = operationAnalyzer.AnalyzeInvocation(
                consumerInvocation,
                consumerMethodSymbol,
                consumerMapping);
            if (consumedComplexity is not UnknownComplexity)
            {
                ReportOrdering(invocation, methodSymbol, iterationComplexity, consumedComplexity, diagnostics);
            }
        }
    }

    private static void AnalyzeSourceMethodInvocation(
        InvocationExpressionSyntax invocation,
        MethodAnalysisContext context,
        ComplexityExpression iterationComplexity,
        CallTargetResolution resolution,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (resolution.TargetMethodSymbol is null)
        {
            return;
        }

        ComplexityExpression invocationComplexity = new InterproceduralInvocationAnalyzer(context)
            .AnalyzeInvocation(invocation);
        if (invocationComplexity is UnknownComplexity or ConstantComplexity)
        {
            return;
        }

        ReportInputDependentCall(
            invocation,
            resolution.TargetMethodSymbol,
            iterationComplexity,
            invocationComplexity,
            diagnostics);
    }

    private static bool IsActionableLinearLookup(
        KnownOperationMapping mapping,
        ComplexityExpression invocationComplexity)
    {
        return mapping.Metadata.IsLookupOperation
            && mapping.Metadata.EnumeratesReceiver
            && mapping.ExecutionKind == KnownOperationExecutionKind.Immediate
            && invocationComplexity is not ConstantComplexity;
    }

    private static bool IsActionableMaterialization(KnownOperationMapping mapping)
    {
        return mapping.Metadata.Materializes
            && mapping.ExecutionKind == KnownOperationExecutionKind.Immediate;
    }

    private static bool IsActionableOrdering(
        InvocationExpressionSyntax invocation,
        KnownOperationMapping mapping,
        MethodAnalysisContext context,
        out InvocationExpressionSyntax? consumerInvocation,
        out IMethodSymbol? consumerMethodSymbol,
        out KnownOperationMapping? consumerMapping)
    {
        consumerInvocation = null;
        consumerMethodSymbol = null;
        consumerMapping = null;

        if (!mapping.Metadata.Orders
            || mapping.ExecutionKind != KnownOperationExecutionKind.Deferred
            || ContainsNestedOrderingInvocation(invocation, context))
        {
            return false;
        }

        foreach (InvocationExpressionSyntax ancestor in invocation.Ancestors().OfType<InvocationExpressionSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!KnownOperationInvocation.TryResolve(ancestor, context, Resolver, out IMethodSymbol? ancestorSymbol, out KnownOperationMapping? ancestorMapping)
                || ancestorSymbol is null
                || ancestorMapping is null
                || ancestorMapping.ExecutionKind != KnownOperationExecutionKind.Immediate
                || !ancestorMapping.Metadata.EnumeratesReceiver
                || !KnownOperationInvocation.TryGetReceiverExpression(ancestor, ancestorSymbol, out ExpressionSyntax? receiver)
                || receiver is null
                || !receiver.Span.Contains(invocation.Span))
            {
                continue;
            }

            consumerInvocation = ancestor;
            consumerMethodSymbol = ancestorSymbol;
            consumerMapping = ancestorMapping;
            return true;
        }

        return false;
    }

    private static bool ContainsNestedOrderingInvocation(
        InvocationExpressionSyntax invocation,
        MethodAnalysisContext context)
    {
        foreach (InvocationExpressionSyntax descendant
            in ExecutableMemberSyntax.DescendantNodesAndSelfExcludingNestedExecutableBodies<InvocationExpressionSyntax>(invocation))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (ReferenceEquals(descendant, invocation))
            {
                continue;
            }

            if (KnownOperationInvocation.TryResolve(descendant, context, Resolver, out _, out KnownOperationMapping? mapping)
                && mapping is not null
                && mapping.Metadata.Orders
                && mapping.ExecutionKind == KnownOperationExecutionKind.Deferred)
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportLinearLookup(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        ComplexityExpression iterationComplexity,
        ComplexityExpression invocationComplexity,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ComplexityExpression combinedComplexity = ComplexityComposer.Nested(
            iterationComplexity,
            invocationComplexity);
        string operation = FormatOperation(methodSymbol);
        string operationComplexityText = invocationComplexity.ToBigONotation();
        string iterationComplexityText = iterationComplexity.ToBigONotation();
        string combinedComplexityText = combinedComplexity.ToBigONotation();

        diagnostics.Add(Diagnostic.Create(
            DiagnosticDescriptors.LinearLookupInsideIteration,
            invocation.GetLocation(),
            CreateNestedOperationProperties(
                operation,
                operationComplexityText,
                iterationComplexityText,
                combinedComplexityText),
            operation,
            operationComplexityText,
            iterationComplexityText,
            combinedComplexityText));
    }

    private static void ReportMaterialization(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        ComplexityExpression iterationComplexity,
        ComplexityExpression invocationComplexity,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ComplexityExpression combinedComplexity = ComplexityComposer.Nested(
            iterationComplexity,
            invocationComplexity);
        string operation = FormatOperation(methodSymbol);
        string operationComplexityText = invocationComplexity.ToBigONotation();
        string iterationComplexityText = iterationComplexity.ToBigONotation();
        string combinedComplexityText = combinedComplexity.ToBigONotation();

        diagnostics.Add(Diagnostic.Create(
            DiagnosticDescriptors.MaterializationInsideIteration,
            invocation.GetLocation(),
            CreateNestedOperationProperties(
                operation,
                operationComplexityText,
                iterationComplexityText,
                combinedComplexityText),
            operation,
            operationComplexityText,
            iterationComplexityText,
            combinedComplexityText));
    }

    private static void ReportOrdering(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        ComplexityExpression iterationComplexity,
        ComplexityExpression consumedComplexity,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ComplexityExpression combinedComplexity = ComplexityComposer.Nested(
            iterationComplexity,
            consumedComplexity);
        string operation = FormatOperation(methodSymbol);
        string operationComplexityText = consumedComplexity.ToBigONotation();
        string iterationComplexityText = iterationComplexity.ToBigONotation();
        string combinedComplexityText = combinedComplexity.ToBigONotation();

        diagnostics.Add(Diagnostic.Create(
            DiagnosticDescriptors.OrderingInsideIteration,
            invocation.GetLocation(),
            CreateNestedOperationProperties(
                operation,
                operationComplexityText,
                iterationComplexityText,
                combinedComplexityText),
            operation,
            operationComplexityText,
            iterationComplexityText,
            combinedComplexityText));
    }

    private static void ReportInputDependentCall(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        ComplexityExpression iterationComplexity,
        ComplexityExpression invocationComplexity,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ComplexityExpression combinedComplexity = ComplexityComposer.Nested(
            iterationComplexity,
            invocationComplexity);
        string operation = FormatOperation(methodSymbol);
        string operationComplexityText = invocationComplexity.ToBigONotation();
        string iterationComplexityText = iterationComplexity.ToBigONotation();
        string combinedComplexityText = combinedComplexity.ToBigONotation();

        diagnostics.Add(Diagnostic.Create(
            DiagnosticDescriptors.InputDependentCallInsideIteration,
            invocation.GetLocation(),
            CreateNestedOperationProperties(
                operation,
                operationComplexityText,
                iterationComplexityText,
                combinedComplexityText),
            operation,
            operationComplexityText,
            iterationComplexityText,
            combinedComplexityText));
    }

    private static ImmutableDictionary<string, string?> CreateNestedOperationProperties(
        string operation,
        string operationComplexity,
        string iterationComplexity,
        string combinedComplexity)
    {
        return ImmutableDictionary<string, string?>.Empty
            .Add(DiagnosticPropertyNames.Operation, operation)
            .Add(DiagnosticPropertyNames.OperationComplexity, operationComplexity)
            .Add(DiagnosticPropertyNames.IterationComplexity, iterationComplexity)
            .Add(DiagnosticPropertyNames.CombinedComplexity, combinedComplexity);
    }

    private static bool TryGetContainingIterationComplexity(
        InvocationExpressionSyntax invocation,
        ExecutableMember member,
        MethodAnalysisContext context,
        out ComplexityExpression iterationComplexity)
    {
        iterationComplexity = ComplexityFactory.Constant();
        bool foundIteration = false;

        foreach (SyntaxNode ancestor in invocation.Ancestors())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!member.Body.Contains(ancestor.Span)
                || !TryGetLoopBody(ancestor, out StatementSyntax? body)
                || body is null
                || !body.Span.Contains(invocation.Span))
            {
                continue;
            }

            if (!TryAnalyzeLoopIteration(ancestor, context, out ComplexityExpression? loopIterationComplexity)
                || loopIterationComplexity is null)
            {
                iterationComplexity = ComplexityFactory.Unknown();
                return false;
            }

            iterationComplexity = ComplexityComposer.Nested(
                iterationComplexity,
                loopIterationComplexity);
            foundIteration = true;
        }

        return foundIteration
            && iterationComplexity is not UnknownComplexity;
    }

    private static bool TryAnalyzeLoopIteration(
        SyntaxNode loop,
        MethodAnalysisContext context,
        out ComplexityExpression? iterationComplexity)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        LoopBoundAnalyzer loopBoundAnalyzer = new(context);
        LoopBoundAnalysisResult result = loop switch
        {
            ForStatementSyntax forStatement => loopBoundAnalyzer.AnalyzeFor(forStatement),
            ForEachStatementSyntax forEachStatement => loopBoundAnalyzer.AnalyzeForEach(forEachStatement),
            WhileStatementSyntax whileStatement => loopBoundAnalyzer.AnalyzeWhile(whileStatement),
            DoStatementSyntax doStatement => loopBoundAnalyzer.AnalyzeDoWhile(doStatement),
            _ => LoopBoundAnalysisResult.Unknown(),
        };

        iterationComplexity = result.IsAnalyzable
            ? result.IterationComplexity
            : null;
        return result.IsAnalyzable;
    }

    private static bool TryGetLoopBody(SyntaxNode node, out StatementSyntax? body)
    {
        body = node switch
        {
            ForStatementSyntax forStatement => forStatement.Statement,
            ForEachStatementSyntax forEachStatement => forEachStatement.Statement,
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            DoStatementSyntax doStatement => doStatement.Statement,
            _ => null,
        };

        return body is not null;
    }

    private static string FormatOperation(IMethodSymbol methodSymbol)
    {
        IMethodSymbol definition = methodSymbol.ReducedFrom?.OriginalDefinition
            ?? methodSymbol.OriginalDefinition;
        INamedTypeSymbol containingType = definition.ContainingType;

        return FormatTypeName(containingType)
            + "."
            + definition.Name;
    }

    private static string FormatTypeName(INamedTypeSymbol type)
    {
        return type.TypeParameters.Length == 0
            ? type.Name
            : type.Name
                + "<"
                + string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name))
                + ">";
    }
}

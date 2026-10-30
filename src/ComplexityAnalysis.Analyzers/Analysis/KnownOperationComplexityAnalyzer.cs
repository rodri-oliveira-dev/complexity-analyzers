using System;
using System.Collections.Immutable;
using System.Linq;

using ComplexityAnalysis.Analyzers.Analysis.Evidence;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class KnownOperationComplexityAnalyzer
{
    private static readonly KnownOperationResolver Resolver = new(KnownOperationRegistry.Default);

    private readonly MethodAnalysisContext context;

    internal KnownOperationComplexityAnalyzer(MethodAnalysisContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    internal ComplexityExpression AnalyzeInvocation(InvocationExpressionSyntax invocation)
    {
        _ = invocation ?? throw new ArgumentNullException(nameof(invocation));

        context.CancellationToken.ThrowIfCancellationRequested();

        if (!KnownOperationInvocation.TryResolve(invocation, context, Resolver, out IMethodSymbol? methodSymbol, out KnownOperationMapping? mapping)
            || methodSymbol is null
            || mapping is null)
        {
            RecordUnknownOperation(invocation, ComplexityUnknownReason.UnresolvedSymbol);
            return ComplexityFactory.Unknown();
        }

        return AnalyzeInvocation(invocation, methodSymbol, mapping);
    }

    internal ComplexityExpression AnalyzeInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        KnownOperationMapping mapping)
    {
        _ = invocation ?? throw new ArgumentNullException(nameof(invocation));
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = mapping ?? throw new ArgumentNullException(nameof(mapping));

        context.CancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression complexity = mapping.ExecutionKind == KnownOperationExecutionKind.Deferred
            ? ComplexityFactory.Constant()
            : IsLinqMapping(mapping)
            ? AnalyzeImmediateLinqInvocation(invocation, methodSymbol, mapping)
            : AnalyzeImmediateKnownInvocation(invocation, methodSymbol, mapping);

        RecordKnownOperation(invocation, mapping, complexity);
        return complexity;
    }

    internal ComplexityExpression AnalyzeMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        _ = memberAccess ?? throw new ArgumentNullException(nameof(memberAccess));

        context.CancellationToken.ThrowIfCancellationRequested();

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is not IPropertySymbol propertySymbol
            || !Resolver.TryResolve(propertySymbol, context.CancellationToken, out KnownOperationMapping mapping))
        {
            return ComplexityFactory.Unknown();
        }

        if (!mapping.Metadata.EnumeratesReceiver)
        {
            return mapping.Complexity;
        }

        return TryResolveInputDimension(memberAccess.Expression, out ComplexityVariable? variable)
            && variable is not null
            ? SubstituteVariable(mapping.Complexity, variable)
            : ComplexityFactory.Unknown();
    }

    internal ComplexityExpression AnalyzeElementAccess(ElementAccessExpressionSyntax elementAccess)
    {
        _ = elementAccess ?? throw new ArgumentNullException(nameof(elementAccess));

        context.CancellationToken.ThrowIfCancellationRequested();

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(elementAccess, context.CancellationToken).Symbol;
        if (symbol is not IPropertySymbol propertySymbol
            || !Resolver.TryResolve(propertySymbol, context.CancellationToken, out KnownOperationMapping mapping))
        {
            return ComplexityFactory.Unknown();
        }

        ComplexityExpression argumentComplexity = AnalyzeArguments(elementAccess.ArgumentList.Arguments);
        if (argumentComplexity is UnknownComplexity)
        {
            return argumentComplexity;
        }

        ComplexityExpression operationComplexity = !mapping.Metadata.EnumeratesReceiver
            ? mapping.Complexity
            : TryResolveInputDimension(elementAccess.Expression, out ComplexityVariable? variable) && variable is not null
                ? SubstituteVariable(mapping.Complexity, variable)
                : ComplexityFactory.Unknown();

        return ComplexityComposer.Sequential(argumentComplexity, operationComplexity);
    }

    internal bool TryAnalyzeForEachSequence(
        ExpressionSyntax expression,
        out LoopBoundAnalysisResult result)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (TryAnalyzeDeferredPipeline(expression, out SequenceEnumerationPlan plan)
            && !plan.IsUnknown)
        {
            result = LoopBoundAnalysisResult.Enumerable(
                plan.ElementIterationComplexity,
                plan.TotalEnumerationComplexity);
            return true;
        }

        result = LoopBoundAnalysisResult.Unknown();
        return false;
    }

    internal bool TryResolveInputDimension(ExpressionSyntax expression, out ComplexityVariable? variable)
    {
        return TryResolveInputDimension(expression, includeDeferredPipeline: true, out variable);
    }

    private bool TryResolveInputDimension(
        ExpressionSyntax expression,
        bool includeDeferredPipeline,
        out ComplexityVariable? variable)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        variable = null;

        if (expression is IdentifierNameSyntax)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken);
            if (symbolInfo.Symbol is not null
                && context.TryGetInputSizeVariable(symbolInfo.Symbol, out ComplexityVariable directVariable))
            {
                variable = directVariable;
                return true;
            }

            if (symbolInfo.Symbol is not null
                && context.TryGetLocalLoopBound(symbolInfo.Symbol, out LoopBoundExpression localBound)
                && localBound.IsVariable)
            {
                variable = localBound.Variable;
                return true;
            }

            return false;
        }

        if (expression is MemberAccessExpressionSyntax memberAccess
            && StringComparer.Ordinal.Equals(memberAccess.Name.Identifier.ValueText, "Length")
            && TryResolveInputDimension(memberAccess.Expression, out ComplexityVariable? receiverVariable)
            && IsArrayOrString(memberAccess.Expression))
        {
            variable = receiverVariable;
            return true;
        }

        if (includeDeferredPipeline
            && TryAnalyzeDeferredPipeline(expression, out SequenceEnumerationPlan plan)
            && !plan.IsUnknown
            && plan.PrimaryVariable is not null)
        {
            variable = plan.PrimaryVariable;
            return true;
        }

        return false;
    }

    private ComplexityExpression AnalyzeImmediateKnownInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        KnownOperationMapping mapping)
    {
        ComplexityExpression argumentComplexity = AnalyzeNonLambdaArguments(invocation, methodSymbol);
        if (argumentComplexity is UnknownComplexity)
        {
            return argumentComplexity;
        }

        if (!mapping.Metadata.EnumeratesReceiver)
        {
            return ComplexityComposer.Sequential(argumentComplexity, mapping.Complexity);
        }

        return KnownOperationInvocation.TryGetReceiverExpression(invocation, methodSymbol, out ExpressionSyntax? receiver)
            && receiver is not null
            && TryResolveInputDimension(receiver, out ComplexityVariable? variable)
            && variable is not null
            ? ComplexityComposer.Sequential(argumentComplexity, SubstituteVariable(mapping.Complexity, variable))
            : ComplexityFactory.Unknown();
    }

    private ComplexityExpression AnalyzeImmediateLinqInvocation(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        KnownOperationMapping mapping)
    {
        if (!KnownOperationInvocation.TryGetReceiverExpression(invocation, methodSymbol, out ExpressionSyntax? source)
            || source is null)
        {
            return ComplexityFactory.Unknown();
        }

        ComplexityExpression argumentComplexity = AnalyzeNonLambdaArguments(invocation, methodSymbol);
        if (argumentComplexity is UnknownComplexity)
        {
            return argumentComplexity;
        }

        ComplexityExpression delegateComplexity = AnalyzeInvocationDelegateArguments(
            invocation,
            methodSymbol,
            mapping);
        if (delegateComplexity is UnknownComplexity)
        {
            return delegateComplexity;
        }

        ComplexityExpression operationComplexity = GetImmediateLinqOperationComplexity(
            source,
            mapping,
            HasDelegateArgument(invocation, methodSymbol));

        if (operationComplexity is UnknownComplexity)
        {
            return operationComplexity;
        }

        if (delegateComplexity is not ConstantComplexity
            && TryAnalyzeSequenceEnumeration(source, out SequenceEnumerationPlan plan)
            && !plan.IsUnknown)
        {
            operationComplexity = ComplexityComposer.Sequential(
                operationComplexity,
                ComplexityComposer.Nested(plan.ElementIterationComplexity, delegateComplexity));
        }

        return ComplexityComposer.Sequential(argumentComplexity, operationComplexity);
    }

    private ComplexityExpression GetImmediateLinqOperationComplexity(
        ExpressionSyntax source,
        KnownOperationMapping mapping,
        bool hasLambdaArgument)
    {
        string operationFamily = mapping.Metadata.OperationFamily;

        if (TryAnalyzeDeferredPipeline(source, out SequenceEnumerationPlan pipelinePlan))
        {
            return pipelinePlan.IsUnknown
                ? ComplexityFactory.Unknown()
                : pipelinePlan.TotalEnumerationComplexity;
        }

        if (StringComparer.Ordinal.Equals(operationFamily, "linq-count")
            && !hasLambdaArgument
            && IsCheapCountSource(source))
        {
            return ComplexityFactory.Constant();
        }

        if (StringComparer.Ordinal.Equals(operationFamily, "linq-any")
            && !hasLambdaArgument)
        {
            return ComplexityFactory.Constant();
        }

        return TryResolveInputDimension(source, out ComplexityVariable? variable)
            && variable is not null
            ? SubstituteVariable(mapping.Complexity, variable)
            : ComplexityFactory.Unknown();
    }

    private bool TryAnalyzeSequenceEnumeration(
        ExpressionSyntax expression,
        out SequenceEnumerationPlan plan)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (TryAnalyzeDeferredPipeline(expression, out plan))
        {
            return true;
        }

        if (TryResolveDirectInputDimension(expression, out ComplexityVariable? variable)
            && variable is not null)
        {
            plan = SequenceEnumerationPlan.Linear(variable);
            return true;
        }

        plan = SequenceEnumerationPlan.Unknown();
        return false;
    }

    private bool TryAnalyzeDeferredPipeline(
        ExpressionSyntax expression,
        out SequenceEnumerationPlan plan)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        plan = SequenceEnumerationPlan.Unknown();

        if (TryAnalyzeLocalPipeline(expression, out plan, out bool isLocalReference))
        {
            return true;
        }

        if (isLocalReference
            || !TryResolveDeferredInvocation(
                expression,
                out InvocationExpressionSyntax? invocation,
                out IMethodSymbol? methodSymbol,
                out KnownOperationMapping? mapping,
                out ExpressionSyntax? source))
        {
            return false;
        }

        if (!TryAnalyzeSequenceEnumeration(source, out SequenceEnumerationPlan sourcePlan)
            || sourcePlan.IsUnknown)
        {
            plan = SequenceEnumerationPlan.Unknown();
            return true;
        }

        ComplexityExpression delegateComplexity = AnalyzeInvocationDelegateArguments(
            invocation,
            methodSymbol,
            mapping);
        if (delegateComplexity is UnknownComplexity)
        {
            plan = SequenceEnumerationPlan.Unknown();
            return true;
        }

        plan = CreateDeferredPipelinePlan(invocation, mapping, sourcePlan, delegateComplexity);
        return true;
    }

    private bool TryAnalyzeLocalPipeline(
        ExpressionSyntax expression,
        out SequenceEnumerationPlan plan,
        out bool isLocalReference)
    {
        plan = SequenceEnumerationPlan.Unknown();
        isLocalReference = TryGetLocalInitializer(
            expression,
            out ExpressionSyntax? localInitializer,
            out _)
            && localInitializer is not null;

        if (!isLocalReference)
        {
            return false;
        }

        if (TryResolveStableLocalInitializer(expression, out ExpressionSyntax? stableInitializer)
            && stableInitializer is not null)
        {
            return TryAnalyzeDeferredPipeline(stableInitializer, out plan);
        }

        return IsDeferredPipelineExpression(localInitializer!);
    }

    private bool TryResolveDeferredInvocation(
        ExpressionSyntax expression,
        out InvocationExpressionSyntax invocation,
        out IMethodSymbol methodSymbol,
        out KnownOperationMapping mapping,
        out ExpressionSyntax source)
    {
        invocation = null!;
        methodSymbol = null!;
        mapping = null!;
        source = null!;

        if (expression is not InvocationExpressionSyntax candidate)
        {
            return false;
        }

        if (!KnownOperationInvocation.TryResolve(
                candidate,
                context,
                Resolver,
                out IMethodSymbol? resolvedMethod,
                out KnownOperationMapping? resolvedMapping)
            || resolvedMethod is null
            || resolvedMapping is null
            || resolvedMapping.ExecutionKind != KnownOperationExecutionKind.Deferred
            || !KnownOperationInvocation.TryGetReceiverExpression(
                candidate,
                resolvedMethod,
                out ExpressionSyntax? resolvedSource)
            || resolvedSource is null)
        {
            return false;
        }

        invocation = candidate;
        methodSymbol = resolvedMethod;
        mapping = resolvedMapping;
        source = resolvedSource;
        return true;
    }

    private SequenceEnumerationPlan CreateDeferredPipelinePlan(
        InvocationExpressionSyntax invocation,
        KnownOperationMapping mapping,
        SequenceEnumerationPlan sourcePlan,
        ComplexityExpression delegateComplexity)
    {
        ComplexityExpression total = sourcePlan.TotalEnumerationComplexity;
        ComplexityExpression elementIteration = sourcePlan.ElementIterationComplexity;
        string operationFamily = mapping.Metadata.OperationFamily;

        if (IsOrderingOperation(operationFamily))
        {
            total = ComplexityComposer.Sequential(
                total,
                SubstituteVariable(mapping.Complexity, sourcePlan.PrimaryVariable));
        }
        else if (StringComparer.Ordinal.Equals(operationFamily, "linq-select-many"))
        {
            if (!TryResolveSelectManyInnerDimension(invocation, out ComplexityVariable? innerVariable)
                || innerVariable is null)
            {
                return SequenceEnumerationPlan.Unknown();
            }

            elementIteration = ComplexityComposer.Nested(
                sourcePlan.ElementIterationComplexity,
                ComplexityFactory.Linear(innerVariable));
            total = elementIteration;
        }
        else
        {
            total = ComplexityComposer.Sequential(
                total,
                SubstituteVariable(mapping.Complexity, sourcePlan.PrimaryVariable));
        }

        if (delegateComplexity is not ConstantComplexity)
        {
            total = ComplexityComposer.Sequential(
                total,
                ComplexityComposer.Nested(sourcePlan.ElementIterationComplexity, delegateComplexity));
        }

        return new SequenceEnumerationPlan(
            isUnknown: false,
            sourcePlan.PrimaryVariable,
            elementIteration,
            total);
    }

    private static bool IsOrderingOperation(string operationFamily)
    {
        return operationFamily is "linq-orderby"
            or "linq-orderby-descending"
            or "linq-thenby"
            or "linq-thenby-descending";
    }

    private bool TryResolveSelectManyInnerDimension(
        InvocationExpressionSyntax invocation,
        out ComplexityVariable? variable)
    {
        variable = null;

        ArgumentSyntax? argument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (argument?.Expression is not LambdaExpressionSyntax lambda)
        {
            return false;
        }

        ExpressionSyntax? bodyExpression = lambda.Body as ExpressionSyntax;
        return bodyExpression is not null
            && TryResolveDirectInputDimension(bodyExpression, out variable);
    }

    private ComplexityExpression AnalyzeInvocationDelegateArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        KnownOperationMapping mapping)
    {
        ComplexityExpression complexity = ComplexityFactory.Constant();
        bool ignoreFirstLambdaForSelectMany = StringComparer.Ordinal.Equals(
            mapping.Metadata.OperationFamily,
            "linq-select-many");

        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!IsDelegateArgument(argument, methodSymbol))
            {
                continue;
            }

            if (ignoreFirstLambdaForSelectMany)
            {
                ignoreFirstLambdaForSelectMany = false;
                continue;
            }

            complexity = ComplexityComposer.Sequential(
                complexity,
                AnalyzeDelegateExpression(argument.Expression));
            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
    }

    private ComplexityExpression AnalyzeDelegateExpression(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        if (expression is LambdaExpressionSyntax lambda)
        {
            return AnalyzeLambda(lambda);
        }

        if (expression is AnonymousMethodExpressionSyntax anonymousMethod)
        {
            return AnalyzeDelegateBlock(anonymousMethod.Block);
        }

        if (TryResolveStableLocalInitializer(expression, out ExpressionSyntax? initializer)
            && initializer is not null)
        {
            return AnalyzeDelegateExpression(initializer);
        }

        SourceMethodResolver resolver = new();
        if (!resolver.TryResolveDelegateTarget(
                expression,
                context.SemanticModel,
                context.CancellationToken,
                out ExecutableMember? target)
            || target is null
            || target.SyntaxTree != context.SemanticModel.SyntaxTree
            || HasUnstableDelegateTargetState(expression, target))
        {
            return ComplexityFactory.Unknown();
        }

        return AnalyzeDelegateTarget(target);
    }

    private ComplexityExpression AnalyzeDelegateTarget(ExecutableMember target)
    {
        InterproceduralAnalysisContext? interproceduralContext = context.InterproceduralContext;
        InterproceduralRootAnalysisState? rootState = context.InterproceduralRootState;
        if (interproceduralContext is null
            || rootState is null
            || rootState.ContainsActiveMethod(target.Symbol)
            || !rootState.TryEnterMethod(
                target.Symbol,
                out InterproceduralRootAnalysisState calleeState,
                out _))
        {
            return ComplexityFactory.Unknown();
        }

        MethodAnalysisContext targetContext = context.WithDelegateTarget(target.Symbol, calleeState);
        ComplexityEvidenceCollector.Scope? evidenceScope = context.EvidenceCollector?.EnterScope(target.Declaration);
        try
        {
            return target.Body.Expression is not null
                ? new BasicOperationAnalyzer(targetContext).AnalyzeExpression(target.Body.Expression)
                : target.Body.Block is not null
                    ? AnalyzeDelegateBlock(target.Body.Block, targetContext)
                    : ComplexityFactory.Unknown();
        }
        finally
        {
            evidenceScope?.Dispose();
        }
    }

    private bool HasUnstableDelegateTargetState(
        ExpressionSyntax targetExpression,
        ExecutableMember target)
    {
        SyntaxNode? executableRoot = targetExpression.Ancestors().FirstOrDefault(IsExecutableRoot);
        SyntaxNode? targetBody = target.Body.Expression ?? (SyntaxNode?)target.Body.Block;
        if (executableRoot is null || targetBody is null)
        {
            return true;
        }

        foreach (IdentifierNameSyntax reference
            in targetBody.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            ISymbol? symbol = context.SemanticModel.GetSymbolInfo(
                reference,
                context.CancellationToken).Symbol;
            if (symbol is not (ILocalSymbol or IParameterSymbol)
                || SymbolEqualityComparer.Default.Equals(symbol.ContainingSymbol, target.Symbol)
                || IsDeclaredWithin(symbol, target.Declaration))
            {
                continue;
            }

            if (HasWriteBetween(
                executableRoot,
                symbol,
                GetStabilityStart(symbol, executableRoot),
                executableRoot.Span.End))
            {
                return true;
            }
        }

        return false;
    }

    private ComplexityExpression AnalyzeLambda(LambdaExpressionSyntax lambda)
    {
        return lambda.Body switch
        {
            ExpressionSyntax expression => new BasicOperationAnalyzer(context).AnalyzeExpression(expression),
            BlockSyntax block => AnalyzeLambdaBlock(block),
            _ => ComplexityFactory.Unknown(),
        };
    }

    private ComplexityExpression AnalyzeLambdaBlock(BlockSyntax block)
    {
        return AnalyzeDelegateBlock(block);
    }

    private ComplexityExpression AnalyzeDelegateBlock(BlockSyntax block)
    {
        return AnalyzeDelegateBlock(block, context);
    }

    private static ComplexityExpression AnalyzeDelegateBlock(
        BlockSyntax block,
        MethodAnalysisContext analysisContext)
    {
        if (block.Statements.Count != 1
            || block.Statements[0] is not ReturnStatementSyntax { Expression: { } expression })
        {
            return ComplexityFactory.Unknown();
        }

        return new BasicOperationAnalyzer(analysisContext).AnalyzeExpression(expression);
    }

    private ComplexityExpression AnalyzeNonLambdaArguments(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol)
    {
        ComplexityExpression complexity = ComplexityFactory.Constant();
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (IsDelegateArgument(argument, methodSymbol))
            {
                continue;
            }

            complexity = ComplexityComposer.Sequential(
                complexity,
                new BasicOperationAnalyzer(context).AnalyzeExpression(argument.Expression));
            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
    }

    private bool IsDelegateArgument(ArgumentSyntax argument, IMethodSymbol? methodSymbol)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (context.SemanticModel.GetOperation(argument, context.CancellationToken)
            is IArgumentOperation { Parameter.Type.TypeKind: TypeKind.Delegate })
        {
            return true;
        }

        return methodSymbol is not null
            && argument.Expression is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax;
    }

    private bool TryResolveStableLocalInitializer(
        ExpressionSyntax expression,
        out ExpressionSyntax? initializer)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        initializer = null;
        if (!TryGetLocalInitializer(
                expression,
                out ExpressionSyntax? value,
                out VariableDeclaratorSyntax? declarator)
            || value is null
            || declarator is null
            || context.SemanticModel.GetSymbolInfo(
                UnwrapParentheses(expression),
                context.CancellationToken).Symbol is not ILocalSymbol localSymbol)
        {
            return false;
        }

        SyntaxNode? executableRoot = declarator.Ancestors().FirstOrDefault(IsExecutableRoot);
        if (executableRoot is null)
        {
            return false;
        }

        if (HasWriteBetween(
                executableRoot,
                localSymbol,
                declarator.Span.End,
                executableRoot.Span.End)
            || HasUnstableCapturedState(
                value,
                executableRoot,
                executableRoot.Span.End))
        {
            return false;
        }

        initializer = value;
        return true;
    }

    private bool TryGetLocalInitializer(
        ExpressionSyntax expression,
        out ExpressionSyntax? initializer,
        out VariableDeclaratorSyntax? declarator)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        initializer = null;
        declarator = null;
        if (expression is not IdentifierNameSyntax
            || context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol
                is not ILocalSymbol { DeclaringSyntaxReferences.Length: 1 } localSymbol
            || localSymbol.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)
                is not VariableDeclaratorSyntax { Initializer.Value: { } value } declaration
            || declaration.SyntaxTree != expression.SyntaxTree
            || declaration.SpanStart >= expression.SpanStart)
        {
            return false;
        }

        initializer = value;
        declarator = declaration;
        return true;
    }

    private bool IsDeferredPipelineExpression(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        if (expression is InvocationExpressionSyntax invocation
            && KnownOperationInvocation.TryResolve(
                invocation,
                context,
                Resolver,
                out _,
                out KnownOperationMapping? mapping)
            && mapping?.ExecutionKind == KnownOperationExecutionKind.Deferred)
        {
            return true;
        }

        return TryGetLocalInitializer(expression, out ExpressionSyntax? initializer, out _)
            && initializer is not null
            && IsDeferredPipelineExpression(initializer);
    }

    private bool HasUnstableCapturedState(
        ExpressionSyntax initializer,
        SyntaxNode executableRoot,
        int observationPosition)
    {
        foreach (AnonymousFunctionExpressionSyntax anonymousFunction
            in initializer.DescendantNodesAndSelf().OfType<AnonymousFunctionExpressionSyntax>())
        {
            foreach (IdentifierNameSyntax reference
                in anonymousFunction.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                ISymbol? symbol = context.SemanticModel.GetSymbolInfo(
                    reference,
                    context.CancellationToken).Symbol;
                if (symbol is not (ILocalSymbol or IParameterSymbol)
                    || IsDeclaredWithin(symbol, anonymousFunction))
                {
                    continue;
                }

                int stabilityStart = GetStabilityStart(symbol, executableRoot);
                if (HasWriteBetween(
                    executableRoot,
                    symbol,
                    stabilityStart,
                    observationPosition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasWriteBetween(
        SyntaxNode executableRoot,
        ISymbol symbol,
        int startPosition,
        int endPosition)
    {
        foreach (IdentifierNameSyntax reference
            in executableRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (reference.SpanStart <= startPosition
                || reference.SpanStart >= endPosition
                || !IsWriteReference(reference)
                || !SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(reference, context.CancellationToken).Symbol,
                    symbol))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsDeclaredWithin(ISymbol symbol, SyntaxNode node)
    {
        return symbol.DeclaringSyntaxReferences.Any(reference =>
            reference.SyntaxTree == node.SyntaxTree
            && node.Span.Contains(reference.Span));
    }

    private static int GetStabilityStart(ISymbol symbol, SyntaxNode executableRoot)
    {
        SyntaxReference? declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault(reference =>
            reference.SyntaxTree == executableRoot.SyntaxTree
            && executableRoot.Span.Contains(reference.Span));
        return declaration?.Span.End ?? executableRoot.SpanStart;
    }

    private static bool IsExecutableRoot(SyntaxNode node)
    {
        return node is BaseMethodDeclarationSyntax
            or LocalFunctionStatementSyntax
            or LambdaExpressionSyntax
            or AnonymousMethodExpressionSyntax
            or AccessorDeclarationSyntax;
    }

    private static bool IsWriteReference(IdentifierNameSyntax reference)
    {
        foreach (SyntaxNode ancestor in reference.Ancestors())
        {
            if (ancestor is AssignmentExpressionSyntax assignment)
            {
                return assignment.Left.Span.Contains(reference.Span);
            }

            if (ancestor is PrefixUnaryExpressionSyntax prefix)
            {
                return prefix.IsKind(SyntaxKind.PreIncrementExpression)
                    || prefix.IsKind(SyntaxKind.PreDecrementExpression);
            }

            if (ancestor is PostfixUnaryExpressionSyntax postfix)
            {
                return postfix.IsKind(SyntaxKind.PostIncrementExpression)
                    || postfix.IsKind(SyntaxKind.PostDecrementExpression);
            }

            if (ancestor is ArgumentSyntax argument)
            {
                return argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword)
                    || argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword);
            }

            if (ancestor is StatementSyntax or LambdaExpressionSyntax or AnonymousMethodExpressionSyntax)
            {
                break;
            }
        }

        return false;
    }

    private ComplexityExpression AnalyzeArguments(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        ComplexityExpression complexity = ComplexityFactory.Constant();
        foreach (ArgumentSyntax argument in arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            complexity = ComplexityComposer.Sequential(
                complexity,
                new BasicOperationAnalyzer(context).AnalyzeExpression(argument.Expression));
            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
    }

    private bool HasDelegateArgument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol)
    {
        return invocation.ArgumentList.Arguments.Any(argument => IsDelegateArgument(argument, methodSymbol));
    }

    private bool TryResolveDirectInputDimension(ExpressionSyntax expression, out ComplexityVariable? variable)
    {
        return TryResolveInputDimension(expression, includeDeferredPipeline: false, out variable);
    }

    private bool IsCheapCountSource(ExpressionSyntax source)
    {
        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(source, context.CancellationToken).Type;
        return type is not null && IsCheapCountType(type);
    }

    private bool IsCheapCountType(ITypeSymbol type)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (type.TypeKind == TypeKind.Array)
        {
            return true;
        }

        if (type is INamedTypeSymbol namedType && IsCollectionCountType(namedType))
        {
            return true;
        }

        foreach (INamedTypeSymbol @interface in type.AllInterfaces)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (IsCollectionCountType(@interface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCollectionCountType(INamedTypeSymbol type)
    {
        INamedTypeSymbol definition = type.OriginalDefinition;
        return HasMetadataName(definition, "System.Collections", "ICollection")
            || HasMetadataName(definition, "System.Collections.Generic", "ICollection`1");
    }

    private bool IsArrayOrString(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
        return type?.TypeKind == TypeKind.Array
            || type?.SpecialType == SpecialType.System_String;
    }

    private static bool IsLinqMapping(KnownOperationMapping mapping)
    {
        return StringComparer.Ordinal.Equals(
            mapping.Identity.ContainingType.NamespaceName,
            "System.Linq");
    }

    private static ComplexityExpression SubstituteVariable(
        ComplexityExpression expression,
        ComplexityVariable? variable)
    {
        if (variable is null)
        {
            return ComplexityFactory.Unknown();
        }

        return expression switch
        {
            ConstantComplexity => ComplexityFactory.Constant(),
            UnknownComplexity => ComplexityFactory.Unknown(),
            PolynomialLogComplexity polynomialLog => new PolynomialLogComplexity(
                variable,
                polynomialLog.PolynomialDegree,
                polynomialLog.LogExponent),
            CompositeComplexity composite => new CompositeComplexity(
                SubstituteVariable(composite.Left, variable),
                composite.Operation,
                SubstituteVariable(composite.Right, variable)),
            _ => ComplexityFactory.Unknown(),
        };
    }

    private static bool HasMetadataName(INamedTypeSymbol type, string namespaceName, string metadataName)
    {
        return StringComparer.Ordinal.Equals(type.MetadataName, metadataName)
            && StringComparer.Ordinal.Equals(GetNamespaceName(type.ContainingNamespace), namespaceName);
    }

    private static string GetNamespaceName(INamespaceSymbol namespaceSymbol)
    {
        return namespaceSymbol.IsGlobalNamespace
            ? string.Empty
            : namespaceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private void RecordKnownOperation(
        InvocationExpressionSyntax invocation,
        KnownOperationMapping mapping,
        ComplexityExpression complexity)
    {
        ComplexityEvidenceCollector? collector = context.EvidenceCollector;
        if (collector is null)
        {
            return;
        }

        collector.Record(
            ComplexityEvidenceKind.KnownOperation,
            ComplexityEvidenceOrigin.KnownOperationRegistry,
            mapping.Metadata.OperationFamily,
            invocation,
            mapping.Estimate.WithComplexity(complexity),
            composition: null,
            complexity is UnknownComplexity
                ? ComplexityUnknownReason.UnresolvedInputDimension
                : ComplexityUnknownReason.None,
            context.CancellationToken);
    }

    private void RecordUnknownOperation(
        InvocationExpressionSyntax invocation,
        ComplexityUnknownReason reason)
    {
        context.EvidenceCollector?.Record(
            ComplexityEvidenceKind.KnownOperation,
            ComplexityEvidenceOrigin.KnownOperationRegistry,
            "unresolved-operation",
            invocation,
            ComplexityEstimate.Unknown(),
            composition: null,
            reason,
            context.CancellationToken);
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private readonly struct SequenceEnumerationPlan
    {
        internal SequenceEnumerationPlan(
            bool isUnknown,
            ComplexityVariable? primaryVariable,
            ComplexityExpression elementIterationComplexity,
            ComplexityExpression totalEnumerationComplexity)
        {
            IsUnknown = isUnknown;
            PrimaryVariable = primaryVariable;
            ElementIterationComplexity = elementIterationComplexity;
            TotalEnumerationComplexity = totalEnumerationComplexity;
        }

        internal bool IsUnknown
        {
            get;
        }

        internal ComplexityVariable? PrimaryVariable
        {
            get;
        }

        internal ComplexityExpression ElementIterationComplexity
        {
            get;
        }

        internal ComplexityExpression TotalEnumerationComplexity
        {
            get;
        }

        internal static SequenceEnumerationPlan Linear(ComplexityVariable variable)
        {
            ComplexityExpression linear = ComplexityFactory.Linear(variable);
            return new SequenceEnumerationPlan(
                isUnknown: false,
                variable,
                linear,
                linear);
        }

        internal static SequenceEnumerationPlan Unknown()
        {
            return new SequenceEnumerationPlan(
                isUnknown: true,
                null,
                ComplexityFactory.Unknown(),
                ComplexityFactory.Unknown());
        }
    }
}

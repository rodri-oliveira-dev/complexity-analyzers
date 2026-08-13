using System;
using System.Collections.Immutable;
using System.Linq;

using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

        if (!TryResolveInvocation(invocation, out IMethodSymbol? methodSymbol, out KnownOperationMapping? mapping)
            || methodSymbol is null
            || mapping is null)
        {
            return ComplexityFactory.Unknown();
        }

        if (mapping.ExecutionKind == KnownOperationExecutionKind.Deferred)
        {
            return ComplexityFactory.Constant();
        }

        return IsLinqMapping(mapping)
            ? AnalyzeImmediateLinqInvocation(invocation, methodSymbol, mapping)
            : AnalyzeImmediateKnownInvocation(invocation, methodSymbol, mapping);
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

        if (TryAnalyzeDeferredPipeline(expression, out SequenceEnumerationPlan plan)
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
        ComplexityExpression argumentComplexity = AnalyzeNonLambdaArguments(invocation);
        if (argumentComplexity is UnknownComplexity)
        {
            return argumentComplexity;
        }

        if (!mapping.Metadata.EnumeratesReceiver)
        {
            return ComplexityComposer.Sequential(argumentComplexity, mapping.Complexity);
        }

        return TryGetReceiverExpression(invocation, methodSymbol, out ExpressionSyntax? receiver)
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
        if (!TryGetReceiverExpression(invocation, methodSymbol, out ExpressionSyntax? source)
            || source is null)
        {
            return ComplexityFactory.Unknown();
        }

        ComplexityExpression argumentComplexity = AnalyzeNonLambdaArguments(invocation);
        if (argumentComplexity is UnknownComplexity)
        {
            return argumentComplexity;
        }

        ComplexityExpression lambdaComplexity = AnalyzeInvocationLambdaArguments(invocation, mapping);
        if (lambdaComplexity is UnknownComplexity)
        {
            return lambdaComplexity;
        }

        ComplexityExpression operationComplexity = GetImmediateLinqOperationComplexity(
            source,
            mapping,
            HasLambdaArgument(invocation));

        if (operationComplexity is UnknownComplexity)
        {
            return operationComplexity;
        }

        if (lambdaComplexity is not ConstantComplexity
            && TryAnalyzeSequenceEnumeration(source, out SequenceEnumerationPlan plan)
            && !plan.IsUnknown)
        {
            operationComplexity = ComplexityComposer.Sequential(
                operationComplexity,
                ComplexityComposer.Nested(plan.ElementIterationComplexity, lambdaComplexity));
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

        if (expression is not InvocationExpressionSyntax invocation
            || !TryResolveInvocation(invocation, out IMethodSymbol? methodSymbol, out KnownOperationMapping? mapping)
            || methodSymbol is null
            || mapping is null
            || mapping.ExecutionKind != KnownOperationExecutionKind.Deferred
            || !TryGetReceiverExpression(invocation, methodSymbol, out ExpressionSyntax? source)
            || source is null)
        {
            return false;
        }

        if (!TryAnalyzeSequenceEnumeration(source, out SequenceEnumerationPlan sourcePlan)
            || sourcePlan.IsUnknown)
        {
            plan = SequenceEnumerationPlan.Unknown();
            return true;
        }

        ComplexityExpression lambdaComplexity = AnalyzeInvocationLambdaArguments(invocation, mapping);
        if (lambdaComplexity is UnknownComplexity)
        {
            plan = SequenceEnumerationPlan.Unknown();
            return true;
        }

        ComplexityExpression total = sourcePlan.TotalEnumerationComplexity;
        ComplexityExpression elementIteration = sourcePlan.ElementIterationComplexity;
        string operationFamily = mapping.Metadata.OperationFamily;

        if (StringComparer.Ordinal.Equals(operationFamily, "linq-orderby")
            || StringComparer.Ordinal.Equals(operationFamily, "linq-orderby-descending")
            || StringComparer.Ordinal.Equals(operationFamily, "linq-thenby")
            || StringComparer.Ordinal.Equals(operationFamily, "linq-thenby-descending"))
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
                plan = SequenceEnumerationPlan.Unknown();
                return true;
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

        if (lambdaComplexity is not ConstantComplexity)
        {
            total = ComplexityComposer.Sequential(
                total,
                ComplexityComposer.Nested(sourcePlan.ElementIterationComplexity, lambdaComplexity));
        }

        plan = new SequenceEnumerationPlan(
            isUnknown: false,
            sourcePlan.PrimaryVariable,
            elementIteration,
            total);
        return true;
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

    private ComplexityExpression AnalyzeInvocationLambdaArguments(
        InvocationExpressionSyntax invocation,
        KnownOperationMapping mapping)
    {
        ComplexityExpression complexity = ComplexityFactory.Constant();
        bool ignoreFirstLambdaForSelectMany = StringComparer.Ordinal.Equals(
            mapping.Metadata.OperationFamily,
            "linq-select-many");

        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (argument.Expression is not LambdaExpressionSyntax lambda)
            {
                continue;
            }

            if (ignoreFirstLambdaForSelectMany)
            {
                ignoreFirstLambdaForSelectMany = false;
                continue;
            }

            complexity = ComplexityComposer.Sequential(complexity, AnalyzeLambda(lambda));
            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
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
        if (block.Statements.Count != 1
            || block.Statements[0] is not ReturnStatementSyntax { Expression: { } expression })
        {
            return ComplexityFactory.Unknown();
        }

        return new BasicOperationAnalyzer(context).AnalyzeExpression(expression);
    }

    private ComplexityExpression AnalyzeNonLambdaArguments(InvocationExpressionSyntax invocation)
    {
        ComplexityExpression complexity = ComplexityFactory.Constant();
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (argument.Expression is LambdaExpressionSyntax)
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

    private bool HasLambdaArgument(InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments.Any(argument => argument.Expression is LambdaExpressionSyntax);
    }

    private bool TryResolveInvocation(
        InvocationExpressionSyntax invocation,
        out IMethodSymbol? methodSymbol,
        out KnownOperationMapping? mapping)
    {
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        methodSymbol = symbolInfo.Symbol as IMethodSymbol;
        if (methodSymbol is null
            || !Resolver.TryResolve(methodSymbol, context.CancellationToken, out KnownOperationMapping resolvedMapping))
        {
            mapping = null;
            return false;
        }

        mapping = resolvedMapping;
        return true;
    }

    private static bool TryGetReceiverExpression(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        out ExpressionSyntax? receiver)
    {
        receiver = null;

        if (methodSymbol.ReducedFrom is not null
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            receiver = memberAccess.Expression;
            return true;
        }

        if (methodSymbol.ReducedFrom is null
            && invocation.Expression is MemberAccessExpressionSyntax
            && methodSymbol.IsStatic
            && invocation.ArgumentList.Arguments.Count > 0)
        {
            receiver = invocation.ArgumentList.Arguments[0].Expression;
            return true;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax instanceMemberAccess)
        {
            receiver = instanceMemberAccess.Expression;
            return true;
        }

        return false;
    }

    private bool TryResolveDirectInputDimension(ExpressionSyntax expression, out ComplexityVariable? variable)
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
        }

        if (expression is MemberAccessExpressionSyntax memberAccess
            && StringComparer.Ordinal.Equals(memberAccess.Name.Identifier.ValueText, "Length")
            && TryResolveDirectInputDimension(memberAccess.Expression, out ComplexityVariable? receiverVariable)
            && IsArrayOrString(memberAccess.Expression))
        {
            variable = receiverVariable;
            return true;
        }

        return false;
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

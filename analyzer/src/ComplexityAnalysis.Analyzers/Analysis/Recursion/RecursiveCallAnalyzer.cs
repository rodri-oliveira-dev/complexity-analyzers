using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecursiveCallAnalyzer
{
    private const double IntegerTolerance = 0.000000001;

    internal RecursiveCallAnalysisResult Analyze(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        IMethodSymbol methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken)
            ?? throw new InvalidOperationException("The method declaration must resolve to a method symbol.");
        MethodAnalysisContext context = MethodAnalysisContext.Create(
            semanticModel,
            methodSymbol,
            cancellationToken);

        return Analyze(methodDeclaration, context);
    }

    internal RecursiveCallAnalysisResult Analyze(
        MethodDeclarationSyntax methodDeclaration,
        MethodAnalysisContext context)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = context ?? throw new ArgumentNullException(nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        PathSummary summary = methodDeclaration.Body is not null
            ? AnalyzeBlock(methodDeclaration.Body, context, LocalArgumentFacts.Empty)
            : methodDeclaration.ExpressionBody is not null
                ? AnalyzeExpressionBody(methodDeclaration.ExpressionBody.Expression, context)
                : PathSummary.Unsupported("Only ordinary methods with a block or expression body are supported.");

        return summary.IsSupported
            ? RecursiveCallAnalysisResult.Supported(
                DeduplicateBaseCases(summary.BaseCaseEvidence),
                summary.Paths
                    .Where(path => path.RecursiveCalls.Length > 0)
                    .Select(path => new RecursiveExecutionPath(path.RecursiveCalls))
                    .ToImmutableArray())
            : RecursiveCallAnalysisResult.Unsupported(summary.UnsupportedReason ?? "The recursive call shape is unsupported.");
    }

    private static PathSummary AnalyzeExpressionBody(
        ExpressionSyntax expression,
        MethodAnalysisContext context)
    {
        return PathSummary.Single(
            RecursivePathState.Terminating(
                ExtractRecursiveCallsFromExpression(
                    expression,
                    context,
                    LocalArgumentFacts.Empty)),
            ImmutableArray<BaseCaseEvidence>.Empty);
    }

    private static PathSummary AnalyzeBlock(
        BlockSyntax block,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        return AnalyzeStatements(block.Statements, context, localFacts);
    }

    private static PathSummary AnalyzeStatements(
        SyntaxList<StatementSyntax> statements,
        MethodAnalysisContext context,
        LocalArgumentFacts initialLocalFacts)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ImmutableArray<RecursivePathState> currentPaths =
            ImmutableArray.Create(RecursivePathState.Active(
                ImmutableArray<RecursiveCallShape>.Empty,
                initialLocalFacts));
        ImmutableArray<BaseCaseEvidence>.Builder baseCases =
            ImmutableArray.CreateBuilder<BaseCaseEvidence>();

        foreach (StatementSyntax statement in statements)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            ImmutableArray<RecursivePathState>.Builder nextPaths =
                ImmutableArray.CreateBuilder<RecursivePathState>();

            foreach (RecursivePathState currentPath in currentPaths)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (currentPath.Terminates)
                {
                    nextPaths.Add(currentPath);
                    continue;
                }

                PathSummary statementSummary = AnalyzeStatement(
                    statement,
                    context,
                    currentPath.LocalFacts);
                if (!statementSummary.IsSupported)
                {
                    return statementSummary;
                }

                baseCases.AddRange(statementSummary.BaseCaseEvidence);
                foreach (RecursivePathState statementPath in statementSummary.Paths)
                {
                    nextPaths.Add(currentPath.Append(statementPath));
                }
            }

            currentPaths = nextPaths.ToImmutable();
        }

        return PathSummary.Create(currentPaths, baseCases.ToImmutable());
    }

    private static PathSummary AnalyzeStatement(
        StatementSyntax statement,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        return statement switch
        {
            BlockSyntax block => AnalyzeBlock(block, context, localFacts),
            IfStatementSyntax ifStatement => AnalyzeIfStatement(ifStatement, context, localFacts),
            ReturnStatementSyntax returnStatement => AnalyzeReturnStatement(returnStatement, context, localFacts),
            ExpressionStatementSyntax expressionStatement => AnalyzeExpressionStatement(expressionStatement, context, localFacts),
            LocalDeclarationStatementSyntax localDeclaration => AnalyzeLocalDeclaration(localDeclaration, context, localFacts),
            EmptyStatementSyntax => PathSummary.Single(
                RecursivePathState.Active(ImmutableArray<RecursiveCallShape>.Empty, localFacts),
                ImmutableArray<BaseCaseEvidence>.Empty),
            ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax =>
                ContainsDirectRecursiveInvocation(statement, context)
                    ? PathSummary.Unsupported("Recursive calls inside loops are not summarized in this extraction step.")
                    : PathSummary.Single(
                        RecursivePathState.Active(ImmutableArray<RecursiveCallShape>.Empty, localFacts),
                        ImmutableArray<BaseCaseEvidence>.Empty),
            _ => PathSummary.Single(
                RecursivePathState.Active(
                    ExtractRecursiveCallsFromNode(statement, context, localFacts),
                    localFacts),
                ImmutableArray<BaseCaseEvidence>.Empty),
        };
    }

    private static PathSummary AnalyzeIfStatement(
        IfStatementSyntax ifStatement,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        PathSummary trueBranch = AnalyzeStatement(ifStatement.Statement, context, localFacts);
        if (!trueBranch.IsSupported)
        {
            return trueBranch;
        }

        PathSummary falseBranch = ifStatement.Else is null
            ? PathSummary.Single(
                RecursivePathState.Active(ImmutableArray<RecursiveCallShape>.Empty, localFacts),
                ImmutableArray<BaseCaseEvidence>.Empty)
            : AnalyzeStatement(ifStatement.Else.Statement, context, localFacts);
        if (!falseBranch.IsSupported)
        {
            return falseBranch;
        }

        ImmutableArray<BaseCaseEvidence>.Builder baseCases =
            ImmutableArray.CreateBuilder<BaseCaseEvidence>();
        baseCases.AddRange(trueBranch.BaseCaseEvidence);
        baseCases.AddRange(falseBranch.BaseCaseEvidence);
        baseCases.AddRange(ExtractBaseCaseEvidence(ifStatement.Condition, trueBranch, falseBranch, context));

        return PathSummary.Create(
            trueBranch.Paths.AddRange(falseBranch.Paths),
            baseCases.ToImmutable());
    }

    private static PathSummary AnalyzeReturnStatement(
        ReturnStatementSyntax returnStatement,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        ImmutableArray<RecursiveCallShape> calls = returnStatement.Expression is null
            ? ImmutableArray<RecursiveCallShape>.Empty
            : ExtractRecursiveCallsFromExpression(returnStatement.Expression, context, localFacts);

        return PathSummary.Single(
            RecursivePathState.Terminating(calls, localFacts),
            ImmutableArray<BaseCaseEvidence>.Empty);
    }

    private static PathSummary AnalyzeExpressionStatement(
        ExpressionStatementSyntax expressionStatement,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        return PathSummary.Single(
            RecursivePathState.Active(
                ExtractRecursiveCallsFromExpression(
                    expressionStatement.Expression,
                    context,
                    localFacts),
                localFacts),
            ImmutableArray<BaseCaseEvidence>.Empty);
    }

    private static PathSummary AnalyzeLocalDeclaration(
        LocalDeclarationStatementSyntax localDeclaration,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        ImmutableArray<RecursiveCallShape>.Builder calls =
            ImmutableArray.CreateBuilder<RecursiveCallShape>();
        LocalArgumentFacts currentFacts = localFacts;

        foreach (VariableDeclaratorSyntax variable in localDeclaration.Declaration.Variables)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (variable.Initializer?.Value is not { } initializer)
            {
                continue;
            }

            calls.AddRange(ExtractRecursiveCallsFromExpression(initializer, context, currentFacts));

            ISymbol? symbol = context.SemanticModel.GetDeclaredSymbol(
                variable,
                context.CancellationToken);
            if (symbol is not null
                && TryResolveArgumentRelation(initializer, context, currentFacts, out ArgumentRelationInfo relation))
            {
                currentFacts = currentFacts.Set(symbol, relation);
            }
        }

        return PathSummary.Single(
            RecursivePathState.Active(calls.ToImmutable(), currentFacts),
            ImmutableArray<BaseCaseEvidence>.Empty);
    }

    private static ImmutableArray<BaseCaseEvidence> ExtractBaseCaseEvidence(
        ExpressionSyntax condition,
        PathSummary trueBranch,
        PathSummary falseBranch,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (!TryResolveBaseCaseCondition(condition, context, out BaseCaseCondition? baseCaseCondition)
            || baseCaseCondition is null)
        {
            return ImmutableArray<BaseCaseEvidence>.Empty;
        }

        ImmutableArray<BaseCaseEvidence>.Builder evidence =
            ImmutableArray.CreateBuilder<BaseCaseEvidence>();
        if ((baseCaseCondition.AppliesWhenTrue && HasTerminatingNonRecursivePath(trueBranch))
            || (!baseCaseCondition.AppliesWhenTrue && HasTerminatingNonRecursivePath(falseBranch)))
        {
            evidence.Add(new BaseCaseEvidence(
                baseCaseCondition.Parameter,
                baseCaseCondition.Variable));
        }

        return evidence.ToImmutable();
    }

    private static bool HasTerminatingNonRecursivePath(PathSummary summary)
    {
        return summary.Paths.Any(path => path.Terminates && path.RecursiveCalls.Length == 0);
    }

    private static bool TryResolveBaseCaseCondition(
        ExpressionSyntax condition,
        MethodAnalysisContext context,
        out BaseCaseCondition? baseCaseCondition)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        baseCaseCondition = null;
        condition = UnwrapParentheses(condition);
        if (condition is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        ExpressionSyntax left = UnwrapParentheses(binary.Left);
        ExpressionSyntax right = UnwrapParentheses(binary.Right);

        return TryResolveParameterReference(left, context, out IParameterSymbol? leftParameter, out ComplexityVariable? leftVariable)
            && leftParameter is not null
            && leftVariable is not null
            && TryGetNumericConstant(right, context, out _)
            ? TryCreateBaseCaseCondition(
                binary.Kind(),
                leftParameter,
                leftVariable,
                variableOnLeft: true,
                out baseCaseCondition)
            : TryResolveParameterReference(right, context, out IParameterSymbol? rightParameter, out ComplexityVariable? rightVariable)
            && rightParameter is not null
            && rightVariable is not null
            && TryGetNumericConstant(left, context, out _)
            && TryCreateBaseCaseCondition(
                binary.Kind(),
                rightParameter,
                rightVariable,
                variableOnLeft: false,
                out baseCaseCondition);
    }

    private static bool TryCreateBaseCaseCondition(
        SyntaxKind comparisonKind,
        IParameterSymbol parameter,
        ComplexityVariable variable,
        bool variableOnLeft,
        out BaseCaseCondition? condition)
    {
        if (comparisonKind == SyntaxKind.EqualsExpression)
        {
            condition = new BaseCaseCondition(parameter, variable, appliesWhenTrue: true);
            return true;
        }

        if (comparisonKind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression)
        {
            condition = new BaseCaseCondition(parameter, variable, variableOnLeft);
            return true;
        }

        if (comparisonKind is not SyntaxKind.GreaterThanExpression and not SyntaxKind.GreaterThanOrEqualExpression)
        {
            condition = null;
            return false;
        }

        bool appliesWhenTrue = !variableOnLeft;
        condition = new BaseCaseCondition(parameter, variable, appliesWhenTrue);
        return true;
    }

    private static ImmutableArray<RecursiveCallShape> ExtractRecursiveCallsFromNode(
        SyntaxNode node,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ImmutableArray<RecursiveCallShape>.Builder calls =
            ImmutableArray.CreateBuilder<RecursiveCallShape>();

        foreach (InvocationExpressionSyntax invocation in node
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TryCreateRecursiveCallShape(invocation, context, localFacts, out RecursiveCallShape? call)
                && call is not null)
            {
                calls.Add(call);
            }
        }

        return calls.ToImmutable();
    }

    private static ImmutableArray<RecursiveCallShape> ExtractRecursiveCallsFromExpression(
        ExpressionSyntax expression,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts)
    {
        return ExtractRecursiveCallsFromNode(expression, context, localFacts);
    }

    private static bool ContainsDirectRecursiveInvocation(
        SyntaxNode node,
        MethodAnalysisContext context)
    {
        return node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => IsDirectRecursiveInvocation(invocation, context, out _));
    }

    private static bool TryCreateRecursiveCallShape(
        InvocationExpressionSyntax invocation,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts,
        out RecursiveCallShape? callShape)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        callShape = null;
        if (!IsDirectRecursiveInvocation(invocation, context, out IMethodSymbol? targetMethod)
            || targetMethod is null)
        {
            return false;
        }

        ImmutableDictionary<IParameterSymbol, ExpressionSyntax> argumentsByParameter =
            BindArgumentsToParameters(
                invocation,
                targetMethod,
                context.MethodSymbol.Parameters,
                context.CancellationToken);
        ImmutableArray<RecursiveArgumentRelation>.Builder relations =
            ImmutableArray.CreateBuilder<RecursiveArgumentRelation>();

        foreach (IParameterSymbol parameter in context.MethodSymbol.Parameters)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!context.TryGetInputSizeVariable(parameter, out ComplexityVariable variable))
            {
                continue;
            }

            relations.Add(
                argumentsByParameter.TryGetValue(parameter, out ExpressionSyntax? argument)
                    && TryResolveArgumentRelation(argument, context, localFacts, out ArgumentRelationInfo relation)
                    && SymbolEqualityComparer.Default.Equals(parameter, relation.Parameter)
                    ? relation.ToRecursiveArgumentRelation()
                    : RecursiveArgumentRelation.Unknown(parameter, variable));
        }

        callShape = new RecursiveCallShape(targetMethod, relations.ToImmutable());
        return true;
    }

    private static bool IsDirectRecursiveInvocation(
        InvocationExpressionSyntax invocation,
        MethodAnalysisContext context,
        out IMethodSymbol? targetMethod)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        targetMethod = null;
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(
            invocation,
            context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        IMethodSymbol targetDefinition = GetMethodDefinition(methodSymbol);
        IMethodSymbol currentDefinition = GetMethodDefinition(context.MethodSymbol);
        if (!SymbolEqualityComparer.Default.Equals(targetDefinition, currentDefinition))
        {
            return false;
        }

        targetMethod = methodSymbol;
        return true;
    }

    private static IMethodSymbol GetMethodDefinition(IMethodSymbol methodSymbol)
    {
        return (methodSymbol.ReducedFrom ?? methodSymbol).OriginalDefinition;
    }

    private static bool TryResolveArgumentRelation(
        ExpressionSyntax expression,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts,
        out ArgumentRelationInfo relation)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);

        if (TryResolveParameterReference(
            expression,
            context,
            out IParameterSymbol? parameter,
            out ComplexityVariable? variable)
            && parameter is not null
            && variable is not null)
        {
            relation = ArgumentRelationInfo.Unchanged(parameter, variable);
            return true;
        }

        if (expression is IdentifierNameSyntax
            && TryResolveLocalArgumentFact(expression, context, localFacts, out relation))
        {
            return true;
        }

        if (expression is BinaryExpressionSyntax binary
            && TryResolveBinaryArgumentRelation(binary, context, localFacts, out relation))
        {
            return true;
        }

        relation = default;
        return false;
    }

    private static bool TryResolveBinaryArgumentRelation(
        BinaryExpressionSyntax binary,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts,
        out ArgumentRelationInfo relation)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ExpressionSyntax left = UnwrapParentheses(binary.Left);
        ExpressionSyntax right = UnwrapParentheses(binary.Right);

        bool leftIsArgument = TryResolveArgumentRelation(left, context, localFacts, out ArgumentRelationInfo leftRelation);
        bool rightIsArgument = TryResolveArgumentRelation(right, context, localFacts, out ArgumentRelationInfo rightRelation);
        bool leftIsConstant = TryGetNumericConstant(left, context, out double leftConstant);
        bool rightIsConstant = TryGetNumericConstant(right, context, out double rightConstant);

        if (binary.IsKind(SyntaxKind.SubtractExpression)
            && leftIsArgument
            && IsSameSize(leftRelation)
            && rightIsConstant)
        {
            if (rightConstant > 0)
            {
                relation = ArgumentRelationInfo.Reducing(
                    leftRelation.Parameter,
                    leftRelation.Variable,
                    RecurrenceReduction.SubtractConstant(NormalizeConstant(rightConstant)));
                return true;
            }

            relation = ArgumentRelationInfo.Increasing(leftRelation.Parameter, leftRelation.Variable);
            return true;
        }

        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            if (leftIsArgument
                && IsSameSize(leftRelation)
                && rightIsConstant)
            {
                relation = CreateAdditiveRelation(leftRelation, rightConstant);
                return true;
            }

            if (rightIsArgument
                && IsSameSize(rightRelation)
                && leftIsConstant)
            {
                relation = CreateAdditiveRelation(rightRelation, leftConstant);
                return true;
            }
        }

        if (binary.IsKind(SyntaxKind.DivideExpression)
            && leftIsArgument
            && IsSameSize(leftRelation)
            && rightIsConstant)
        {
            if (rightConstant > 1)
            {
                relation = ArgumentRelationInfo.Reducing(
                    leftRelation.Parameter,
                    leftRelation.Variable,
                    RecurrenceReduction.Scale(1 / rightConstant));
                return true;
            }

            relation = rightConstant == 1
                ? ArgumentRelationInfo.Unchanged(leftRelation.Parameter, leftRelation.Variable)
                : ArgumentRelationInfo.Unknown(leftRelation.Parameter, leftRelation.Variable);
            return true;
        }

        if (binary.IsKind(SyntaxKind.MultiplyExpression))
        {
            if (leftIsArgument
                && IsSameSize(leftRelation)
                && rightIsConstant)
            {
                relation = CreateMultiplicativeRelation(leftRelation, rightConstant);
                return true;
            }

            if (rightIsArgument
                && IsSameSize(rightRelation)
                && leftIsConstant)
            {
                relation = CreateMultiplicativeRelation(rightRelation, leftConstant);
                return true;
            }
        }

        relation = default;
        return false;
    }

    private static ArgumentRelationInfo CreateAdditiveRelation(
        ArgumentRelationInfo baseRelation,
        double constant)
    {
        return constant < 0
            ? ArgumentRelationInfo.Reducing(
                baseRelation.Parameter,
                baseRelation.Variable,
                RecurrenceReduction.SubtractConstant(NormalizeConstant(-constant)))
            : constant > 0
            ? ArgumentRelationInfo.Increasing(baseRelation.Parameter, baseRelation.Variable)
            : ArgumentRelationInfo.Unchanged(baseRelation.Parameter, baseRelation.Variable);
    }

    private static ArgumentRelationInfo CreateMultiplicativeRelation(
        ArgumentRelationInfo baseRelation,
        double constant)
    {
        return constant is > 0 and < 1
            ? ArgumentRelationInfo.Reducing(
                baseRelation.Parameter,
                baseRelation.Variable,
                RecurrenceReduction.Scale(constant))
            : NearlyEquals(constant, 1)
            ? ArgumentRelationInfo.Unchanged(baseRelation.Parameter, baseRelation.Variable)
            : ArgumentRelationInfo.Unknown(baseRelation.Parameter, baseRelation.Variable);
    }

    private static bool IsSameSize(ArgumentRelationInfo relation)
    {
        return relation.Kind == RecursiveArgumentRelationKind.Unchanged;
    }

    private static bool TryResolveParameterReference(
        ExpressionSyntax expression,
        MethodAnalysisContext context,
        out IParameterSymbol? parameter,
        out ComplexityVariable? variable)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        parameter = null;
        variable = null;
        expression = UnwrapParentheses(expression);
        if (expression is not IdentifierNameSyntax)
        {
            return false;
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(
            expression,
            context.CancellationToken);
        if (symbolInfo.Symbol is IParameterSymbol parameterSymbol
            && context.TryGetInputSizeVariable(parameterSymbol, out ComplexityVariable inputVariable))
        {
            parameter = parameterSymbol;
            variable = inputVariable;
            return true;
        }

        return false;
    }

    private static bool TryResolveLocalArgumentFact(
        ExpressionSyntax expression,
        MethodAnalysisContext context,
        LocalArgumentFacts localFacts,
        out ArgumentRelationInfo relation)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        relation = default;
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(
            expression,
            context.CancellationToken);
        return symbolInfo.Symbol is not null
            && localFacts.TryGet(symbolInfo.Symbol, out relation);
    }

    private static ImmutableDictionary<IParameterSymbol, ExpressionSyntax> BindArgumentsToParameters(
        InvocationExpressionSyntax invocation,
        IMethodSymbol targetMethodSymbol,
        ImmutableArray<IParameterSymbol> parameters,
        CancellationToken cancellationToken)
    {
        ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Builder bindings =
            ImmutableDictionary.CreateBuilder<IParameterSymbol, ExpressionSyntax>(ParameterSymbolComparer.Instance);
        int nextPositionalParameter = TryBindReducedExtensionReceiver(
            invocation,
            targetMethodSymbol,
            parameters,
            bindings,
            cancellationToken)
            ? 1
            : 0;

        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IParameterSymbol? parameter = argument.NameColon is not null
                ? FindParameterByName(parameters, argument.NameColon.Name.Identifier.ValueText)
                : FindNextPositionalParameter(parameters, bindings, ref nextPositionalParameter);

            if (parameter is not null)
            {
                bindings[parameter] = argument.Expression;
            }
        }

        return bindings.ToImmutable();
    }

    private static bool TryBindReducedExtensionReceiver(
        InvocationExpressionSyntax invocation,
        IMethodSymbol targetMethodSymbol,
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Builder bindings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (targetMethodSymbol.ReducedFrom is null
            || parameters.IsEmpty
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        bindings[parameters[0]] = memberAccess.Expression;
        return true;
    }

    private static IParameterSymbol? FindParameterByName(
        ImmutableArray<IParameterSymbol> parameters,
        string parameterName)
    {
        foreach (IParameterSymbol parameter in parameters)
        {
            if (StringComparer.Ordinal.Equals(parameter.Name, parameterName))
            {
                return parameter;
            }
        }

        return null;
    }

    private static IParameterSymbol? FindNextPositionalParameter(
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableDictionary<IParameterSymbol, ExpressionSyntax>.Builder bindings,
        ref int nextPositionalParameter)
    {
        while (nextPositionalParameter < parameters.Length)
        {
            IParameterSymbol parameter = parameters[nextPositionalParameter];
            nextPositionalParameter++;

            if (!bindings.ContainsKey(parameter))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool TryGetNumericConstant(
        ExpressionSyntax expression,
        MethodAnalysisContext context,
        out double value)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        Optional<object?> constantValue = context.SemanticModel.GetConstantValue(
            UnwrapParentheses(expression),
            context.CancellationToken);
        if (!constantValue.HasValue || constantValue.Value is null)
        {
            value = default;
            return false;
        }

        return TryConvertNumericConstant(constantValue.Value, out value);
    }

    private static bool TryConvertNumericConstant(object value, out double number)
    {
        switch (value)
        {
            case sbyte sbyteValue:
                number = sbyteValue;
                return true;
            case byte byteValue:
                number = byteValue;
                return true;
            case short shortValue:
                number = shortValue;
                return true;
            case ushort ushortValue:
                number = ushortValue;
                return true;
            case int intValue:
                number = intValue;
                return true;
            case uint uintValue:
                number = uintValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                number = ulongValue;
                return true;
            case float floatValue when !float.IsNaN(floatValue) && !float.IsInfinity(floatValue):
                number = floatValue;
                return true;
            case double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue):
                number = doubleValue;
                return true;
            case decimal decimalValue:
                number = (double)decimalValue;
                return !double.IsNaN(number) && !double.IsInfinity(number);
            default:
                number = default;
                return false;
        }
    }

    private static double NormalizeConstant(double value)
    {
        double nearestInteger = Math.Round(value);
        return Math.Abs(value - nearestInteger) <= IntegerTolerance
            ? nearestInteger
            : value;
    }

    private static bool NearlyEquals(double left, double right)
    {
        return Math.Abs(left - right) <= IntegerTolerance;
    }

    private static ImmutableArray<BaseCaseEvidence> DeduplicateBaseCases(
        ImmutableArray<BaseCaseEvidence> baseCases)
    {
        ImmutableArray<BaseCaseEvidence>.Builder builder =
            ImmutableArray.CreateBuilder<BaseCaseEvidence>();

        foreach (BaseCaseEvidence baseCase in baseCases)
        {
            if (!builder.Any(existing => existing.Equals(baseCase)))
            {
                builder.Add(baseCase);
            }
        }

        return builder.ToImmutable();
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private readonly struct ArgumentRelationInfo
    {
        private ArgumentRelationInfo(
            IParameterSymbol parameter,
            ComplexityVariable variable,
            RecursiveArgumentRelationKind kind,
            RecurrenceReduction? reduction)
        {
            Parameter = parameter;
            Variable = variable;
            Kind = kind;
            Reduction = reduction;
        }

        internal IParameterSymbol Parameter
        {
            get;
        }

        internal ComplexityVariable Variable
        {
            get;
        }

        internal RecursiveArgumentRelationKind Kind
        {
            get;
        }

        internal RecurrenceReduction? Reduction
        {
            get;
        }

        internal static ArgumentRelationInfo Unknown(
            IParameterSymbol parameter,
            ComplexityVariable variable)
        {
            return new ArgumentRelationInfo(
                parameter,
                variable,
                RecursiveArgumentRelationKind.Unknown,
                reduction: null);
        }

        internal static ArgumentRelationInfo Unchanged(
            IParameterSymbol parameter,
            ComplexityVariable variable)
        {
            return new ArgumentRelationInfo(
                parameter,
                variable,
                RecursiveArgumentRelationKind.Unchanged,
                reduction: null);
        }

        internal static ArgumentRelationInfo Increasing(
            IParameterSymbol parameter,
            ComplexityVariable variable)
        {
            return new ArgumentRelationInfo(
                parameter,
                variable,
                RecursiveArgumentRelationKind.Increasing,
                reduction: null);
        }

        internal static ArgumentRelationInfo Reducing(
            IParameterSymbol parameter,
            ComplexityVariable variable,
            RecurrenceReduction reduction)
        {
            return new ArgumentRelationInfo(
                parameter,
                variable,
                RecursiveArgumentRelationKind.Reducing,
                reduction);
        }

        internal RecursiveArgumentRelation ToRecursiveArgumentRelation()
        {
            return new RecursiveArgumentRelation(Parameter, Variable, Kind, Reduction);
        }
    }

    private sealed class BaseCaseCondition
    {
        internal BaseCaseCondition(
            IParameterSymbol parameter,
            ComplexityVariable variable,
            bool appliesWhenTrue)
        {
            Parameter = parameter;
            Variable = variable;
            AppliesWhenTrue = appliesWhenTrue;
        }

        internal IParameterSymbol Parameter
        {
            get;
        }

        internal ComplexityVariable Variable
        {
            get;
        }

        internal bool AppliesWhenTrue
        {
            get;
        }
    }

    private sealed class LocalArgumentFacts
    {
        internal static readonly LocalArgumentFacts Empty = new(
            ImmutableDictionary.Create<ISymbol, ArgumentRelationInfo>(SymbolEqualityComparer.Default));

        private readonly ImmutableDictionary<ISymbol, ArgumentRelationInfo> facts;

        private LocalArgumentFacts(ImmutableDictionary<ISymbol, ArgumentRelationInfo> facts)
        {
            this.facts = facts;
        }

        internal LocalArgumentFacts Set(ISymbol symbol, ArgumentRelationInfo relation)
        {
            return new LocalArgumentFacts(facts.SetItem(symbol, relation));
        }

        internal bool TryGet(ISymbol symbol, out ArgumentRelationInfo relation)
        {
            return facts.TryGetValue(symbol, out relation);
        }
    }

    private sealed class RecursivePathState
    {
        private RecursivePathState(
            ImmutableArray<RecursiveCallShape> recursiveCalls,
            bool terminates,
            LocalArgumentFacts localFacts)
        {
            RecursiveCalls = recursiveCalls;
            Terminates = terminates;
            LocalFacts = localFacts;
        }

        internal ImmutableArray<RecursiveCallShape> RecursiveCalls
        {
            get;
        }

        internal bool Terminates
        {
            get;
        }

        internal LocalArgumentFacts LocalFacts
        {
            get;
        }

        internal static RecursivePathState Active(
            ImmutableArray<RecursiveCallShape> recursiveCalls,
            LocalArgumentFacts localFacts)
        {
            return new RecursivePathState(recursiveCalls, terminates: false, localFacts);
        }

        internal static RecursivePathState Terminating(
            ImmutableArray<RecursiveCallShape> recursiveCalls,
            LocalArgumentFacts localFacts)
        {
            return new RecursivePathState(recursiveCalls, terminates: true, localFacts);
        }

        internal static RecursivePathState Terminating(
            ImmutableArray<RecursiveCallShape> recursiveCalls)
        {
            return new RecursivePathState(recursiveCalls, terminates: true, LocalArgumentFacts.Empty);
        }

        internal RecursivePathState Append(RecursivePathState next)
        {
            return new RecursivePathState(
                RecursiveCalls.AddRange(next.RecursiveCalls),
                next.Terminates,
                next.LocalFacts);
        }
    }

    private sealed class PathSummary
    {
        private PathSummary(
            bool isSupported,
            ImmutableArray<RecursivePathState> paths,
            ImmutableArray<BaseCaseEvidence> baseCaseEvidence,
            string? unsupportedReason)
        {
            IsSupported = isSupported;
            Paths = paths;
            BaseCaseEvidence = baseCaseEvidence;
            UnsupportedReason = unsupportedReason;
        }

        internal bool IsSupported
        {
            get;
        }

        internal ImmutableArray<RecursivePathState> Paths
        {
            get;
        }

        internal ImmutableArray<BaseCaseEvidence> BaseCaseEvidence
        {
            get;
        }

        internal string? UnsupportedReason
        {
            get;
        }

        internal static PathSummary Create(
            ImmutableArray<RecursivePathState> paths,
            ImmutableArray<BaseCaseEvidence> baseCaseEvidence)
        {
            return new PathSummary(
                isSupported: true,
                paths,
                baseCaseEvidence,
                unsupportedReason: null);
        }

        internal static PathSummary Single(
            RecursivePathState path,
            ImmutableArray<BaseCaseEvidence> baseCaseEvidence)
        {
            return Create(ImmutableArray.Create(path), baseCaseEvidence);
        }

        internal static PathSummary Unsupported(string reason)
        {
            return new PathSummary(
                isSupported: false,
                paths: ImmutableArray<RecursivePathState>.Empty,
                baseCaseEvidence: ImmutableArray<BaseCaseEvidence>.Empty,
                unsupportedReason: reason);
        }
    }

    private sealed class ParameterSymbolComparer : IEqualityComparer<IParameterSymbol>
    {
        internal static readonly ParameterSymbolComparer Instance = new();

        public bool Equals(IParameterSymbol? x, IParameterSymbol? y)
        {
            return SymbolEqualityComparer.Default.Equals(x, y);
        }

        public int GetHashCode(IParameterSymbol obj)
        {
            return SymbolEqualityComparer.Default.GetHashCode(obj);
        }
    }
}

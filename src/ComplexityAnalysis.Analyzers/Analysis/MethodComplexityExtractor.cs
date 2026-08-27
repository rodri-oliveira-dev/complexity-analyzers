using System;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Analysis.Recursion;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class MethodComplexityExtractor
{
    internal static ComplexityExpression AnalyzeMethod(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        return TryCreateMember(methodDeclaration, semanticModel, cancellationToken, out ExecutableMember member)
            ? AnalyzeMember(member, semanticModel, cancellationToken)
            : throw new InvalidOperationException("The method declaration must resolve to a method symbol.");
    }

    internal static ComplexityExpression AnalyzeMember(
        ExecutableMember member,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        ComplexityAnalyzerOptions options = ComplexityAnalyzerOptions.Default;
        InterproceduralAnalysisContext interproceduralContext = InterproceduralAnalysisContext.Create(
            semanticModel.Compilation,
            cancellationToken);
        InterproceduralRootAnalysisState rootState = InterproceduralAnalysisContext.CreateRootState(
            member.Symbol,
            options,
            cancellationToken);

        return AnalyzeMember(
            member,
            semanticModel,
            interproceduralContext,
            rootState,
            options,
            cancellationToken);
    }

    internal static ComplexityExpression AnalyzeMember(
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

        return AnalyzeMember(
            member,
            semanticModel,
            interproceduralContext,
            rootState,
            options.WithAnalysisBudget(rootState.Budget),
            cancellationToken);
    }

    internal static ComplexityExpression AnalyzeMember(
        ExecutableMember member,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        CancellationToken cancellationToken)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = interproceduralContext ?? throw new ArgumentNullException(nameof(interproceduralContext));

        cancellationToken.ThrowIfCancellationRequested();

        ComplexityAnalyzerOptions options = interproceduralContext.GetAnalysisOptions(
            member.SyntaxTree,
            interproceduralContext.Budget,
            cancellationToken);
        InterproceduralRootAnalysisState rootState = InterproceduralAnalysisContext.CreateRootState(
            member.Symbol,
            options,
            cancellationToken);

        return AnalyzeMember(
            member,
            semanticModel,
            interproceduralContext,
            rootState,
            options.WithAnalysisBudget(rootState.Budget),
            cancellationToken);
    }

    internal InterproceduralAnalysisResult AnalyzeSourceMember(
        ExecutableMember member,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        InterproceduralRootAnalysisState rootState,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = interproceduralContext ?? throw new ArgumentNullException(nameof(interproceduralContext));
        _ = rootState ?? throw new ArgumentNullException(nameof(rootState));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        MethodAnalysisContext context = MethodAnalysisContext.Create(
            semanticModel,
            member.Symbol,
            options.WithAnalysisBudget(rootState.Budget),
            interproceduralContext,
            rootState,
            cancellationToken);
        ComplexityExpression complexity = AnalyzeMemberCore(member, context);

        return complexity is UnknownComplexity
            ? InterproceduralAnalysisResult.Unknown("The source method complexity is unknown.")
            : InterproceduralAnalysisResult.Known(
                MethodComplexityTemplate.Create(
                    complexity,
                    context,
                    cancellationToken));
    }

    private static ComplexityExpression AnalyzeMember(
        ExecutableMember member,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        InterproceduralRootAnalysisState rootState,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        MethodAnalysisContext context = MethodAnalysisContext.Create(
            semanticModel,
            member.Symbol,
            options.WithAnalysisBudget(rootState.Budget),
            interproceduralContext,
            rootState,
            cancellationToken);

        return AnalyzeMemberCore(member, context);
    }

    private static ComplexityExpression AnalyzeMemberCore(
        ExecutableMember member,
        MethodAnalysisContext context)
    {
        return context.Options.RecursionAnalysisEnabled
            && TrySolveDirectRecurrence(member, context, out ComplexityExpression? recursiveComplexity)
            && recursiveComplexity is not null
            ? recursiveComplexity
            : member.Body.Block is not null
            ? AnalyzeBlockCore(member.Body.Block, context)
            : member.Body.Expression is null
            ? ComplexityFactory.Unknown()
            : new BasicOperationAnalyzer(context).AnalyzeExpression(member.Body.Expression);
    }

    internal static bool TrySolveDirectRecurrence(
        ExecutableMember member,
        MethodAnalysisContext context,
        out ComplexityExpression? complexity)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = context ?? throw new ArgumentNullException(nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        complexity = null;
        if (!context.Options.RecursionAnalysisEnabled)
        {
            return false;
        }

        if (!member.SupportsDirectRecursion)
        {
            return false;
        }

        if (context.InterproceduralContext?.TryGetDirectRecurrenceSolution(
            context.MethodSymbol,
            context.Options,
            context.CancellationToken,
            out ComplexityExpression cachedComplexity) == true)
        {
            complexity = cachedComplexity;
            return true;
        }

        if (!TrySolveDirectRecurrenceUncached(member, context, out complexity))
        {
            return false;
        }

        if (context.InterproceduralContext is not null
            && complexity is not null)
        {
            context.InterproceduralContext.StoreDirectRecurrenceSolution(
                context.MethodSymbol,
                context.Options,
                complexity,
                context.CancellationToken);
        }

        return true;
    }

    private static bool TrySolveDirectRecurrenceUncached(
        ExecutableMember member,
        MethodAnalysisContext context,
        out ComplexityExpression? complexity)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        complexity = null;
        RecurrenceExtractionResult extraction = new RecurrenceExtractor().Extract(
            member,
            context);
        if (extraction.Kind != RecurrenceExtractionResultKind.Extracted
            || extraction.Relation is null)
        {
            return false;
        }

        RecurrenceSolution solution = new RecurrenceSolver().Solve(extraction.Relation);
        if (solution.Kind != RecurrenceSolutionKind.Solved
            || solution.Complexity is null)
        {
            return false;
        }

        complexity = solution.Complexity;
        return true;
    }

    internal static ComplexityExpression AnalyzeMethod(
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

        return TryCreateMember(methodDeclaration, semanticModel, cancellationToken, out ExecutableMember member)
            ? AnalyzeMember(member, semanticModel, interproceduralContext, options, cancellationToken)
            : throw new InvalidOperationException("The method declaration must resolve to a method symbol.");
    }

    internal static ComplexityExpression AnalyzeMethod(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = interproceduralContext ?? throw new ArgumentNullException(nameof(interproceduralContext));

        cancellationToken.ThrowIfCancellationRequested();

        return TryCreateMember(methodDeclaration, semanticModel, cancellationToken, out ExecutableMember member)
            ? AnalyzeMember(member, semanticModel, interproceduralContext, cancellationToken)
            : throw new InvalidOperationException("The method declaration must resolve to a method symbol.");
    }

    internal InterproceduralAnalysisResult AnalyzeSourceMethod(
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        InterproceduralRootAnalysisState rootState,
        ComplexityAnalyzerOptions options,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _ = interproceduralContext ?? throw new ArgumentNullException(nameof(interproceduralContext));
        _ = rootState ?? throw new ArgumentNullException(nameof(rootState));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        ExecutableMember member = ExecutableMember.CreateOrdinaryMethod(methodDeclaration, methodSymbol);
        return AnalyzeSourceMember(
            member,
            semanticModel,
            interproceduralContext,
            rootState,
            options,
            cancellationToken);
    }

    internal static bool TrySolveDirectRecurrence(
        MethodDeclarationSyntax methodDeclaration,
        MethodAnalysisContext context,
        out ComplexityExpression? complexity)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = context ?? throw new ArgumentNullException(nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        ExecutableMember member = ExecutableMember.CreateOrdinaryMethod(methodDeclaration, context.MethodSymbol);
        return TrySolveDirectRecurrence(member, context, out complexity);
    }

    private static bool TryCreateMember(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExecutableMember member)
    {
        if (ExecutableMember.TryCreateOrdinaryMethod(
            methodDeclaration,
            semanticModel,
            cancellationToken,
            out ExecutableMember? resolvedMember)
            && resolvedMember is not null)
        {
            member = resolvedMember;
            return true;
        }

        member = null!;
        return false;
    }

    internal ComplexityExpression AnalyzeBlock(
        BlockSyntax block,
        MethodAnalysisContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));

        return AnalyzeBlockCore(block, context);
    }

    private static ComplexityExpression AnalyzeBlockCore(
        BlockSyntax block,
        MethodAnalysisContext context)
    {
        _ = block ?? throw new ArgumentNullException(nameof(block));
        _ = context ?? throw new ArgumentNullException(nameof(context));

        return AnalyzeStatements(block.Statements, context);
    }

    private static ComplexityExpression AnalyzeStatements(
        SyntaxList<StatementSyntax> statements,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression complexity = ComplexityFactory.Constant();
        MethodAnalysisContext currentContext = context;
        foreach (StatementSyntax statement in statements)
        {
            currentContext.CancellationToken.ThrowIfCancellationRequested();

            complexity = ComplexityComposer.Sequential(
                complexity,
                AnalyzeStatement(statement, currentContext));

            if (complexity is UnknownComplexity)
            {
                return complexity;
            }

            currentContext = UpdateLocalLoopFactsAfterStatement(statement, currentContext);
        }

        return complexity;
    }

    private static ComplexityExpression AnalyzeStatement(
        StatementSyntax statement,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        return statement switch
        {
            BlockSyntax block => AnalyzeBlockCore(block, context),
            ForStatementSyntax forStatement => AnalyzeForStatement(forStatement, context),
            ForEachStatementSyntax forEachStatement => AnalyzeForEachStatement(forEachStatement, context),
            WhileStatementSyntax whileStatement => AnalyzeWhileStatement(whileStatement, context),
            DoStatementSyntax doStatement => AnalyzeDoWhileStatement(doStatement, context),
            IfStatementSyntax ifStatement => AnalyzeIfStatement(ifStatement, context),
            SwitchStatementSyntax switchStatement => AnalyzeSwitchStatement(switchStatement, context),
            LocalFunctionStatementSyntax => ComplexityFactory.Constant(),
            _ => new BasicOperationAnalyzer(context).AnalyzeStatement(statement),
        };
    }

    private static ComplexityExpression AnalyzeForStatement(
        ForStatementSyntax forStatement,
        MethodAnalysisContext context)
    {
        LoopBoundAnalysisResult loopBound = new LoopBoundAnalyzer(context).AnalyzeFor(forStatement);
        return loopBound.IsAnalyzable
            ? ComplexityComposer.Nested(
                loopBound.IterationComplexity,
                AnalyzeLoopBody(forStatement.Statement, context))
            : ComplexityFactory.Unknown();
    }

    private static ComplexityExpression AnalyzeForEachStatement(
        ForEachStatementSyntax forEachStatement,
        MethodAnalysisContext context)
    {
        LoopBoundAnalysisResult loopBound = new LoopBoundAnalyzer(context).AnalyzeForEach(forEachStatement);
        if (!loopBound.IsAnalyzable)
        {
            return ComplexityFactory.Unknown();
        }

        ComplexityExpression bodyComplexity = ComplexityComposer.Nested(
            loopBound.IterationComplexity,
            AnalyzeLoopBody(forEachStatement.Statement, context));

        return ComplexityComposer.Sequential(
            loopBound.EnumerationComplexity,
            bodyComplexity);
    }

    private static ComplexityExpression AnalyzeWhileStatement(
        WhileStatementSyntax whileStatement,
        MethodAnalysisContext context)
    {
        LoopBoundAnalysisResult loopBound = new LoopBoundAnalyzer(context).AnalyzeWhile(whileStatement);
        return loopBound.IsAnalyzable
            ? ComplexityComposer.Nested(
                loopBound.IterationComplexity,
                AnalyzeLoopBody(whileStatement.Statement, context))
            : ComplexityFactory.Unknown();
    }

    private static ComplexityExpression AnalyzeDoWhileStatement(
        DoStatementSyntax doStatement,
        MethodAnalysisContext context)
    {
        LoopBoundAnalysisResult loopBound = new LoopBoundAnalyzer(context).AnalyzeDoWhile(doStatement);
        return loopBound.IsAnalyzable
            ? ComplexityComposer.Nested(
                loopBound.IterationComplexity,
                AnalyzeLoopBody(doStatement.Statement, context))
            : ComplexityFactory.Unknown();
    }

    private static ComplexityExpression AnalyzeIfStatement(
        IfStatementSyntax ifStatement,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression conditionComplexity =
            new BasicOperationAnalyzer(context).AnalyzeExpression(ifStatement.Condition);
        ComplexityExpression trueBranch = AnalyzeStatement(ifStatement.Statement, context);
        ComplexityExpression falseBranch = ifStatement.Else is null
            ? ComplexityFactory.Constant()
            : AnalyzeStatement(ifStatement.Else.Statement, context);

        return ComplexityComposer.Sequential(
            conditionComplexity,
            ComplexityComposer.Branching(trueBranch, falseBranch));
    }

    private static ComplexityExpression AnalyzeSwitchStatement(
        SwitchStatementSyntax switchStatement,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression switchExpressionComplexity =
            new BasicOperationAnalyzer(context).AnalyzeExpression(switchStatement.Expression);
        ComplexityExpression branchComplexity = ComplexityFactory.Constant();

        foreach (SwitchSectionSyntax section in switchStatement.Sections)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            branchComplexity = ComplexityComposer.Branching(
                branchComplexity,
                AnalyzeSwitchSection(section, context));

            if (branchComplexity is UnknownComplexity)
            {
                break;
            }
        }

        return ComplexityComposer.Sequential(switchExpressionComplexity, branchComplexity);
    }

    private static ComplexityExpression AnalyzeSwitchSection(
        SwitchSectionSyntax section,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        foreach (SwitchLabelSyntax label in section.Labels)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!IsSupportedSwitchLabel(label, context))
            {
                return ComplexityFactory.Unknown();
            }
        }

        return AnalyzeStatements(section.Statements, context);
    }

    private static bool IsSupportedSwitchLabel(
        SwitchLabelSyntax label,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        return label switch
        {
            DefaultSwitchLabelSyntax => true,
            CaseSwitchLabelSyntax caseLabel => context.SemanticModel
                .GetConstantValue(caseLabel.Value, context.CancellationToken)
                .HasValue,
            _ => false,
        };
    }

    private static ComplexityExpression AnalyzeLoopBody(
        StatementSyntax statement,
        MethodAnalysisContext context)
    {
        return AnalyzeStatement(statement, context);
    }

    private static MethodAnalysisContext UpdateLocalLoopFactsAfterStatement(
        StatementSyntax statement,
        MethodAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        context = RemoveMutatedLocalLoopFacts(statement, context);

        return statement switch
        {
            LocalDeclarationStatementSyntax localDeclaration => AddLocalDeclarationLoopFacts(localDeclaration, context),
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
                AddSimpleAssignmentLoopFact(assignment, context),
            _ => context,
        };
    }

    private static MethodAnalysisContext AddLocalDeclarationLoopFacts(
        LocalDeclarationStatementSyntax localDeclaration,
        MethodAnalysisContext context)
    {
        LoopBoundAnalyzer loopBoundAnalyzer = new(context);
        MethodAnalysisContext currentContext = context;

        foreach (VariableDeclaratorSyntax variable in localDeclaration.Declaration.Variables)
        {
            currentContext.CancellationToken.ThrowIfCancellationRequested();

            ISymbol? symbol = currentContext.SemanticModel.GetDeclaredSymbol(
                variable,
                currentContext.CancellationToken);

            if (symbol is not null
                && variable.Initializer?.Value is { } initializer
                && loopBoundAnalyzer.TryResolveBoundExpression(initializer, out LoopBoundExpression bound))
            {
                currentContext = currentContext.WithLocalLoopBound(symbol, bound);
            }
        }

        return currentContext;
    }

    private static MethodAnalysisContext AddSimpleAssignmentLoopFact(
        AssignmentExpressionSyntax assignment,
        MethodAnalysisContext context)
    {
        return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && TryGetLocalSymbol(assignment.Left, context, out ISymbol? symbol)
            && symbol is not null
            && new LoopBoundAnalyzer(context).TryResolveBoundExpression(assignment.Right, out LoopBoundExpression bound)
                ? context.WithLocalLoopBound(symbol, bound)
                : context;
    }

    private static MethodAnalysisContext RemoveMutatedLocalLoopFacts(
        StatementSyntax statement,
        MethodAnalysisContext context)
    {
        if (context.LocalLoopBounds.IsEmpty)
        {
            return context;
        }

        MethodAnalysisContext currentContext = context;

        foreach (SyntaxNode node in ExecutableMemberSyntax.DescendantNodesAndSelfExcludingNestedExecutableBodies<SyntaxNode>(statement))
        {
            currentContext.CancellationToken.ThrowIfCancellationRequested();

            if (node is ExpressionSyntax expression
                && TryGetMutatedLocalSymbol(expression, currentContext, out ISymbol? symbol)
                && symbol is not null)
            {
                currentContext = currentContext.WithoutLocalLoopBound(symbol);
            }
        }

        return currentContext;
    }

    private static bool TryGetMutatedLocalSymbol(
        ExpressionSyntax expression,
        MethodAnalysisContext context,
        out ISymbol? symbol)
    {
        expression = UnwrapParentheses(expression);
        symbol = null;

        return expression switch
        {
            AssignmentExpressionSyntax assignment => TryGetLocalSymbol(assignment.Left, context, out symbol),
            PostfixUnaryExpressionSyntax postfixUnary when postfixUnary.IsKind(SyntaxKind.PostIncrementExpression)
                || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression) => TryGetLocalSymbol(postfixUnary.Operand, context, out symbol),
            PrefixUnaryExpressionSyntax prefixUnary when prefixUnary.IsKind(SyntaxKind.PreIncrementExpression)
                || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression) => TryGetLocalSymbol(prefixUnary.Operand, context, out symbol),
            _ => false,
        };
    }

    private static bool TryGetLocalSymbol(
        ExpressionSyntax expression,
        MethodAnalysisContext context,
        out ISymbol? symbol)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        symbol = expression is IdentifierNameSyntax
            ? context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol
            : null;

        return symbol is ILocalSymbol;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}

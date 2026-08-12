using System;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class MethodComplexityExtractor
{
    internal ComplexityExpression AnalyzeMethod(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        MethodAnalysisContext context = MethodAnalysisContext.Create(
            methodDeclaration,
            semanticModel,
            cancellationToken);

        return methodDeclaration.Body is not null
            ? AnalyzeBlock(methodDeclaration.Body, context)
            : methodDeclaration.ExpressionBody is null
            ? ComplexityFactory.Unknown()
            : new BasicOperationAnalyzer(context).AnalyzeExpression(methodDeclaration.ExpressionBody.Expression);
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

        context.CancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression complexity = ComplexityFactory.Constant();
        MethodAnalysisContext currentContext = context;
        foreach (StatementSyntax statement in block.Statements)
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
        return loopBound.IsAnalyzable
            ? ComplexityComposer.Nested(
                loopBound.IterationComplexity,
                AnalyzeLoopBody(forEachStatement.Statement, context))
            : ComplexityFactory.Unknown();
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
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            || !TryGetLocalSymbol(assignment.Left, context, out ISymbol? symbol)
            || symbol is null
            || !new LoopBoundAnalyzer(context).TryResolveBoundExpression(assignment.Right, out LoopBoundExpression bound))
        {
            return context;
        }

        return context.WithLocalLoopBound(symbol, bound);
    }

    private static MethodAnalysisContext RemoveMutatedLocalLoopFacts(
        StatementSyntax statement,
        MethodAnalysisContext context)
    {
        MethodAnalysisContext currentContext = context;

        foreach (SyntaxNode node in statement.DescendantNodesAndSelf())
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

using System;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
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
        foreach (StatementSyntax statement in block.Statements)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            complexity = ComplexityComposer.Sequential(
                complexity,
                AnalyzeStatement(statement, context));

            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
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

    private static ComplexityExpression AnalyzeLoopBody(
        StatementSyntax statement,
        MethodAnalysisContext context)
    {
        return AnalyzeStatement(statement, context);
    }
}

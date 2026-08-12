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

        BasicOperationAnalyzer basicOperations = new(context);

        return methodDeclaration.Body is not null
            ? AnalyzeBlock(methodDeclaration.Body, basicOperations, cancellationToken)
            : methodDeclaration.ExpressionBody is null
            ? ComplexityFactory.Unknown()
            : basicOperations.AnalyzeExpression(methodDeclaration.ExpressionBody.Expression);
    }

    internal ComplexityExpression AnalyzeBlock(
        BlockSyntax block,
        MethodAnalysisContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));

        return AnalyzeBlock(
            block,
            new BasicOperationAnalyzer(context),
            context.CancellationToken);
    }

    private static ComplexityExpression AnalyzeBlock(
        BlockSyntax block,
        BasicOperationAnalyzer basicOperations,
        CancellationToken cancellationToken)
    {
        _ = block ?? throw new ArgumentNullException(nameof(block));
        _ = basicOperations ?? throw new ArgumentNullException(nameof(basicOperations));

        cancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression complexity = ComplexityFactory.Constant();
        foreach (StatementSyntax statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            complexity = ComplexityComposer.Sequential(
                complexity,
                basicOperations.AnalyzeStatement(statement));

            if (complexity is UnknownComplexity)
            {
                return complexity;
            }
        }

        return complexity;
    }
}

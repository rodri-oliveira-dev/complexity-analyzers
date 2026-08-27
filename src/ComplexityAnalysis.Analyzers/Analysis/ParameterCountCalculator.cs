using System;
using System.Threading;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class ParameterCountCalculator
{
    internal bool TryCalculate(
        ExecutableMember member,
        CancellationToken cancellationToken,
        out ParameterCount parameterCount)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));

        cancellationToken.ThrowIfCancellationRequested();

        if (!member.Body.HasBody)
        {
            parameterCount = default;
            return false;
        }

        int count;
        switch (member.Declaration)
        {
            case MethodDeclarationSyntax method:
                count = Count(method.ParameterList);
                break;
            case ConstructorDeclarationSyntax constructor:
                count = Count(constructor.ParameterList);
                break;
            case OperatorDeclarationSyntax operatorDeclaration:
                count = Count(operatorDeclaration.ParameterList);
                break;
            case ConversionOperatorDeclarationSyntax conversionOperator:
                count = Count(conversionOperator.ParameterList);
                break;
            case LocalFunctionStatementSyntax localFunction:
                count = Count(localFunction.ParameterList);
                break;
            case SimpleLambdaExpressionSyntax:
                count = 1;
                break;
            case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                count = Count(parenthesizedLambda.ParameterList);
                break;
            case AnonymousMethodExpressionSyntax anonymousMethod:
                count = anonymousMethod.ParameterList is null
                    ? 0
                    : Count(anonymousMethod.ParameterList);
                break;
            case AccessorDeclarationSyntax accessor:
                count = CountAccessorParameters(accessor);
                break;
            case PropertyDeclarationSyntax:
                count = 0;
                break;
            default:
                parameterCount = default;
                return false;
        }

        parameterCount = new ParameterCount(count);
        return true;
    }

    private static int Count(BaseParameterListSyntax parameterList)
    {
        return parameterList.Parameters.Count;
    }

    private static int CountAccessorParameters(AccessorDeclarationSyntax accessor)
    {
        return accessor.Parent?.Parent is IndexerDeclarationSyntax indexer
            ? Count(indexer.ParameterList)
            : 0;
    }
}

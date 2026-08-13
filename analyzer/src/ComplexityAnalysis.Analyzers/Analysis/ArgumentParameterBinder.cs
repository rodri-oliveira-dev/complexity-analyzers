using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal static class ArgumentParameterBinder
{
    internal static ImmutableDictionary<IParameterSymbol, ExpressionSyntax> BindArgumentsToParameters(
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

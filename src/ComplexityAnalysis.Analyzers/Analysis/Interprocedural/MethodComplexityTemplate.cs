using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class MethodComplexityTemplate
{
    internal MethodComplexityTemplate(
        ComplexityExpression complexity,
        ImmutableDictionary<IParameterSymbol, ComplexityVariable> parameterVariables)
    {
        Complexity = complexity ?? throw new ArgumentNullException(nameof(complexity));
        _ = parameterVariables ?? throw new ArgumentNullException(nameof(parameterVariables));
        ParameterVariables = NormalizeParameterVariables(parameterVariables);
    }

    internal ComplexityExpression Complexity
    {
        get;
    }

    internal ImmutableDictionary<IParameterSymbol, ComplexityVariable> ParameterVariables
    {
        get;
    }

    internal static MethodComplexityTemplate Create(
        ComplexityExpression complexity,
        MethodAnalysisContext methodAnalysisContext,
        CancellationToken cancellationToken)
    {
        _ = methodAnalysisContext ?? throw new ArgumentNullException(nameof(methodAnalysisContext));

        cancellationToken.ThrowIfCancellationRequested();

        ImmutableDictionary<IParameterSymbol, ComplexityVariable>.Builder parameterVariables =
            ImmutableDictionary.CreateBuilder<IParameterSymbol, ComplexityVariable>(ParameterSymbolComparer.Instance);

        foreach (KeyValuePair<ISymbol, ComplexityVariable> pair in methodAnalysisContext.InputSizeVariables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pair.Key is IParameterSymbol parameter)
            {
                parameterVariables[parameter] = pair.Value;
            }
        }

        return new MethodComplexityTemplate(
            complexity,
            parameterVariables.ToImmutable());
    }

    private static ImmutableDictionary<IParameterSymbol, ComplexityVariable> NormalizeParameterVariables(
        ImmutableDictionary<IParameterSymbol, ComplexityVariable> parameterVariables)
    {
        ImmutableDictionary<IParameterSymbol, ComplexityVariable>.Builder builder =
            ImmutableDictionary.CreateBuilder<IParameterSymbol, ComplexityVariable>(ParameterSymbolComparer.Instance);

        foreach (KeyValuePair<IParameterSymbol, ComplexityVariable> pair in parameterVariables)
        {
            builder[pair.Key] = pair.Value;
        }

        return builder.ToImmutable();
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

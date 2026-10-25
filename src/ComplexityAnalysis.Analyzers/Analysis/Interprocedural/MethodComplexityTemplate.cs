using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;
using ComplexityAnalysis.Analyzers.Analysis.Evidence;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class MethodComplexityTemplate
{
    internal MethodComplexityTemplate(
        ComplexityExpression complexity,
        ImmutableDictionary<IParameterSymbol, ComplexityVariable> parameterVariables,
        ComplexityEvidenceTrace? evidence = null,
        ComplexityEstimate? estimate = null)
    {
        Complexity = complexity ?? throw new ArgumentNullException(nameof(complexity));
        _ = parameterVariables ?? throw new ArgumentNullException(nameof(parameterVariables));
        ParameterVariables = NormalizeParameterVariables(parameterVariables);
        Evidence = evidence;
        Estimate = estimate;
    }

    internal ComplexityExpression Complexity
    {
        get;
    }

    internal ImmutableDictionary<IParameterSymbol, ComplexityVariable> ParameterVariables
    {
        get;
    }

    internal ComplexityEvidenceTrace? Evidence
    {
        get;
    }

    internal ComplexityEstimate? Estimate
    {
        get;
    }

    internal static MethodComplexityTemplate Create(
        ComplexityExpression complexity,
        MethodAnalysisContext methodAnalysisContext,
        ComplexityEvidenceTrace? evidence,
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
            parameterVariables.ToImmutable(),
            evidence,
            CreateEstimate(complexity, evidence));
    }

    private static ComplexityEstimate? CreateEstimate(
        ComplexityExpression complexity,
        ComplexityEvidenceTrace? evidence)
    {
        if (evidence is null || evidence.Steps.Length == 0)
        {
            return null;
        }

        ComplexityEstimate[] estimates =
        [
            .. evidence.Steps
                .Select(step => step.Estimate)
                .Where(estimate => estimate.Complexity is not UnknownComplexity)
        ];
        ComplexityGuarantee[] guarantees =
        [
            .. estimates.Select(estimate => estimate.Guarantee).Distinct()
        ];
        if (estimates.Length == 0 || guarantees.Length != 1)
        {
            return null;
        }

        ComplexityAssumption[] assumptions =
        [
            .. estimates
                .SelectMany(estimate => estimate.Assumptions)
                .Distinct()
        ];
        return guarantees[0] switch
        {
            ComplexityGuarantee.WorstCaseUpperBound => ComplexityEstimate.WorstCaseUpperBound(complexity, assumptions),
            ComplexityGuarantee.Expected => ComplexityEstimate.Expected(complexity, assumptions),
            ComplexityGuarantee.Amortized => ComplexityEstimate.Amortized(complexity, assumptions),
            _ => null,
        };
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

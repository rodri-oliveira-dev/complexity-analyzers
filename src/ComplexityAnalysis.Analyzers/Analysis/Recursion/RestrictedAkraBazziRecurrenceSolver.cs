using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RestrictedAkraBazziRecurrenceSolver
{
    private readonly int maxBracketExpansions;
    private readonly int maxBisectionIterations;
    private readonly double maxHighExponent;

    internal RestrictedAkraBazziRecurrenceSolver()
        : this(
            RecurrenceNumerics.AkraBazziMaxBracketExpansions,
            RecurrenceNumerics.AkraBazziMaxBisectionIterations,
            RecurrenceNumerics.AkraBazziMaxHighExponent)
    {
    }

    internal RestrictedAkraBazziRecurrenceSolver(
        int maxBracketExpansions,
        int maxBisectionIterations,
        double maxHighExponent)
    {
        if (maxBracketExpansions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBracketExpansions), "Bracket expansion limit must be positive.");
        }

        if (maxBisectionIterations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBisectionIterations), "Bisection iteration limit cannot be negative.");
        }

        if (double.IsNaN(maxHighExponent)
            || double.IsInfinity(maxHighExponent)
            || maxHighExponent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHighExponent), "High exponent cap must be finite and positive.");
        }

        this.maxBracketExpansions = maxBracketExpansions;
        this.maxBisectionIterations = maxBisectionIterations;
        this.maxHighExponent = maxHighExponent;
    }

    internal bool IsApplicable(RecurrenceRelation? relation)
    {
        return CheckApplicability(relation).Kind == RecurrenceApplicabilityKind.Applicable;
    }

    internal RecurrenceSolution Solve(RecurrenceRelation? relation)
    {
        return CheckApplicability(relation).ToSolution(
            relation,
            RecurrenceSolverKind.RestrictedAkraBazzi);
    }

    private RecurrenceApplicability CheckApplicability(RecurrenceRelation? relation)
    {
        if (relation is null)
        {
            return RecurrenceApplicability.Invalid();
        }

        if (!HasRestrictedAkraBazziShape(relation)
            || !RecurrencePolyLogWork.TryClassify(
                relation.NonRecursiveWork,
                relation.ComplexityVariable,
                out RecurrencePolyLogWork? localWork)
            || localWork is null)
        {
            return RecurrenceApplicability.Unsupported();
        }

        if (!TrySolveCriticalExponent(relation, out double criticalExponent))
        {
            return RecurrenceApplicability.NumericallyInconclusive();
        }

        double normalizedCriticalExponent = PolynomialExponentNormalizer.Normalize(criticalExponent);
        double diff = localWork.PolynomialDegree - normalizedCriticalExponent;
        return TollComparisonClassifier.Classify(diff) is { } theoremCase
            ? RecurrenceApplicability.Applicable(localWork, normalizedCriticalExponent, theoremCase)
            : RecurrenceApplicability.NumericallyInconclusive();
    }

    private static bool HasRestrictedAkraBazziShape(RecurrenceRelation relation)
    {
        if (relation.RecursiveTerms.Length < 2)
        {
            return false;
        }

        bool hasDistinctScale = false;
        double firstScale = 0;
        for (int index = 0; index < relation.RecursiveTerms.Length; index++)
        {
            RecurrenceTerm term = relation.RecursiveTerms[index];
            if (term.Multiplicity <= 0
                || term.Reduction.Kind != RecurrenceReductionKind.Scale
                || term.Reduction.Value <= 0
                || term.Reduction.Value >= 1)
            {
                return false;
            }

            if (index == 0)
            {
                firstScale = term.Reduction.Value;
            }
            else if (Math.Abs(term.Reduction.Value - firstScale) > RecurrenceNumerics.ComparisonEpsilon)
            {
                hasDistinctScale = true;
            }
        }

        return hasDistinctScale;
    }

    private bool TrySolveCriticalExponent(
        RecurrenceRelation relation,
        out double criticalExponent)
    {
        criticalExponent = 0;

        if (!TryEvaluateInitialCriticalExponent(relation, out bool foundInitialExponent))
        {
            return false;
        }

        return foundInitialExponent
            || (TryFindCriticalExponentBracket(
                relation,
                out double high,
                out bool foundExactExponent,
                out criticalExponent)
                && (foundExactExponent
                    || TryBisectCriticalExponent(relation, low: 0, high, out criticalExponent)));
    }

    private static bool TryEvaluateInitialCriticalExponent(
        RecurrenceRelation relation,
        out bool foundInitialExponent)
    {
        foundInitialExponent = false;

        if (!TryEvaluateShiftedCharacteristic(relation, exponent: 0, out double lowValue))
        {
            return false;
        }

        if (Math.Abs(lowValue) <= RecurrenceNumerics.ComparisonEpsilon)
        {
            foundInitialExponent = true;
            return true;
        }

        return lowValue >= 0;
    }

    private bool TryFindCriticalExponentBracket(
        RecurrenceRelation relation,
        out double high,
        out bool foundExactExponent,
        out double criticalExponent)
    {
        high = 1;
        foundExactExponent = false;
        criticalExponent = 0;

        for (int attempt = 0; attempt < maxBracketExpansions; attempt++)
        {
            if (!TryEvaluateShiftedCharacteristic(relation, high, out double highValue))
            {
                return false;
            }

            if (Math.Abs(highValue) <= RecurrenceNumerics.ComparisonEpsilon)
            {
                criticalExponent = high;
                foundExactExponent = true;
                return true;
            }

            if (highValue < 0)
            {
                return true;
            }

            if (high >= maxHighExponent)
            {
                return false;
            }

            high = Math.Min(high * 2, maxHighExponent);
        }

        return false;
    }

    private bool TryBisectCriticalExponent(
        RecurrenceRelation relation,
        double low,
        double high,
        out double criticalExponent)
    {
        criticalExponent = 0;

        for (int iteration = 0; iteration < maxBisectionIterations; iteration++)
        {
            double mid = low + ((high - low) / 2);
            if (!TryEvaluateShiftedCharacteristic(relation, mid, out double midValue))
            {
                return false;
            }

            if (Math.Abs(midValue) <= RecurrenceNumerics.ComparisonEpsilon
                || high - low <= RecurrenceNumerics.ComparisonEpsilon)
            {
                criticalExponent = mid;
                return true;
            }

            if (midValue > 0)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return false;
    }

    private static bool TryEvaluateShiftedCharacteristic(
        RecurrenceRelation relation,
        double exponent,
        out double value)
    {
        if (!TryEvaluateCharacteristic(relation, exponent, out value))
        {
            return false;
        }

        value -= 1;
        return true;
    }

    private static bool TryEvaluateCharacteristic(
        RecurrenceRelation relation,
        double exponent,
        out double value)
    {
        value = 0;
        foreach (RecurrenceTerm term in relation.RecursiveTerms)
        {
            double contribution = term.Multiplicity * Math.Pow(term.Reduction.Value, exponent);
            if (double.IsNaN(contribution) || double.IsInfinity(contribution))
            {
                return false;
            }

            value += contribution;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }
        }

        return true;
    }

}

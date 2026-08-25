using System;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class MasterTheoremRecurrenceSolver
{
    internal bool IsApplicable(RecurrenceRelation? relation)
    {
        return CheckApplicability(relation).Kind == RecurrenceApplicabilityKind.Applicable;
    }

    internal RecurrenceSolution Solve(RecurrenceRelation? relation)
    {
        return CheckApplicability(relation).ToSolution(
            relation,
            RecurrenceSolverKind.MasterTheorem);
    }

    private static RecurrenceApplicability CheckApplicability(RecurrenceRelation? relation)
    {
        if (relation is null)
        {
            return RecurrenceApplicability.Invalid();
        }

        if (relation.RecursiveTerms.Length != 1)
        {
            return RecurrenceApplicability.Unsupported();
        }

        RecurrenceTerm term = relation.RecursiveTerms[0];
        if (term.Multiplicity < 1
            || term.Reduction.Kind != RecurrenceReductionKind.Scale
            || term.Reduction.Value <= 0
            || term.Reduction.Value >= 1
            || !RecurrencePolyLogWork.TryClassify(
                relation.NonRecursiveWork,
                relation.ComplexityVariable,
                out RecurrencePolyLogWork? localWork)
            || localWork is null)
        {
            return RecurrenceApplicability.Unsupported();
        }

        double b = 1.0 / term.Reduction.Value;
        if (!IsFiniteGreaterThanOne(b))
        {
            return RecurrenceApplicability.Unsupported();
        }

        double criticalExponent = Math.Log(term.Multiplicity) / Math.Log(b);
        if (double.IsNaN(criticalExponent)
            || double.IsInfinity(criticalExponent)
            || criticalExponent < 0)
        {
            return RecurrenceApplicability.NumericallyInconclusive();
        }

        double diff = localWork.PolynomialDegree - criticalExponent;
        return TollComparisonClassifier.Classify(diff) is { } theoremCase
            ? CreateApplicableResult(term, localWork, criticalExponent, theoremCase)
            : RecurrenceApplicability.NumericallyInconclusive();
    }

    private static RecurrenceApplicability CreateApplicableResult(
        RecurrenceTerm term,
        RecurrencePolyLogWork localWork,
        double criticalExponent,
        TollComparisonCase theoremCase)
    {
        return theoremCase == TollComparisonCase.TollLarger
            && !RegularityHoldsForSupportedPolyLogToll(term.Multiplicity, term.Reduction.Value, localWork)
            ? RecurrenceApplicability.Unsupported()
            : RecurrenceApplicability.Applicable(localWork, criticalExponent, theoremCase);
    }

    private static bool RegularityHoldsForSupportedPolyLogToll(
        int multiplicity,
        double scale,
        RecurrencePolyLogWork localWork)
    {
        if (localWork.PolynomialDegree <= 0)
        {
            return false;
        }

        double ratio = multiplicity * Math.Pow(scale, localWork.PolynomialDegree);
        return !double.IsNaN(ratio)
            && !double.IsInfinity(ratio)
            && ratio < 1 - RecurrenceNumerics.ComparisonEpsilon;
    }

    private static bool IsFiniteGreaterThanOne(double value)
    {
        return !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value > 1 + RecurrenceNumerics.ComparisonEpsilon;
    }
}

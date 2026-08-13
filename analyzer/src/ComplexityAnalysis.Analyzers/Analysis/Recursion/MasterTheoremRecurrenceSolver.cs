using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class MasterTheoremRecurrenceSolver
{
    internal bool IsApplicable(RecurrenceRelation? relation)
    {
        return CheckApplicability(relation).Kind == ApplicabilityKind.Applicable;
    }

    internal RecurrenceSolution Solve(RecurrenceRelation? relation)
    {
        Applicability applicability = CheckApplicability(relation);
        if (applicability.Kind == ApplicabilityKind.Invalid)
        {
            return RecurrenceSolution.Invalid();
        }

        if (applicability.Kind == ApplicabilityKind.NumericallyInconclusive)
        {
            return RecurrenceSolution.NumericallyInconclusive();
        }

        if (applicability.Kind != ApplicabilityKind.Applicable
            || relation is null
            || applicability.LocalWork is null
            || !applicability.CriticalExponent.HasValue
            || !applicability.TheoremCase.HasValue)
        {
            return RecurrenceSolution.Unsupported();
        }

        double p = PolynomialExponentNormalizer.Normalize(applicability.CriticalExponent.Value);
        ComplexityExpression solution = CreateSolution(
            relation.ComplexityVariable,
            p,
            applicability.LocalWork,
            applicability.TheoremCase.Value);

        return RecurrenceSolution.Solved(
            solution,
            RecurrenceSolverKind.MasterTheorem,
            computedExponent: p);
    }

    private static ComplexityExpression CreateSolution(
        ComplexityVariable variable,
        double criticalExponent,
        PolyLogWork localWork,
        MasterTheoremCase theoremCase)
    {
        return theoremCase switch
        {
            MasterTheoremCase.Case1 => PolyLog(variable, criticalExponent, logExponent: 0),
            MasterTheoremCase.Case2 => PolyLog(variable, criticalExponent, localWork.LogExponent + 1),
            MasterTheoremCase.Case3 => PolyLog(variable, localWork.PolynomialDegree, localWork.LogExponent),
            _ => throw new InvalidOperationException("Unknown Master Theorem case."),
        };
    }

    private static ComplexityExpression PolyLog(
        ComplexityVariable variable,
        double polynomialDegree,
        int logExponent)
    {
        double normalizedPolynomialDegree = PolynomialExponentNormalizer.Normalize(polynomialDegree);
        return normalizedPolynomialDegree == 0 && logExponent == 0
            ? ComplexityFactory.Constant()
            : new PolynomialLogComplexity(variable, normalizedPolynomialDegree, logExponent);
    }

    private static Applicability CheckApplicability(RecurrenceRelation? relation)
    {
        if (relation is null)
        {
            return Applicability.Invalid();
        }

        if (relation.RecursiveTerms.Length != 1)
        {
            return Applicability.Unsupported();
        }

        RecurrenceTerm term = relation.RecursiveTerms[0];
        if (term.Multiplicity < 1
            || term.Reduction.Kind != RecurrenceReductionKind.Scale
            || term.Reduction.Value <= 0
            || term.Reduction.Value >= 1
            || !TryClassifySupportedLocalWork(
                relation.NonRecursiveWork,
                relation.ComplexityVariable,
                out PolyLogWork? localWork)
            || localWork is null)
        {
            return Applicability.Unsupported();
        }

        double b = 1.0 / term.Reduction.Value;
        if (!IsFiniteGreaterThanOne(b))
        {
            return Applicability.Unsupported();
        }

        double criticalExponent = Math.Log(term.Multiplicity) / Math.Log(b);
        if (double.IsNaN(criticalExponent)
            || double.IsInfinity(criticalExponent)
            || criticalExponent < 0)
        {
            return Applicability.NumericallyInconclusive();
        }

        double diff = localWork.PolynomialDegree - criticalExponent;
        return ClassifyCase(diff) is { } theoremCase
            ? CreateApplicableResult(term, localWork, criticalExponent, theoremCase)
            : Applicability.NumericallyInconclusive();
    }

    private static Applicability CreateApplicableResult(
        RecurrenceTerm term,
        PolyLogWork localWork,
        double criticalExponent,
        MasterTheoremCase theoremCase)
    {
        return theoremCase == MasterTheoremCase.Case3
            && !RegularityHoldsForSupportedPolyLogToll(term.Multiplicity, term.Reduction.Value, localWork)
            ? Applicability.Unsupported()
            : Applicability.Applicable(localWork, criticalExponent, theoremCase);
    }

    private static MasterTheoremCase? ClassifyCase(double polynomialDegreeDifference)
    {
        return polynomialDegreeDifference < -RecurrenceNumerics.PolynomialGapThreshold
            ? MasterTheoremCase.Case1
            : polynomialDegreeDifference > RecurrenceNumerics.PolynomialGapThreshold
            ? MasterTheoremCase.Case3
            : Math.Abs(polynomialDegreeDifference) <= RecurrenceNumerics.ComparisonEpsilon
            ? MasterTheoremCase.Case2
            : null;
    }

    private static bool RegularityHoldsForSupportedPolyLogToll(
        int multiplicity,
        double scale,
        PolyLogWork localWork)
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

    private static bool TryClassifySupportedLocalWork(
        ComplexityExpression localWork,
        ComplexityVariable variable,
        out PolyLogWork? polyLogWork)
    {
        polyLogWork = null;

        switch (localWork)
        {
            case ConstantComplexity:
                polyLogWork = new PolyLogWork(polynomialDegree: 0, logExponent: 0);
                return true;

            case PolynomialLogComplexity polynomialLog when polynomialLog.Variable.Equals(variable):
                polyLogWork = new PolyLogWork(
                    polynomialLog.PolynomialDegree,
                    polynomialLog.LogExponent);
                return true;

            default:
                return false;
        }
    }

    private sealed class Applicability
    {
        private Applicability(
            ApplicabilityKind kind,
            PolyLogWork? localWork,
            double? criticalExponent,
            MasterTheoremCase? theoremCase)
        {
            if (!Enum.IsDefined(typeof(ApplicabilityKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Master Theorem applicability kind.");
            }

            Kind = kind;
            LocalWork = localWork;
            CriticalExponent = criticalExponent;
            TheoremCase = theoremCase;
        }

        internal ApplicabilityKind Kind
        {
            get;
        }

        internal PolyLogWork? LocalWork
        {
            get;
        }

        internal double? CriticalExponent
        {
            get;
        }

        internal MasterTheoremCase? TheoremCase
        {
            get;
        }

        internal static Applicability Applicable(
            PolyLogWork localWork,
            double criticalExponent,
            MasterTheoremCase theoremCase)
        {
            return new Applicability(
                ApplicabilityKind.Applicable,
                localWork ?? throw new ArgumentNullException(nameof(localWork)),
                criticalExponent,
                theoremCase);
        }

        internal static Applicability Unsupported()
        {
            return new Applicability(
                ApplicabilityKind.Unsupported,
                localWork: null,
                criticalExponent: null,
                theoremCase: null);
        }

        internal static Applicability Invalid()
        {
            return new Applicability(
                ApplicabilityKind.Invalid,
                localWork: null,
                criticalExponent: null,
                theoremCase: null);
        }

        internal static Applicability NumericallyInconclusive()
        {
            return new Applicability(
                ApplicabilityKind.NumericallyInconclusive,
                localWork: null,
                criticalExponent: null,
                theoremCase: null);
        }
    }

    private sealed class PolyLogWork
    {
        internal PolyLogWork(double polynomialDegree, int logExponent)
        {
            PolynomialDegree = polynomialDegree;
            LogExponent = logExponent;
        }

        internal double PolynomialDegree
        {
            get;
        }

        internal int LogExponent
        {
            get;
        }
    }

    private enum ApplicabilityKind
    {
        Applicable,
        Unsupported,
        Invalid,
        NumericallyInconclusive,
    }

    private enum MasterTheoremCase
    {
        Case1,
        Case2,
        Case3,
    }
}

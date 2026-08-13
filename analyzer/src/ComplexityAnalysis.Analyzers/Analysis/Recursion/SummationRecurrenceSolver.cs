using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class SummationRecurrenceSolver
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

        if (applicability.Kind != ApplicabilityKind.Applicable
            || relation is null
            || applicability.LocalWork is null)
        {
            return RecurrenceSolution.Unsupported();
        }

        ComplexityExpression solution = new PolynomialLogComplexity(
            relation.ComplexityVariable,
            applicability.LocalWork.PolynomialDegree + 1,
            applicability.LocalWork.LogExponent);

        return RecurrenceSolution.Solved(
            solution,
            RecurrenceSolverKind.Summation,
            computedExponent: applicability.LocalWork.PolynomialDegree + 1);
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
        return term.Multiplicity == 1
            && term.Reduction.Kind == RecurrenceReductionKind.SubtractConstant
            && RecurrencePolyLogWork.TryClassify(
                relation.NonRecursiveWork,
                relation.ComplexityVariable,
                out RecurrencePolyLogWork? localWork)
            && localWork is not null
            ? Applicability.Applicable(localWork)
            : Applicability.Unsupported();
    }

    private sealed class Applicability
    {
        private Applicability(ApplicabilityKind kind, RecurrencePolyLogWork? localWork)
        {
            if (!Enum.IsDefined(typeof(ApplicabilityKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown summation applicability kind.");
            }

            Kind = kind;
            LocalWork = localWork;
        }

        internal ApplicabilityKind Kind
        {
            get;
        }

        internal RecurrencePolyLogWork? LocalWork
        {
            get;
        }

        internal static Applicability Applicable(RecurrencePolyLogWork localWork)
        {
            return new Applicability(
                ApplicabilityKind.Applicable,
                localWork ?? throw new ArgumentNullException(nameof(localWork)));
        }

        internal static Applicability Unsupported()
        {
            return new Applicability(ApplicabilityKind.Unsupported, localWork: null);
        }

        internal static Applicability Invalid()
        {
            return new Applicability(ApplicabilityKind.Invalid, localWork: null);
        }
    }

    private enum ApplicabilityKind
    {
        Applicable,
        Unsupported,
        Invalid,
    }
}

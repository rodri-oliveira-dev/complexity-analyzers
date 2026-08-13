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
            && TryClassifySupportedLocalWork(
                relation.NonRecursiveWork,
                relation.ComplexityVariable,
                out PolyLogWork? localWork)
            && localWork is not null
            ? Applicability.Applicable(localWork)
            : Applicability.Unsupported();
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
        private Applicability(ApplicabilityKind kind, PolyLogWork? localWork)
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

        internal PolyLogWork? LocalWork
        {
            get;
        }

        internal static Applicability Applicable(PolyLogWork localWork)
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
    }
}

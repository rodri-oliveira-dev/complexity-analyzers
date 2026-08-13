using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceApplicability
{
    private RecurrenceApplicability(
        RecurrenceApplicabilityKind kind,
        RecurrencePolyLogWork? localWork,
        double? criticalExponent,
        TollComparisonCase? theoremCase)
    {
        if (!Enum.IsDefined(typeof(RecurrenceApplicabilityKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown recurrence applicability kind.");
        }

        Kind = kind;
        LocalWork = localWork;
        CriticalExponent = criticalExponent;
        TheoremCase = theoremCase;
    }

    internal RecurrenceApplicabilityKind Kind
    {
        get;
    }

    internal RecurrencePolyLogWork? LocalWork
    {
        get;
    }

    internal double? CriticalExponent
    {
        get;
    }

    internal TollComparisonCase? TheoremCase
    {
        get;
    }

    internal static RecurrenceApplicability Applicable(
        RecurrencePolyLogWork localWork,
        double criticalExponent,
        TollComparisonCase theoremCase)
    {
        return new RecurrenceApplicability(
            RecurrenceApplicabilityKind.Applicable,
            localWork ?? throw new ArgumentNullException(nameof(localWork)),
            criticalExponent,
            theoremCase);
    }

    internal static RecurrenceApplicability Unsupported()
    {
        return Unsolved(RecurrenceApplicabilityKind.Unsupported);
    }

    internal static RecurrenceApplicability Invalid()
    {
        return Unsolved(RecurrenceApplicabilityKind.Invalid);
    }

    internal static RecurrenceApplicability NumericallyInconclusive()
    {
        return Unsolved(RecurrenceApplicabilityKind.NumericallyInconclusive);
    }

    internal RecurrenceSolution ToSolution(
        RecurrenceRelation? relation,
        RecurrenceSolverKind solverKind)
    {
        if (Kind == RecurrenceApplicabilityKind.Invalid)
        {
            return RecurrenceSolution.Invalid();
        }

        if (Kind == RecurrenceApplicabilityKind.NumericallyInconclusive)
        {
            return RecurrenceSolution.NumericallyInconclusive();
        }

        if (Kind != RecurrenceApplicabilityKind.Applicable
            || relation is null
            || LocalWork is null
            || !CriticalExponent.HasValue
            || !TheoremCase.HasValue)
        {
            return RecurrenceSolution.Unsupported();
        }

        double p = PolynomialExponentNormalizer.Normalize(CriticalExponent.Value);
        ComplexityExpression solution = RecurrencePolyLogWork.CreateSolution(
            relation.ComplexityVariable,
            p,
            LocalWork,
            TheoremCase.Value);

        return RecurrenceSolution.Solved(
            solution,
            solverKind,
            computedExponent: p);
    }

    private static RecurrenceApplicability Unsolved(RecurrenceApplicabilityKind kind)
    {
        return new RecurrenceApplicability(kind, localWork: null, criticalExponent: null, theoremCase: null);
    }
}

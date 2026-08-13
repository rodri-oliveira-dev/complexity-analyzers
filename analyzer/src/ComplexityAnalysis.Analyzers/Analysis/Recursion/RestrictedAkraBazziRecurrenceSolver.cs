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
            RecurrenceSolverKind.RestrictedAkraBazzi,
            computedExponent: p);
    }

    private static ComplexityExpression CreateSolution(
        ComplexityVariable variable,
        double criticalExponent,
        PolyLogWork localWork,
        AkraBazziCase theoremCase)
    {
        return theoremCase switch
        {
            AkraBazziCase.TollSmaller => PolyLog(variable, criticalExponent, logExponent: 0),
            AkraBazziCase.TollMatches => PolyLog(variable, criticalExponent, localWork.LogExponent + 1),
            AkraBazziCase.TollLarger => PolyLog(variable, localWork.PolynomialDegree, localWork.LogExponent),
            _ => throw new InvalidOperationException("Unknown restricted Akra-Bazzi case."),
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

    private Applicability CheckApplicability(RecurrenceRelation? relation)
    {
        if (relation is null)
        {
            return Applicability.Invalid();
        }

        if (!HasRestrictedAkraBazziShape(relation)
            || !TryClassifySupportedLocalWork(
                relation.NonRecursiveWork,
                relation.ComplexityVariable,
                out PolyLogWork? localWork)
            || localWork is null)
        {
            return Applicability.Unsupported();
        }

        if (!TrySolveCriticalExponent(relation, out double criticalExponent))
        {
            return Applicability.NumericallyInconclusive();
        }

        double normalizedCriticalExponent = PolynomialExponentNormalizer.Normalize(criticalExponent);
        double diff = localWork.PolynomialDegree - normalizedCriticalExponent;
        return ClassifyCase(diff) is { } theoremCase
            ? Applicability.Applicable(localWork, normalizedCriticalExponent, theoremCase)
            : Applicability.NumericallyInconclusive();
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

        if (!TryEvaluateCharacteristic(relation, exponent: 0, out double lowValue))
        {
            return false;
        }

        lowValue -= 1;
        if (Math.Abs(lowValue) <= RecurrenceNumerics.ComparisonEpsilon)
        {
            return true;
        }

        if (lowValue < 0)
        {
            return false;
        }

        double low = 0;
        double high = 1;
        bool bracketed = false;
        for (int attempt = 0; attempt < maxBracketExpansions; attempt++)
        {
            if (!TryEvaluateCharacteristic(relation, high, out double highValue))
            {
                return false;
            }

            highValue -= 1;
            if (Math.Abs(highValue) <= RecurrenceNumerics.ComparisonEpsilon)
            {
                criticalExponent = high;
                return true;
            }

            if (highValue < 0)
            {
                bracketed = true;
                break;
            }

            if (high >= maxHighExponent)
            {
                break;
            }

            high = Math.Min(high * 2, maxHighExponent);
        }

        if (!bracketed)
        {
            return false;
        }

        for (int iteration = 0; iteration < maxBisectionIterations; iteration++)
        {
            double mid = low + ((high - low) / 2);
            if (!TryEvaluateCharacteristic(relation, mid, out double midValue))
            {
                return false;
            }

            midValue -= 1;
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

    private static AkraBazziCase? ClassifyCase(double polynomialDegreeDifference)
    {
        return polynomialDegreeDifference < -RecurrenceNumerics.PolynomialGapThreshold
            ? AkraBazziCase.TollSmaller
            : polynomialDegreeDifference > RecurrenceNumerics.PolynomialGapThreshold
            ? AkraBazziCase.TollLarger
            : Math.Abs(polynomialDegreeDifference) <= RecurrenceNumerics.ComparisonEpsilon
            ? AkraBazziCase.TollMatches
            : null;
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
            AkraBazziCase? theoremCase)
        {
            if (!Enum.IsDefined(typeof(ApplicabilityKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown restricted Akra-Bazzi applicability kind.");
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

        internal AkraBazziCase? TheoremCase
        {
            get;
        }

        internal static Applicability Applicable(
            PolyLogWork localWork,
            double criticalExponent,
            AkraBazziCase theoremCase)
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

    private enum AkraBazziCase
    {
        TollSmaller,
        TollMatches,
        TollLarger,
    }
}

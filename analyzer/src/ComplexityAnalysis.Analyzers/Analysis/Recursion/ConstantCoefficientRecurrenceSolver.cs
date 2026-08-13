using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class ConstantCoefficientRecurrenceSolver
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
            || !applicability.DominantRoot.HasValue)
        {
            return RecurrenceSolution.Unsupported();
        }

        double normalizedRoot = ExponentialBaseNormalizer.Normalize(applicability.DominantRoot.Value);
        return RecurrenceSolution.Solved(
            ComplexityFactory.Exponential(relation.ComplexityVariable, normalizedRoot),
            RecurrenceSolverKind.ConstantCoefficient,
            computedExponent: normalizedRoot);
    }

    private static Applicability CheckApplicability(RecurrenceRelation? relation)
    {
        return relation is null
            ? Applicability.Invalid()
            : TryClassifySupportedLocalWork(relation.NonRecursiveWork, relation.ComplexityVariable)
            && TrySolveSupportedDominantRoot(relation, out double dominantRoot)
            ? Applicability.Applicable(dominantRoot)
            : Applicability.Unsupported();
    }

    private static bool TrySolveSupportedDominantRoot(
        RecurrenceRelation relation,
        out double dominantRoot)
    {
        return TrySolveSameDecrementMultiplicity(relation, out dominantRoot)
            || TrySolvePositiveSecondOrder(relation, out dominantRoot);
    }

    private static bool TrySolveSameDecrementMultiplicity(
        RecurrenceRelation relation,
        out double dominantRoot)
    {
        dominantRoot = 0;

        if (relation.RecursiveTerms.Length != 1)
        {
            return false;
        }

        RecurrenceTerm term = relation.RecursiveTerms[0];
        if (term.Multiplicity <= 1
            || term.Reduction.Kind != RecurrenceReductionKind.SubtractConstant)
        {
            return false;
        }

        dominantRoot = Math.Pow(term.Multiplicity, 1 / term.Reduction.Value);
        return IsSupportedExponentialRoot(dominantRoot);
    }

    private static bool TrySolvePositiveSecondOrder(
        RecurrenceRelation relation,
        out double dominantRoot)
    {
        dominantRoot = 0;

        if (relation.RecursiveTerms.Length != 2
            || !TryGetDecrementMultiplicity(relation, decrement: 1, out int a1)
            || !TryGetDecrementMultiplicity(relation, decrement: 2, out int a2))
        {
            return false;
        }

        if (a1 < 0 || a2 <= 0)
        {
            return false;
        }

        double discriminant = ((double)a1 * a1) + (4.0 * a2);
        if (double.IsNaN(discriminant) || double.IsInfinity(discriminant) || discriminant < 0)
        {
            return false;
        }

        dominantRoot = (a1 + Math.Sqrt(discriminant)) / 2.0;
        return IsSupportedExponentialRoot(dominantRoot);
    }

    private static bool TryGetDecrementMultiplicity(
        RecurrenceRelation relation,
        int decrement,
        out int multiplicity)
    {
        multiplicity = 0;

        foreach (RecurrenceTerm term in relation.RecursiveTerms)
        {
            if (term.Reduction.Kind != RecurrenceReductionKind.SubtractConstant)
            {
                return false;
            }

            if (Math.Abs(term.Reduction.Value - decrement) <= RecurrenceNumerics.ComparisonEpsilon)
            {
                multiplicity += term.Multiplicity;
            }
        }

        return multiplicity > 0;
    }

    private static bool IsSupportedExponentialRoot(double root)
    {
        return !double.IsNaN(root)
            && !double.IsInfinity(root)
            && root > 1 + RecurrenceNumerics.ComparisonEpsilon;
    }

    private static bool TryClassifySupportedLocalWork(
        ComplexityExpression localWork,
        ComplexityVariable variable)
    {
        return localWork switch
        {
            ConstantComplexity => true,
            PolynomialLogComplexity polynomialLog => polynomialLog.Variable.Equals(variable),
            _ => false,
        };
    }

    private sealed class Applicability
    {
        private Applicability(ApplicabilityKind kind, double? dominantRoot)
        {
            if (!Enum.IsDefined(typeof(ApplicabilityKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown constant-coefficient applicability kind.");
            }

            Kind = kind;
            DominantRoot = dominantRoot;
        }

        internal ApplicabilityKind Kind
        {
            get;
        }

        internal double? DominantRoot
        {
            get;
        }

        internal static Applicability Applicable(double dominantRoot)
        {
            return new Applicability(ApplicabilityKind.Applicable, dominantRoot);
        }

        internal static Applicability Unsupported()
        {
            return new Applicability(ApplicabilityKind.Unsupported, dominantRoot: null);
        }

        internal static Applicability Invalid()
        {
            return new Applicability(ApplicabilityKind.Invalid, dominantRoot: null);
        }
    }

    private enum ApplicabilityKind
    {
        Applicable,
        Unsupported,
        Invalid,
    }
}

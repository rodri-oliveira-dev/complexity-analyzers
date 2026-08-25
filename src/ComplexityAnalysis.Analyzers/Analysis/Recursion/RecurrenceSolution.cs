using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceSolution : IEquatable<RecurrenceSolution>
{
    private RecurrenceSolution(
        RecurrenceSolutionKind kind,
        ComplexityExpression? complexity,
        RecurrenceSolverKind solverKind,
        double? computedExponent)
    {
        if (!Enum.IsDefined(typeof(RecurrenceSolutionKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown recurrence solution kind.");
        }

        if (!Enum.IsDefined(typeof(RecurrenceSolverKind), solverKind))
        {
            throw new ArgumentOutOfRangeException(nameof(solverKind), solverKind, "Unknown recurrence solver kind.");
        }

        if (kind == RecurrenceSolutionKind.Solved)
        {
            if (complexity is null)
            {
                throw new ArgumentNullException(nameof(complexity), "Solved recurrences must include a complexity.");
            }

            if (solverKind == RecurrenceSolverKind.None)
            {
                throw new ArgumentException("Solved recurrences must identify the solver kind.", nameof(solverKind));
            }
        }
        else if (complexity is not null || solverKind != RecurrenceSolverKind.None)
        {
            throw new ArgumentException("Unsolved recurrence outcomes must not include a complexity or solver kind.", nameof(kind));
        }

        if (computedExponent.HasValue
            && (double.IsNaN(computedExponent.Value) || double.IsInfinity(computedExponent.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(computedExponent), "Computed exponent must be finite when present.");
        }

        Kind = kind;
        Complexity = complexity;
        SolverKind = solverKind;
        ComputedExponent = computedExponent;
    }

    internal RecurrenceSolutionKind Kind
    {
        get;
    }

    internal ComplexityExpression? Complexity
    {
        get;
    }

    internal RecurrenceSolverKind SolverKind
    {
        get;
    }

    internal double? ComputedExponent
    {
        get;
    }

    internal static RecurrenceSolution Solved(
        ComplexityExpression complexity,
        RecurrenceSolverKind solverKind,
        double? computedExponent = null)
    {
        return new RecurrenceSolution(
            RecurrenceSolutionKind.Solved,
            complexity,
            solverKind,
            computedExponent);
    }

    internal static RecurrenceSolution Unsupported()
    {
        return Unsolved(RecurrenceSolutionKind.Unsupported);
    }

    internal static RecurrenceSolution Invalid()
    {
        return Unsolved(RecurrenceSolutionKind.Invalid);
    }

    internal static RecurrenceSolution NumericallyInconclusive()
    {
        return Unsolved(RecurrenceSolutionKind.NumericallyInconclusive);
    }

    public bool Equals(RecurrenceSolution? other)
    {
        return other is not null
            && Kind == other.Kind
            && Equals(Complexity, other.Complexity)
            && SolverKind == other.SolverKind
            && Nullable.Equals(ComputedExponent, other.ComputedExponent);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RecurrenceSolution);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Kind.GetHashCode();
            hash = (hash * 397) ^ (Complexity?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ SolverKind.GetHashCode();
            hash = (hash * 397) ^ ComputedExponent.GetHashCode();
            return hash;
        }
    }

    private static RecurrenceSolution Unsolved(RecurrenceSolutionKind kind)
    {
        return new RecurrenceSolution(kind, null, RecurrenceSolverKind.None, null);
    }
}

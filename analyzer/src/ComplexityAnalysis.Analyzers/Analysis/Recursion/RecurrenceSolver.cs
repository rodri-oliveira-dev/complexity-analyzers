using System;
using System.Collections.Immutable;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceSolver
{
    private readonly ImmutableArray<Func<RecurrenceRelation?, RecurrenceSolution>> solvers;

    internal RecurrenceSolver()
    {
        solvers = ImmutableArray.Create<Func<RecurrenceRelation?, RecurrenceSolution>>(
            new SummationRecurrenceSolver().Solve,
            new ConstantCoefficientRecurrenceSolver().Solve,
            new MasterTheoremRecurrenceSolver().Solve,
            new RestrictedAkraBazziRecurrenceSolver().Solve);
    }

    internal RecurrenceSolution Solve(RecurrenceRelation? relation)
    {
        if (relation is null)
        {
            return RecurrenceSolution.Invalid();
        }

        foreach (Func<RecurrenceRelation?, RecurrenceSolution> solver in solvers)
        {
            RecurrenceSolution solution = solver(relation);
            if (solution.Kind != RecurrenceSolutionKind.Unsupported)
            {
                return solution;
            }
        }

        return RecurrenceSolution.Unsupported();
    }
}

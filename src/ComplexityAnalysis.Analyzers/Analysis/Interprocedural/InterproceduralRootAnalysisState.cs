using System;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal sealed class InterproceduralRootAnalysisState
{
    private InterproceduralRootAnalysisState(
        AnalysisBudget budget,
        int currentDepth,
        RootExpansionCounter expansionCounter,
        ImmutableHashSet<MethodSymbolKey> activeCallPath,
        CancellationToken cancellationToken)
    {
        Budget = budget;
        CurrentDepth = currentDepth;
        ExpansionCounter = expansionCounter ?? throw new ArgumentNullException(nameof(expansionCounter));
        ActiveCallPath = activeCallPath ?? throw new ArgumentNullException(nameof(activeCallPath));
        CancellationToken = cancellationToken;
    }

    internal AnalysisBudget Budget
    {
        get;
    }

    internal int CurrentDepth
    {
        get;
    }

    internal int ExpandedMethodCount
        => ExpansionCounter.Count;

    internal CancellationToken CancellationToken
    {
        get;
    }

    private ImmutableHashSet<MethodSymbolKey> ActiveCallPath
    {
        get;
    }

    private RootExpansionCounter ExpansionCounter
    {
        get;
    }

    internal static InterproceduralRootAnalysisState Create(
        IMethodSymbol rootMethodSymbol,
        AnalysisBudget budget,
        CancellationToken cancellationToken)
    {
        _ = rootMethodSymbol ?? throw new ArgumentNullException(nameof(rootMethodSymbol));
        _ = budget ?? throw new ArgumentNullException(nameof(budget));

        cancellationToken.ThrowIfCancellationRequested();

        return new InterproceduralRootAnalysisState(
            budget,
            currentDepth: 0,
            new RootExpansionCounter(),
            ImmutableHashSet.Create(MethodSymbolKey.Comparer, MethodSymbolKey.Create(rootMethodSymbol)),
            cancellationToken);
    }

    internal bool ContainsActiveMethod(IMethodSymbol methodSymbol)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        CancellationToken.ThrowIfCancellationRequested();

        return ActiveCallPath.Contains(MethodSymbolKey.Create(methodSymbol));
    }

    internal bool TryEnterMethod(
        IMethodSymbol methodSymbol,
        out InterproceduralRootAnalysisState nextState,
        out InterproceduralAnalysisResult boundaryResult)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        CancellationToken.ThrowIfCancellationRequested();

        MethodSymbolKey methodKey = MethodSymbolKey.Create(methodSymbol);
        if (ActiveCallPath.Contains(methodKey))
        {
            nextState = this;
            boundaryResult = InterproceduralAnalysisResult.CycleBoundary("The method is already active in the current root call path.");
            return false;
        }

        if (CurrentDepth >= Budget.MaximumCallDepth)
        {
            nextState = this;
            boundaryResult = InterproceduralAnalysisResult.BudgetExceeded("Maximum call depth was reached.");
            return false;
        }

        if (!ExpansionCounter.TryReserve(Budget.MaximumMethodsPerRootAnalysis))
        {
            nextState = this;
            boundaryResult = InterproceduralAnalysisResult.BudgetExceeded("Maximum methods per root analysis was reached.");
            return false;
        }

        nextState = new InterproceduralRootAnalysisState(
            Budget,
            CurrentDepth + 1,
            ExpansionCounter,
            ActiveCallPath.Add(methodKey),
            CancellationToken);
        boundaryResult = InterproceduralAnalysisResult.Unknown(string.Empty);
        return true;
    }

    internal InterproceduralRootAnalysisState ExitMethod(IMethodSymbol methodSymbol)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        CancellationToken.ThrowIfCancellationRequested();

        MethodSymbolKey methodKey = MethodSymbolKey.Create(methodSymbol);
        return ActiveCallPath.Contains(methodKey)
            ? new InterproceduralRootAnalysisState(
                Budget,
                Math.Max(0, CurrentDepth - 1),
                ExpansionCounter,
                ActiveCallPath.Remove(methodKey),
                CancellationToken)
            : this;
    }

    private sealed class RootExpansionCounter
    {
        private int count;

        internal int Count
            => Volatile.Read(ref count);

        internal bool TryReserve(int maximumCount)
        {
            while (true)
            {
                int current = Volatile.Read(ref count);
                if (current >= maximumCount)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref count, current + 1, current) == current)
                {
                    return true;
                }
            }
        }
    }
}

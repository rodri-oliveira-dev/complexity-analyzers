using System;
using System.Collections.Immutable;
using System.Linq;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecursiveExecutionPath : IEquatable<RecursiveExecutionPath>
{
    internal RecursiveExecutionPath(ImmutableArray<RecursiveCallShape> recursiveCalls)
    {
        if (recursiveCalls.IsDefault)
        {
            throw new ArgumentException("Recursive calls cannot be default.", nameof(recursiveCalls));
        }

        if (recursiveCalls.Any(call => call is null))
        {
            throw new ArgumentException("Recursive calls cannot contain null entries.", nameof(recursiveCalls));
        }

        RecursiveCalls = recursiveCalls;
    }

    internal ImmutableArray<RecursiveCallShape> RecursiveCalls
    {
        get;
    }

    internal int RecursiveCallCount => RecursiveCalls.Length;

    public bool Equals(RecursiveExecutionPath? other)
    {
        return other is not null && RecursiveCalls.SequenceEqual(other.RecursiveCalls);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RecursiveExecutionPath);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (RecursiveCallShape call in RecursiveCalls)
            {
                hash = (hash * 397) ^ call.GetHashCode();
            }

            return hash;
        }
    }
}

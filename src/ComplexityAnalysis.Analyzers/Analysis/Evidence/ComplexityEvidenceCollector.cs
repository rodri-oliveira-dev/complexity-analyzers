using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.Evidence;

internal sealed class ComplexityEvidenceCollector
{
    internal const int DefaultMaximumDepth = 8;
    internal const int DefaultMaximumSteps = 64;
    internal const int HardMaximumDepth = 16;
    internal const int HardMaximumSteps = 256;

    private readonly List<ComplexityEvidenceStep> steps = [];
    private readonly Stack<int> scopeStarts = new();
    private int currentDepth;

    internal ComplexityEvidenceCollector(
        SyntaxNode root,
        int maximumDepth = DefaultMaximumDepth,
        int maximumSteps = DefaultMaximumSteps)
    {
        _ = root ?? throw new ArgumentNullException(nameof(root));
        if (maximumDepth is < 0 or > HardMaximumDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        }

        if (maximumSteps is <= 0 or > HardMaximumSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSteps));
        }

        MaximumDepth = maximumDepth;
        MaximumSteps = maximumSteps;
        scopeStarts.Push(root.SpanStart);
    }

    internal int MaximumDepth
    {
        get;
    }

    internal int MaximumSteps
    {
        get;
    }

    internal bool IsTruncated
    {
        get;
        private set;
    }

    internal Scope EnterScope(SyntaxNode node)
    {
        _ = node ?? throw new ArgumentNullException(nameof(node));

        currentDepth++;
        scopeStarts.Push(node.SpanStart);
        if (currentDepth > MaximumDepth)
        {
            IsTruncated = true;
        }

        return new Scope(this);
    }

    internal void Record(
        ComplexityEvidenceKind kind,
        ComplexityEvidenceOrigin origin,
        string operation,
        SyntaxNode node,
        ComplexityEstimate estimate,
        ComplexityOperation? composition,
        ComplexityUnknownReason unknownReason,
        CancellationToken cancellationToken)
    {
        _ = operation ?? throw new ArgumentNullException(nameof(operation));
        _ = node ?? throw new ArgumentNullException(nameof(node));
        _ = estimate ?? throw new ArgumentNullException(nameof(estimate));

        cancellationToken.ThrowIfCancellationRequested();

        if (currentDepth > MaximumDepth || steps.Count >= MaximumSteps)
        {
            IsTruncated = true;
            return;
        }

        int relativeStart = Math.Max(0, node.SpanStart - scopeStarts.Peek());
        ImmutableArray<string> dimensions = GetDimensions(estimate.Complexity);
        string identity = CreateIdentity(
            steps.Count,
            kind,
            currentDepth,
            relativeStart,
            node.Span.Length,
            operation);

        steps.Add(new ComplexityEvidenceStep(
            identity,
            kind,
            origin,
            operation,
            new ComplexityEvidenceLocation(relativeStart, node.Span.Length),
            currentDepth,
            dimensions,
            estimate,
            composition,
            unknownReason));
    }

    internal void Replay(
        ComplexityEvidenceTrace trace,
        CancellationToken cancellationToken)
    {
        _ = trace ?? throw new ArgumentNullException(nameof(trace));

        if (trace.IsTruncated)
        {
            IsTruncated = true;
        }

        foreach (ComplexityEvidenceStep step in trace.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int depth = currentDepth + step.Depth;
            if (depth > MaximumDepth || steps.Count >= MaximumSteps)
            {
                IsTruncated = true;
                break;
            }

            steps.Add(new ComplexityEvidenceStep(
                CreateIdentity(
                    steps.Count,
                    step.Kind,
                    depth,
                    step.Location.RelativeStart,
                    step.Location.Length,
                    step.Operation),
                step.Kind,
                step.Origin,
                step.Operation,
                step.Location,
                depth,
                step.Dimensions,
                step.Estimate,
                step.Composition,
                step.UnknownReason));
        }
    }

    internal ComplexityEvidenceTrace CreateTrace(ComplexityExpression complexity)
    {
        _ = complexity ?? throw new ArgumentNullException(nameof(complexity));

        return new ComplexityEvidenceTrace(
            complexity,
            steps.ToImmutableArray(),
            IsTruncated,
            MaximumDepth,
            MaximumSteps);
    }

    private static ImmutableArray<string> GetDimensions(ComplexityExpression complexity)
    {
        HashSet<string> dimensions = new(StringComparer.Ordinal);
        CollectDimensions(complexity, dimensions);
        return dimensions.OrderBy(dimension => dimension, StringComparer.Ordinal).ToImmutableArray();
    }

    private static string CreateIdentity(
        int index,
        ComplexityEvidenceKind kind,
        int depth,
        int relativeStart,
        int length,
        string operation)
    {
        return string.Join(
            ":",
            index.ToString("D4", CultureInfo.InvariantCulture),
            kind.ToString(),
            depth.ToString(CultureInfo.InvariantCulture),
            relativeStart.ToString(CultureInfo.InvariantCulture),
            length.ToString(CultureInfo.InvariantCulture),
            operation);
    }

    private static void CollectDimensions(
        ComplexityExpression complexity,
        ISet<string> dimensions)
    {
        switch (complexity)
        {
            case PolynomialLogComplexity polynomialLog:
                _ = dimensions.Add(polynomialLog.Variable.Name);
                break;
            case ExponentialComplexity exponential:
                _ = dimensions.Add(exponential.Variable.Name);
                break;
            case FactorialComplexity factorial:
                _ = dimensions.Add(factorial.Variable.Name);
                break;
            case CompositeComplexity composite:
                CollectDimensions(composite.Left, dimensions);
                CollectDimensions(composite.Right, dimensions);
                break;
            default:
                break;
        }
    }

    private void ExitScope()
    {
        _ = scopeStarts.Pop();
        currentDepth--;
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly ComplexityEvidenceCollector collector;

        internal Scope(ComplexityEvidenceCollector collector)
        {
            this.collector = collector;
        }

        public void Dispose()
        {
            collector.ExitScope();
        }
    }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis.Recursion;

internal sealed class RecurrenceExtractor
{
    internal RecurrenceExtractionResult Extract(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        cancellationToken.ThrowIfCancellationRequested();

        if (!ExecutableMember.TryCreateOrdinaryMethod(
            methodDeclaration,
            semanticModel,
            cancellationToken,
            out ExecutableMember? member)
            || member is null)
        {
            throw new InvalidOperationException("The method declaration must resolve to a method symbol.");
        }

        MethodAnalysisContext context = MethodAnalysisContext.Create(
            semanticModel,
            member.Symbol,
            cancellationToken);

        return Extract(member, context);
    }

    internal RecurrenceExtractionResult Extract(
        ExecutableMember member,
        MethodAnalysisContext context)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = context ?? throw new ArgumentNullException(nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        RecursiveCallAnalysisResult callAnalysis = new RecursiveCallAnalyzer().Analyze(
            member,
            context);
        if (!callAnalysis.IsSupported)
        {
            return RecurrenceExtractionResult.Unsupported(
                callAnalysis.UnsupportedReason ?? "The recursive call shape is unsupported.");
        }

        if (!callAnalysis.HasDirectRecursiveCalls)
        {
            return RecurrenceExtractionResult.Unsupported("The method does not contain direct recursive calls.");
        }

        if (!TryIdentifyRecursiveDimension(callAnalysis, out RecursiveDimension? dimension, out string? reason)
            || dimension is null)
        {
            return RecurrenceExtractionResult.Unsupported(reason ?? "The recursive input variable is unsupported.");
        }

        if (!HasBaseCaseEvidence(callAnalysis, dimension))
        {
            return RecurrenceExtractionResult.Unsupported("Missing base case evidence for the recursive input variable.");
        }

        if (!TrySelectWorstCaseTerms(
            callAnalysis.ExecutionPaths,
            dimension,
            out ImmutableArray<RecurrenceTerm> recursiveTerms,
            out reason))
        {
            return RecurrenceExtractionResult.Unsupported(reason ?? "The recursive execution paths are unsupported.");
        }

        ComplexityExpression localWork = AnalyzeLocalWork(member, context);
        return localWork is UnknownComplexity
            ? RecurrenceExtractionResult.Unknown("The non-recursive local work is unknown.")
            : RecurrenceExtractionResult.Extracted(
                new RecurrenceRelation(
                    dimension.Variable,
                    recursiveTerms,
                    localWork));
    }

    internal RecurrenceExtractionResult Extract(
        MethodDeclarationSyntax methodDeclaration,
        MethodAnalysisContext context)
    {
        _ = methodDeclaration ?? throw new ArgumentNullException(nameof(methodDeclaration));
        _ = context ?? throw new ArgumentNullException(nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        ExecutableMember member = ExecutableMember.CreateOrdinaryMethod(methodDeclaration, context.MethodSymbol);
        return Extract(member, context);
    }

    private static ComplexityExpression AnalyzeLocalWork(
        ExecutableMember member,
        MethodAnalysisContext context)
    {
        MethodAnalysisContext localWorkContext = context.WithDirectRecursiveInvocationsAsConstant();
        return member.Body.Block is not null
            ? new MethodComplexityExtractor().AnalyzeBlock(member.Body.Block, localWorkContext)
            : member.Body.Expression is null
                ? ComplexityFactory.Unknown()
                : new BasicOperationAnalyzer(localWorkContext).AnalyzeExpression(member.Body.Expression);
    }

    private static bool TryIdentifyRecursiveDimension(
        RecursiveCallAnalysisResult callAnalysis,
        out RecursiveDimension? dimension,
        out string? reason)
    {
        dimension = null;
        reason = null;

        foreach (RecursiveExecutionPath path in callAnalysis.ExecutionPaths)
        {
            foreach (RecursiveCallShape call in path.RecursiveCalls)
            {
                ImmutableArray<RecursiveArgumentRelation> reducingRelations =
                    call.ReducingArgumentRelations;
                if (reducingRelations.Length == 0)
                {
                    reason = "The recursive argument is not reducing.";
                    return false;
                }

                if (reducingRelations.Length > 1)
                {
                    reason = "The recursive argument dimensions are incompatible.";
                    return false;
                }

                RecursiveArgumentRelation reducingRelation = reducingRelations[0];
                RecursiveDimension current = new(
                    reducingRelation.Parameter,
                    reducingRelation.Variable);
                if (dimension is null)
                {
                    dimension = current;
                }
                else if (!dimension.Equals(current))
                {
                    reason = "The recursive argument dimensions are incompatible.";
                    return false;
                }

                if (!AllOtherInputDimensionsAreStable(call, current))
                {
                    reason = "The recursive argument dimensions are incompatible.";
                    return false;
                }
            }
        }

        return dimension is not null;
    }

    private static bool AllOtherInputDimensionsAreStable(
        RecursiveCallShape call,
        RecursiveDimension recursiveDimension)
    {
        foreach (RecursiveArgumentRelation relation in call.ArgumentRelations)
        {
            if (SymbolEqualityComparer.Default.Equals(relation.Parameter, recursiveDimension.Parameter))
            {
                continue;
            }

            if (relation.Kind != RecursiveArgumentRelationKind.Unchanged)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasBaseCaseEvidence(
        RecursiveCallAnalysisResult callAnalysis,
        RecursiveDimension dimension)
    {
        return callAnalysis.BaseCaseEvidence.Any(evidence =>
            SymbolEqualityComparer.Default.Equals(evidence.Parameter, dimension.Parameter)
            && evidence.Variable.Equals(dimension.Variable));
    }

    private static bool TrySelectWorstCaseTerms(
        ImmutableArray<RecursiveExecutionPath> paths,
        RecursiveDimension dimension,
        out ImmutableArray<RecurrenceTerm> selectedTerms,
        out string? reason)
    {
        selectedTerms = ImmutableArray<RecurrenceTerm>.Empty;
        reason = null;

        foreach (RecursiveExecutionPath path in paths)
        {
            if (!TryCreateTerms(path, dimension, out ImmutableArray<RecurrenceTerm> pathTerms, out reason))
            {
                return false;
            }

            if (selectedTerms.IsEmpty || CompareTermSets(pathTerms, selectedTerms) > 0)
            {
                selectedTerms = pathTerms;
            }
        }

        if (selectedTerms.IsEmpty)
        {
            reason = "The recursive execution paths do not contain supported recurrence terms.";
            return false;
        }

        return true;
    }

    private static bool TryCreateTerms(
        RecursiveExecutionPath path,
        RecursiveDimension dimension,
        out ImmutableArray<RecurrenceTerm> terms,
        out string? reason)
    {
        ImmutableArray<RecurrenceReduction>.Builder reductions =
            ImmutableArray.CreateBuilder<RecurrenceReduction>();
        terms = ImmutableArray<RecurrenceTerm>.Empty;
        reason = null;

        foreach (RecursiveCallShape call in path.RecursiveCalls)
        {
            RecursiveArgumentRelation? relation = call.ArgumentRelations.FirstOrDefault(argumentRelation =>
                SymbolEqualityComparer.Default.Equals(argumentRelation.Parameter, dimension.Parameter));
            if (relation is null || !relation.IsReducing || relation.Reduction is null)
            {
                reason = "The recursive argument is not reducing.";
                return false;
            }

            reductions.Add(relation.Reduction);
        }

        terms = GroupReductions(reductions.ToImmutable());
        return true;
    }

    private static ImmutableArray<RecurrenceTerm> GroupReductions(
        ImmutableArray<RecurrenceReduction> reductions)
    {
        ImmutableArray<RecurrenceTerm>.Builder terms =
            ImmutableArray.CreateBuilder<RecurrenceTerm>();

        foreach (RecurrenceReduction reduction in SortReductionsForDeterminism(reductions))
        {
            int existingIndex = FindReductionIndex(terms, reduction);
            if (existingIndex >= 0)
            {
                RecurrenceTerm existing = terms[existingIndex];
                terms[existingIndex] = new RecurrenceTerm(existing.Multiplicity + 1, existing.Reduction);
            }
            else
            {
                terms.Add(new RecurrenceTerm(1, reduction));
            }
        }

        return terms.ToImmutable();
    }

    private static ImmutableArray<RecurrenceReduction> SortReductionsForDeterminism(
        ImmutableArray<RecurrenceReduction> reductions)
    {
        return reductions
            .OrderBy(reduction => reduction.Kind)
            .ThenBy(reduction => reduction.Value)
            .ToImmutableArray();
    }

    private static int FindReductionIndex(
        ImmutableArray<RecurrenceTerm>.Builder terms,
        RecurrenceReduction reduction)
    {
        for (int index = 0; index < terms.Count; index++)
        {
            if (terms[index].Reduction.Equals(reduction))
            {
                return index;
            }
        }

        return -1;
    }

    private static int CompareTermSets(
        ImmutableArray<RecurrenceTerm> left,
        ImmutableArray<RecurrenceTerm> right)
    {
        int leftMultiplicity = left.Sum(term => term.Multiplicity);
        int rightMultiplicity = right.Sum(term => term.Multiplicity);
        if (leftMultiplicity != rightMultiplicity)
        {
            return leftMultiplicity.CompareTo(rightMultiplicity);
        }

        ImmutableArray<RecurrenceReduction> leftReductions = ExpandReductionsWorstFirst(left);
        ImmutableArray<RecurrenceReduction> rightReductions = ExpandReductionsWorstFirst(right);
        for (int index = 0; index < leftReductions.Length && index < rightReductions.Length; index++)
        {
            int comparison = CompareReductionWorstCase(leftReductions[index], rightReductions[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftReductions.Length.CompareTo(rightReductions.Length);
    }

    private static ImmutableArray<RecurrenceReduction> ExpandReductionsWorstFirst(
        ImmutableArray<RecurrenceTerm> terms)
    {
        return terms
            .SelectMany(term => Enumerable.Repeat(term.Reduction, term.Multiplicity))
            .OrderByDescending(ReductionWorstCaseRank)
            .ThenBy(reduction => reduction.Kind == RecurrenceReductionKind.SubtractConstant
                ? reduction.Value
                : -reduction.Value)
            .ToImmutableArray();
    }

    private static int ReductionWorstCaseRank(RecurrenceReduction reduction)
    {
        return reduction.Kind == RecurrenceReductionKind.SubtractConstant ? 1 : 0;
    }

    private static int CompareReductionWorstCase(
        RecurrenceReduction left,
        RecurrenceReduction right)
    {
        int leftRank = ReductionWorstCaseRank(left);
        int rightRank = ReductionWorstCaseRank(right);
        return leftRank != rightRank
            ? leftRank.CompareTo(rightRank)
            : left.Kind == RecurrenceReductionKind.SubtractConstant
            ? right.Value.CompareTo(left.Value)
            : left.Value.CompareTo(right.Value);
    }

    private sealed class RecursiveDimension : IEquatable<RecursiveDimension>
    {
        internal RecursiveDimension(
            IParameterSymbol parameter,
            ComplexityVariable variable)
        {
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            Variable = variable ?? throw new ArgumentNullException(nameof(variable));
        }

        internal IParameterSymbol Parameter
        {
            get;
        }

        internal ComplexityVariable Variable
        {
            get;
        }

        public bool Equals(RecursiveDimension? other)
        {
            return other is not null
                && SymbolEqualityComparer.Default.Equals(Parameter, other.Parameter)
                && Variable.Equals(other.Variable);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as RecursiveDimension);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SymbolEqualityComparer.Default.GetHashCode(Parameter) * 397) ^ Variable.GetHashCode();
            }
        }
    }
}

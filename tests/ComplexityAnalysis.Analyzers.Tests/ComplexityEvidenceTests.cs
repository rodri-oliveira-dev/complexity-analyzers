using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Evidence;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityEvidenceTests
{
    [Fact]
    public void Vertical_slice_records_loop_known_operation_and_source_call_composition()
    {
        MethodFacts facts = CreateFacts(
            "M",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> values, int[] items) => Helper(values, items);

                private bool Helper(List<int> values, int[] items)
                {
                    for (var index = 0; index < items.Length; index++)
                    {
                        if (values.Contains(items[index]))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            """);

        ComplexityAnalysisWithEvidenceResult result = Analyze(facts);

        Assert.Equal("O(m · n)", result.Complexity.ToBigONotation());
        ComplexityEvidenceStep loop = Assert.Single(result.Evidence.Steps.Where(step =>
            step.Kind == ComplexityEvidenceKind.Loop));
        ComplexityEvidenceStep knownOperation = Assert.Single(result.Evidence.Steps.Where(step =>
            step.Kind == ComplexityEvidenceKind.KnownOperation));
        ComplexityEvidenceStep sourceCall = Assert.Single(result.Evidence.Steps.Where(step =>
            step.Kind == ComplexityEvidenceKind.SourceCall));

        Assert.Equal("for-loop", loop.Operation);
        Assert.Equal("O(m)", loop.Estimate.Complexity.ToBigONotation());
        Assert.Equal(["m"], loop.Dimensions.ToArray());
        Assert.Equal(ComplexityOperation.Nested, loop.Composition);
        Assert.Equal("list-contains", knownOperation.Operation);
        Assert.Equal("O(n)", knownOperation.Estimate.Complexity.ToBigONotation());
        Assert.Contains(
            knownOperation.Estimate.Assumptions,
            assumption => assumption.Kind == ComplexityAssumptionKind.ElementOperationCost);
        Assert.Equal("O(m · n)", sourceCall.Estimate.Complexity.ToBigONotation());
        Assert.Equal(["m", "n"], sourceCall.Dimensions.ToArray());
        Assert.Equal(ComplexityOperation.Sequential, sourceCall.Composition);
        Assert.True(knownOperation.Depth > loop.Depth);
        Assert.Equal(ComplexityUnknownReason.None, result.Evidence.UnknownReason);
    }

    [Fact]
    public void Known_hash_lookup_evidence_retains_expected_guarantee_and_assumptions()
    {
        MethodFacts facts = CreateFacts(
            "M",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(HashSet<int> values, int value) => values.Contains(value);
            }
            """);

        ComplexityAnalysisWithEvidenceResult result = Analyze(facts);
        ComplexityEvidenceStep evidence = Assert.Single(result.Evidence.Steps);

        Assert.Equal("O(1)", result.Complexity.ToBigONotation());
        Assert.Equal(ComplexityGuarantee.Expected, evidence.Estimate.Guarantee);
        Assert.Contains(
            evidence.Estimate.Assumptions,
            assumption => assumption.Kind == ComplexityAssumptionKind.HashDistribution);
        Assert.Contains(
            evidence.Estimate.Assumptions,
            assumption => assumption.Kind == ComplexityAssumptionKind.ElementOperationCost);
    }

    [Fact]
    public void Unknown_loop_retains_typed_reason_without_file_path()
    {
        MethodFacts facts = CreateFacts(
            "M",
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var index = 0; index < count; index++)
                    {
                        index = 0;
                    }
                }
            }
            """);

        ComplexityAnalysisWithEvidenceResult result = Analyze(facts);
        ComplexityEvidenceStep evidence = Assert.Single(result.Evidence.Steps);
        string snapshot = Assert.Single(result.Evidence.CreateDeterministicSnapshot());

        Assert.Equal("Unknown", result.Complexity.ToBigONotation());
        Assert.Equal(ComplexityUnknownReason.UnprovenLoopBound, result.Evidence.UnknownReason);
        Assert.Equal(ComplexityUnknownReason.UnprovenLoopBound, evidence.UnknownReason);
        Assert.True(evidence.Location.RelativeStart >= 0);
        Assert.DoesNotContain(".cs", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_size_and_depth_are_bounded_without_changing_complexity()
    {
        MethodFacts facts = CreateFacts(
            "M",
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    foreach (var outer in values)
                    {
                        foreach (var inner in values)
                        {
                            var value = outer + inner;
                        }
                    }

                    foreach (var value in values)
                    {
                        var copy = value;
                    }
                }
            }
            """);

        ComplexityAnalysisWithEvidenceResult result = Analyze(
            facts,
            maximumDepth: 0,
            maximumSteps: 1);

        Assert.Equal("O(n²)", result.Complexity.ToBigONotation());
        Assert.True(result.Evidence.IsTruncated);
        _ = Assert.Single(result.Evidence.Steps);
        Assert.All(result.Evidence.Steps, step => Assert.InRange(step.Depth, 0, 0));
        Assert.Equal(1, result.Evidence.MaximumSteps);
        Assert.Equal(0, result.Evidence.MaximumDepth);
    }

    [Fact]
    public void Evidence_limits_reject_unbounded_values()
    {
        MethodFacts facts = CreateFacts("M", "public sealed class Sample { void M() { } }");

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Analyze(
            facts,
            ComplexityEvidenceCollector.HardMaximumDepth + 1,
            maximumSteps: 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Analyze(
            facts,
            maximumDepth: 1,
            ComplexityEvidenceCollector.HardMaximumSteps + 1));
    }

    [Fact]
    public void Unknown_after_evidence_limit_retains_limit_reason()
    {
        MethodFacts facts = CreateFacts(
            "M",
            """
            public sealed class Sample
            {
                void M(int[] values, int count)
                {
                    foreach (var value in values)
                    {
                    }

                    for (var index = 0; index < count; index++)
                    {
                        index = 0;
                    }
                }
            }
            """);

        ComplexityAnalysisWithEvidenceResult result = Analyze(
            facts,
            maximumDepth: ComplexityEvidenceCollector.DefaultMaximumDepth,
            maximumSteps: 1);

        Assert.Equal("Unknown", result.Complexity.ToBigONotation());
        Assert.True(result.Evidence.IsTruncated);
        Assert.Equal(ComplexityUnknownReason.EvidenceLimitExceeded, result.Evidence.UnknownReason);
    }

    [Fact]
    public async Task Concurrent_evidence_snapshots_are_deterministic()
    {
        MethodFacts facts = CreateFacts(
            "M",
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> values, int[] items) => Helper(values, items);

                private bool Helper(List<int> values, int[] items)
                {
                    foreach (var item in items)
                    {
                        if (values.Contains(item))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            """);

        Task<ImmutableArray<string>>[] tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Analyze(facts).Evidence.CreateDeterministicSnapshot()))
            .ToArray();
        ImmutableArray<string>[] snapshots = await Task.WhenAll(tasks);

        Assert.All(snapshots, snapshot => Assert.Equal(snapshots[0].ToArray(), snapshot.ToArray()));
        Assert.Equal(
            snapshots[0].Select(value => value.Split('|')[0]).Distinct(StringComparer.Ordinal).Count(),
            snapshots[0].Length);
    }

    [Fact]
    public void Cancellation_is_observed_before_evidence_collection()
    {
        MethodFacts facts = CreateFacts(
            "M",
            "public sealed class Sample { void M(int[] values) { foreach (var value in values) { } } }");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            MethodComplexityExtractor.AnalyzeMethodWithEvidence(
                facts.Method,
                facts.SemanticModel,
                cancellationTokenSource.Token));
    }

    private static ComplexityAnalysisWithEvidenceResult Analyze(
        MethodFacts facts,
        int maximumDepth = ComplexityEvidenceCollector.DefaultMaximumDepth,
        int maximumSteps = ComplexityEvidenceCollector.DefaultMaximumSteps)
    {
        return MethodComplexityExtractor.AnalyzeMethodWithEvidence(
            facts.Method,
            facts.SemanticModel,
            CancellationToken.None,
            maximumDepth,
            maximumSteps);
    }

    private static MethodFacts CreateFacts(string methodName, string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "ComplexityEvidenceTests",
            [syntaxTree],
            BasicReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        MethodDeclarationSyntax method = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(candidate => StringComparer.Ordinal.Equals(
                methodName,
                candidate.Identifier.ValueText));

        return new MethodFacts(compilation.GetSemanticModel(syntaxTree), method);
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    }
        = CreateTrustedPlatformReferences();

    private static ImmutableArray<MetadataReference> CreateTrustedPlatformReferences()
    {
        string trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? string.Empty;

        return
        [
            .. trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record MethodFacts(
        SemanticModel SemanticModel,
        MethodDeclarationSyntax Method);
}

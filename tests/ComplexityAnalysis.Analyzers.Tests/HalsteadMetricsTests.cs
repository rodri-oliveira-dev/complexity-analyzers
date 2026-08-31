using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class HalsteadMetricsTests
{
    [Fact]
    public void Metrics_follow_documented_formulas_for_hand_verifiable_counts()
    {
        HalsteadMetrics metrics = HalsteadMetrics.FromPrimitiveCounts(
            distinctOperatorCount: 4,
            distinctOperandCount: 4,
            totalOperatorCount: 8,
            totalOperandCount: 12);

        Assert.Equal(8, metrics.Vocabulary);
        Assert.Equal(20, metrics.Length);
        Assert.Equal(16.0, metrics.CalculatedLength, precision: 12);
        Assert.Equal(60.0, metrics.Volume, precision: 12);
        Assert.Equal(6.0, metrics.Difficulty, precision: 12);
        Assert.Equal(360.0, metrics.Effort, precision: 12);
        Assert.Equal(20.0, metrics.EstimatedImplementationTime, precision: 12);
        Assert.Equal(0.02, metrics.EstimatedDeliveredBugs, precision: 12);
    }

    [Fact]
    public void Metrics_follow_documented_formulas_for_non_power_of_two_vocabulary()
    {
        HalsteadMetrics metrics = HalsteadMetrics.FromPrimitiveCounts(
            distinctOperatorCount: 2,
            distinctOperandCount: 3,
            totalOperatorCount: 4,
            totalOperandCount: 5);

        Assert.Equal(5, metrics.Vocabulary);
        Assert.Equal(9, metrics.Length);
        Assert.Equal(6.754887502163469, metrics.CalculatedLength, precision: 12);
        Assert.Equal(20.897352853986263, metrics.Volume, precision: 12);
        Assert.Equal(1.6666666666666667, metrics.Difficulty, precision: 12);
        Assert.Equal(34.82892142331044, metrics.Effort, precision: 12);
        Assert.Equal(1.9349400790728023, metrics.EstimatedImplementationTime, precision: 12);
        Assert.Equal(0.006965784284662088, metrics.EstimatedDeliveredBugs, precision: 12);
    }

    [Fact]
    public void Empty_counts_produce_zero_metrics()
    {
        HalsteadMetrics metrics = HalsteadMetrics.FromPrimitiveCounts(
            distinctOperatorCount: 0,
            distinctOperandCount: 0,
            totalOperatorCount: 0,
            totalOperandCount: 0);

        AssertZeroDerivedMetrics(metrics);
    }

    [Fact]
    public void Trivial_counts_keep_valid_non_negative_metrics()
    {
        HalsteadMetrics metrics = HalsteadMetrics.FromPrimitiveCounts(
            distinctOperatorCount: 1,
            distinctOperandCount: 1,
            totalOperatorCount: 1,
            totalOperandCount: 1);

        Assert.Equal(2, metrics.Vocabulary);
        Assert.Equal(2, metrics.Length);
        Assert.Equal(0.0, metrics.CalculatedLength, precision: 12);
        Assert.Equal(2.0, metrics.Volume, precision: 12);
        Assert.Equal(0.5, metrics.Difficulty, precision: 12);
        Assert.Equal(1.0, metrics.Effort, precision: 12);
        Assert.Equal(1.0 / 18.0, metrics.EstimatedImplementationTime, precision: 12);
        Assert.Equal(2.0 / 3000.0, metrics.EstimatedDeliveredBugs, precision: 12);
    }

    [Theory]
    [InlineData(0, 1, 0, 3)]
    [InlineData(1, 0, 4, 0)]
    [InlineData(0, 4, 0, 9)]
    [InlineData(4, 0, 9, 0)]
    public void Zero_operator_or_operand_vocabulary_never_divides_by_zero(
        int distinctOperatorCount,
        int distinctOperandCount,
        int totalOperatorCount,
        int totalOperandCount)
    {
        HalsteadMetrics metrics = HalsteadMetrics.FromPrimitiveCounts(
            distinctOperatorCount,
            distinctOperandCount,
            totalOperatorCount,
            totalOperandCount);

        Assert.Equal(0.0, metrics.Difficulty, precision: 12);
        Assert.Equal(0.0, metrics.Effort, precision: 12);
        Assert.Equal(0.0, metrics.EstimatedImplementationTime, precision: 12);
        AssertFinite(metrics.Volume);
        AssertFinite(metrics.EstimatedDeliveredBugs);
    }

    [Fact]
    public void Result_exposes_metrics_derived_from_classification_counts()
    {
        HalsteadClassificationResult result = new(
        [
            HalsteadElement.Operator(HalsteadOperatorKind.Add),
            HalsteadElement.Operand(HalsteadOperandKind.Local, "local:left"),
            HalsteadElement.Operator(HalsteadOperatorKind.Add),
            HalsteadElement.Operand(HalsteadOperandKind.Local, "local:left"),
            HalsteadElement.Operand(HalsteadOperandKind.Local, "local:right"),
        ]);

        Assert.Equal(new HalsteadPrimitiveCounts(1, 2, 2, 3), result.PrimitiveCounts);
        Assert.Equal(3, result.Metrics.Vocabulary);
        Assert.Equal(5, result.Metrics.Length);
    }

    [Fact]
    public void Syntax_classification_flows_to_derived_metrics_without_retraversing_for_formulas()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                int M()
                {
                    return 1;
                }
            }
            """);

        Assert.Equal(new HalsteadPrimitiveCounts(1, 1, 1, 1), result.PrimitiveCounts);
        Assert.Equal(2.0, result.Metrics.Volume, precision: 12);
        Assert.Equal(1.0, result.Metrics.Effort, precision: 12);
    }

    [Fact]
    public void Empty_executable_member_flows_to_zero_derived_metrics()
    {
        HalsteadClassificationResult result = AnalyzeMethod(
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """);

        Assert.Equal(default, result.PrimitiveCounts);
        AssertZeroDerivedMetrics(result.Metrics);
    }

    [Fact]
    public void Formatting_is_invariant_across_current_culture()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            HalsteadMetrics metrics = HalsteadMetrics.FromPrimitiveCounts(2, 3, 4, 5);
            string invariant = metrics.ToString();

            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

            Assert.Equal(invariant, metrics.ToString());
            Assert.Contains("difficulty=1.6666666666666667", metrics.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(0, 1, 0, 1)]
    [InlineData(1, 0, 1, 0)]
    [InlineData(2, 3, 4, 5)]
    [InlineData(4, 4, 8, 12)]
    public void Metrics_never_expose_nan_or_infinity(
        int distinctOperatorCount,
        int distinctOperandCount,
        int totalOperatorCount,
        int totalOperandCount)
    {
        HalsteadMetrics metrics = HalsteadMetrics.FromPrimitiveCounts(
            distinctOperatorCount,
            distinctOperandCount,
            totalOperatorCount,
            totalOperandCount);

        AssertFinite(metrics.CalculatedLength);
        AssertFinite(metrics.Volume);
        AssertFinite(metrics.Difficulty);
        AssertFinite(metrics.Effort);
        AssertFinite(metrics.EstimatedImplementationTime);
        AssertFinite(metrics.EstimatedDeliveredBugs);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    [InlineData(2, 0, 1, 0)]
    [InlineData(0, 2, 0, 1)]
    public void Primitive_counts_enforce_non_negative_consistent_inputs(
        int distinctOperatorCount,
        int distinctOperandCount,
        int totalOperatorCount,
        int totalOperandCount)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HalsteadPrimitiveCounts(
                distinctOperatorCount,
                distinctOperandCount,
                totalOperatorCount,
                totalOperandCount));
    }

    private static void AssertZeroDerivedMetrics(HalsteadMetrics metrics)
    {
        Assert.Equal(0, metrics.Vocabulary);
        Assert.Equal(0, metrics.Length);
        Assert.Equal(0.0, metrics.CalculatedLength, precision: 12);
        AssertZeroEstimates(metrics);
    }

    private static void AssertZeroEstimates(HalsteadMetrics metrics)
    {
        Assert.Equal(0.0, metrics.Volume, precision: 12);
        Assert.Equal(0.0, metrics.Difficulty, precision: 12);
        Assert.Equal(0.0, metrics.Effort, precision: 12);
        Assert.Equal(0.0, metrics.EstimatedImplementationTime, precision: 12);
        Assert.Equal(0.0, metrics.EstimatedDeliveredBugs, precision: 12);
    }

    private static void AssertFinite(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
        Assert.True(value >= 0.0);
    }

    private static HalsteadClassificationResult AnalyzeMethod(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Parse, SourceCodeKind.Regular));
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "HalsteadMetricsTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        ExecutableMember member = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "M")
            .PipeToExecutableMember(semanticModel);

        bool analyzed = new HalsteadClassificationAnalyzer().TryAnalyze(
            member,
            semanticModel,
            CancellationToken.None,
            out HalsteadClassificationResult result);

        Assert.True(analyzed);
        return result;
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } = CreateTrustedPlatformReferences();

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
}

internal static class HalsteadMetricsTestExtensions
{
    internal static ExecutableMember PipeToExecutableMember(
        this SyntaxNode node,
        SemanticModel semanticModel)
    {
        bool created = ExecutableMember.TryCreate(
            node,
            semanticModel,
            CancellationToken.None,
            out ExecutableMember? member);

        Assert.True(created);
        Assert.NotNull(member);
        return member;
    }
}

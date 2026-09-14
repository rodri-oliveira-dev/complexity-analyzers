using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Model;
using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Duplicates;
using ComplexityAnalysis.Tool.Reporting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ComplexityAnalysis.Tool.Project;

internal static class ProjectAnalysisRunner
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp12,
        DocumentationMode.Parse,
        SourceCodeKind.Regular);

    internal static async Task<ProjectReport> AnalyzeAsync(
        ToolOptions options,
        CancellationToken cancellationToken)
    {
        _ = options ?? throw new ArgumentNullException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();

        string baseDirectory = Directory.GetCurrentDirectory();
        ProjectReport report = new()
        {
            EntryPoint = PathUtilities.ToDisplayPath(options.EntryPath, baseDirectory),
        };

        foreach (string projectPath in ProjectEntryResolver.ResolveProjectPaths(options.EntryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CSharpProjectSource project = await CSharpProjectLoader.LoadAsync(projectPath, options, cancellationToken);
            report.Projects.Add(AnalyzeProject(project, baseDirectory, cancellationToken));
        }

        report.Projects.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));

        if (options.DetectDuplicates)
        {
            DuplicateCodeDetector detector = new();
            detector.AddDuplicateReport(report, options.MinimumDuplicateTokens, options.MinimumDuplicateLines, cancellationToken);
        }

        return report;
    }

    private static AnalyzedProjectReport AnalyzeProject(
        CSharpProjectSource project,
        string baseDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SyntaxTree[] syntaxTrees =
        [
            .. project.Files.Select(file => CSharpSyntaxTree.ParseText(
                file.Text,
                ParseOptions,
                path: file.Path,
                cancellationToken: cancellationToken))
        ];
        CSharpCompilation compilation = CSharpCompilation.Create(
            project.Name,
            syntaxTrees,
            TrustedPlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        InterproceduralAnalysisContext interproceduralContext = InterproceduralAnalysisContext.Create(
            compilation,
            cancellationToken);

        AnalyzedProjectReport projectReport = new()
        {
            Name = project.Name,
            Path = PathUtilities.ToDisplayPath(project.ProjectPath, baseDirectory),
        };

        foreach (SyntaxTree syntaxTree in syntaxTrees.OrderBy(tree => PathUtilities.Normalize(tree.FilePath), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
            AnalyzedFileReport fileReport = new()
            {
                Path = PathUtilities.ToDisplayPath(syntaxTree.FilePath, baseDirectory),
            };

            foreach (SyntaxNode node in syntaxTree.GetRoot(cancellationToken).DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ExecutableMember.TryCreate(node, semanticModel, cancellationToken, out ExecutableMember? member)
                    && member is not null)
                {
                    fileReport.Members.Add(CreateMemberReport(
                        member,
                        semanticModel,
                        interproceduralContext,
                        baseDirectory,
                        cancellationToken));
                }
            }

            fileReport.Members.Sort(CompareMembers);
            projectReport.Files.Add(fileReport);
        }

        projectReport.Files.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        return projectReport;
    }

    private static MemberReport CreateMemberReport(
        ExecutableMember member,
        SemanticModel semanticModel,
        InterproceduralAnalysisContext interproceduralContext,
        string baseDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression complexity = MethodComplexityExtractor.AnalyzeMember(
            member,
            semanticModel,
            interproceduralContext,
            cancellationToken);

        MetricReport<int> cyclomatic = new CyclomaticComplexityAnalyzer().TryAnalyze(
            member,
            CyclomaticComplexityAnalysisMode.Standard,
            cancellationToken,
            out CyclomaticComplexityResult cyclomaticResult)
            ? MetricReport<int>.Known(cyclomaticResult.Value)
            : MetricReport<int>.Unknown();
        MetricReport<int> nesting = new MaximumNestingDepthAnalyzer().TryAnalyze(
            member,
            cancellationToken,
            out MaximumNestingDepthResult nestingResult)
            ? MetricReport<int>.Known(nestingResult.Value)
            : MetricReport<int>.Unknown();
        MetricReport<int> nloc = MetricReport<int>.Unknown();
        MetricReport<int> statementCount = MetricReport<int>.Unknown();
        MetricReport<int> tokenCount = MetricReport<int>.Unknown();
        if (new MethodSizeMetricsAnalyzer().TryAnalyze(
            member,
            MethodSizeMetricTargets.Nloc | MethodSizeMetricTargets.StatementCount | MethodSizeMetricTargets.TokenCount,
            cancellationToken,
            out MethodSizeMetricsResult size))
        {
            nloc = MetricReport<int>.Known(size.Nloc);
            statementCount = MetricReport<int>.Known(size.StatementCount);
            tokenCount = MetricReport<int>.Known(size.TokenCount);
        }

        MetricReport<int> parameterCount = new ParameterCountCalculator().TryCalculate(
            member,
            cancellationToken,
            out ParameterCount parameters)
            ? MetricReport<int>.Known(parameters.Value)
            : MetricReport<int>.Unknown();
        MetricReport<int> cognitive = new CognitiveComplexityCalculator().TryCalculate(
            member,
            semanticModel,
            cancellationToken,
            out CognitiveComplexity cognitiveComplexity)
            ? MetricReport<int>.Known(cognitiveComplexity.Value)
            : MetricReport<int>.Unknown();
        HalsteadMetricReport halstead = HalsteadMetricsAnalyzer.TryAnalyze(
            member,
            semanticModel,
            cancellationToken,
            out HalsteadMetrics halsteadMetrics)
            ? CreateHalsteadReport(halsteadMetrics)
            : HalsteadMetricReport.Unknown();

        return new MemberReport
        {
            DisplayName = member.DisplayName,
            Kind = member.Kind.ToString(),
            SymbolId = member.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Location = CreateLocationReport(member.Declaration.GetLocation(), baseDirectory),
            BigO = complexity is UnknownComplexity
                ? BigOReport.Unknown()
                : BigOReport.Known(complexity.ToString()),
            CyclomaticComplexity = cyclomatic,
            MaximumNestingDepth = nesting,
            Nloc = nloc,
            StatementCount = statementCount,
            TokenCount = tokenCount,
            ParameterCount = parameterCount,
            CognitiveComplexity = cognitive,
            Halstead = halstead,
        };
    }

    private static HalsteadMetricReport CreateHalsteadReport(HalsteadMetrics metrics)
    {
        return new HalsteadMetricReport
        {
            Status = "known",
            DistinctOperatorCount = metrics.PrimitiveCounts.DistinctOperatorCount,
            DistinctOperandCount = metrics.PrimitiveCounts.DistinctOperandCount,
            TotalOperatorCount = metrics.PrimitiveCounts.TotalOperatorCount,
            TotalOperandCount = metrics.PrimitiveCounts.TotalOperandCount,
            Vocabulary = metrics.Vocabulary,
            Length = metrics.Length,
            CalculatedLength = metrics.CalculatedLength,
            Volume = metrics.Volume,
            Difficulty = metrics.Difficulty,
            Effort = metrics.Effort,
            EstimatedImplementationTime = metrics.EstimatedImplementationTime,
            EstimatedDeliveredBugs = metrics.EstimatedDeliveredBugs,
        };
    }

    internal static SourceLocationReport CreateLocationReport(Location location, string baseDirectory)
    {
        FileLinePositionSpan lineSpan = location.GetLineSpan();
        return new SourceLocationReport
        {
            FilePath = PathUtilities.ToDisplayPath(lineSpan.Path, baseDirectory),
            StartLine = lineSpan.StartLinePosition.Line + 1,
            StartColumn = lineSpan.StartLinePosition.Character + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            EndColumn = lineSpan.EndLinePosition.Character + 1,
            Start = location.SourceSpan.Start,
            Length = location.SourceSpan.Length,
        };
    }

    private static int CompareMembers(MemberReport left, MemberReport right)
    {
        int fileComparison = StringComparer.Ordinal.Compare(left.Location.FilePath, right.Location.FilePath);
        if (fileComparison != 0)
        {
            return fileComparison;
        }

        int startComparison = left.Location.Start.CompareTo(right.Location.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        return StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
    }

    private static ImmutableArray<MetadataReference> TrustedPlatformReferences
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

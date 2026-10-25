using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Evidence;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Model;
using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Duplicates;
using ComplexityAnalysis.Tool.Reporting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Tool.Project;

internal static class ProjectAnalysisRunner
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.CSharp12,
        DocumentationMode.Parse,
        SourceCodeKind.Regular);

    internal static async Task<ProjectReport> AnalyzeAsync(
        ToolOptions options,
        CancellationToken cancellationToken,
        Action<string>? validatePath = null,
        bool skipReparsePoints = false,
        string? displayBaseDirectory = null)
    {
        _ = options ?? throw new ArgumentNullException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();
        validatePath?.Invoke(options.EntryPath);

        string baseDirectory = displayBaseDirectory is null
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(displayBaseDirectory);
        ProjectReport report = new()
        {
            EntryPoint = PathUtilities.ToDisplayPath(options.EntryPath, baseDirectory),
            SchemaVersion = options.Snapshot ? "complexity-snapshot/1.0" : options.IncludeEvidence ? "1.1" : "1.0",
            Origin = options.Snapshot
                ? new SnapshotOriginReport
                {
                    Kind = "projectAnalysis",
                    EntryPoint = PathUtilities.ToDisplayPath(options.EntryPath, baseDirectory),
                }
                : null,
            Configuration = options.Snapshot
                ? new SnapshotConfigurationReport
                {
                    IncludeGenerated = options.IncludeGenerated,
                    IncludeBuildOutput = options.IncludeBuildOutput,
                    IncludeEvidence = options.IncludeEvidence,
                    DetectDuplicates = options.DetectDuplicates,
                    MinimumDuplicateTokens = options.MinimumDuplicateTokens,
                    MinimumDuplicateLines = options.MinimumDuplicateLines,
                }
                : null,
        };

        foreach (string projectPath in ProjectEntryResolver.ResolveProjectPaths(options.EntryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            validatePath?.Invoke(projectPath);
            CSharpProjectSource project = await CSharpProjectLoader.LoadAsync(
                projectPath,
                options,
                cancellationToken,
                validatePath,
                skipReparsePoints);
            report.Projects.Add(AnalyzeProject(
                project,
                baseDirectory,
                options.IncludeEvidence,
                cancellationToken));
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
        bool includeEvidence,
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
                        includeEvidence,
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
        bool includeEvidence,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ComplexityExpression complexity;
        ComplexityEvidenceReport? evidence = null;
        if (includeEvidence)
        {
            ComplexityAnalysisWithEvidenceResult result = MethodComplexityExtractor.AnalyzeMemberWithEvidence(
                member,
                semanticModel,
                interproceduralContext,
                cancellationToken);
            complexity = result.Complexity;
            evidence = CreateEvidenceReport(result.Evidence);
        }
        else
        {
            complexity = MethodComplexityExtractor.AnalyzeMember(
                member,
                semanticModel,
                interproceduralContext,
                cancellationToken);
        }

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
            StableId = CreateStableId(member, cancellationToken),
            DisplayName = member.DisplayName,
            Kind = member.Kind.ToString(),
            SymbolId = member.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Location = CreateLocationReport(member.Declaration.GetLocation(), baseDirectory),
            BigO = complexity is UnknownComplexity
                ? BigOReport.Unknown()
                : BigOReport.Known(complexity.ToString()),
            Evidence = evidence,
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

    private static string CreateStableId(
        ExecutableMember member,
        CancellationToken cancellationToken)
    {
        string? documentationId = member.Symbol.GetDocumentationCommentId();
        if (documentationId is not null
            && member.Kind is not (ExecutableMemberKind.LocalFunction
                or ExecutableMemberKind.Lambda
                or ExecutableMemberKind.AnonymousMethod))
        {
            return documentationId;
        }

        ISymbol containingSymbol = member.Symbol.ContainingSymbol;
        string containerId = containingSymbol.GetDocumentationCommentId()
            ?? containingSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        SyntaxNode container = containingSymbol.DeclaringSyntaxReferences
            .FirstOrDefault(reference => reference.SyntaxTree == member.SyntaxTree)
            ?.GetSyntax(cancellationToken)
            ?? member.SyntaxTree.GetRoot(cancellationToken);
        int ordinal = container.DescendantNodesAndSelf()
            .Where(IsOrdinalExecutable)
            .TakeWhile(node => node.SpanStart <= member.Declaration.SpanStart)
            .Count() - 1;

        return containerId + "#" + member.Kind + ":" + Math.Max(0, ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsOrdinalExecutable(SyntaxNode node)
    {
        return node is LocalFunctionStatementSyntax
            or LambdaExpressionSyntax
            or AnonymousMethodExpressionSyntax;
    }

    private static ComplexityEvidenceReport CreateEvidenceReport(ComplexityEvidenceTrace trace)
    {
        ComplexityEvidenceReport report = new()
        {
            IsTruncated = trace.IsTruncated,
            MaximumDepth = trace.MaximumDepth,
            MaximumSteps = trace.MaximumSteps,
            UnknownReason = trace.UnknownReason == ComplexityUnknownReason.None
                ? null
                : FormatUnknownReason(trace.UnknownReason),
        };

        foreach (ComplexityEvidenceStep step in trace.Steps)
        {
            ComplexityEstimateReport estimate = new()
            {
                Status = step.Estimate.Complexity is UnknownComplexity ? "unknown" : "known",
                Value = step.Estimate.Complexity is UnknownComplexity
                    ? null
                    : step.Estimate.Complexity.ToString(),
                Guarantee = FormatGuarantee(step.Estimate.Guarantee),
            };
            foreach (ComplexityAssumption assumption in step.Estimate.Assumptions)
            {
                estimate.Assumptions.Add(new ComplexityAssumptionReport
                {
                    Kind = FormatAssumptionKind(assumption.Kind),
                    Description = assumption.Description,
                });
            }

            ComplexityEvidenceStepReport stepReport = new()
            {
                Identity = step.Identity,
                Kind = FormatEvidenceKind(step.Kind),
                Origin = FormatEvidenceOrigin(step.Origin),
                Operation = step.Operation,
                Location = new ComplexityEvidenceLocationReport
                {
                    RelativeStart = step.Location.RelativeStart,
                    Length = step.Location.Length,
                },
                Depth = step.Depth,
                Estimate = estimate,
                Composition = step.Composition is null
                    ? null
                    : FormatComposition(step.Composition.Value),
                UnknownReason = step.UnknownReason == ComplexityUnknownReason.None
                    ? null
                    : FormatUnknownReason(step.UnknownReason),
            };
            stepReport.Dimensions.AddRange(step.Dimensions);
            report.Steps.Add(stepReport);
        }

        return report;
    }

    private static string FormatEvidenceKind(ComplexityEvidenceKind kind)
    {
        return kind switch
        {
            ComplexityEvidenceKind.Analysis => "analysis",
            ComplexityEvidenceKind.Loop => "loop",
            ComplexityEvidenceKind.KnownOperation => "knownOperation",
            ComplexityEvidenceKind.SourceCall => "sourceCall",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static string FormatEvidenceOrigin(ComplexityEvidenceOrigin origin)
    {
        return origin switch
        {
            ComplexityEvidenceOrigin.Syntax => "syntax",
            ComplexityEvidenceOrigin.KnownOperationRegistry => "knownOperationRegistry",
            ComplexityEvidenceOrigin.SourceAnalysis => "sourceAnalysis",
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
    }

    private static string FormatGuarantee(ComplexityGuarantee guarantee)
    {
        return guarantee switch
        {
            ComplexityGuarantee.WorstCaseUpperBound => "worstCaseUpperBound",
            ComplexityGuarantee.Expected => "expected",
            ComplexityGuarantee.Amortized => "amortized",
            ComplexityGuarantee.Unknown => "unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(guarantee)),
        };
    }

    private static string FormatAssumptionKind(ComplexityAssumptionKind kind)
    {
        return kind switch
        {
            ComplexityAssumptionKind.InputDistribution => "inputDistribution",
            ComplexityAssumptionKind.HashDistribution => "hashDistribution",
            ComplexityAssumptionKind.OperationSequence => "operationSequence",
            ComplexityAssumptionKind.ElementOperationCost => "elementOperationCost",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static string FormatComposition(ComplexityOperation operation)
    {
        return operation switch
        {
            ComplexityOperation.Sequential => "sequential",
            ComplexityOperation.Nested => "nested",
            ComplexityOperation.Maximum => "maximum",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    private static string FormatUnknownReason(ComplexityUnknownReason reason)
    {
        return reason switch
        {
            ComplexityUnknownReason.UnsupportedOperation => "unsupportedOperation",
            ComplexityUnknownReason.UnresolvedSymbol => "unresolvedSymbol",
            ComplexityUnknownReason.UnprovenLoopBound => "unprovenLoopBound",
            ComplexityUnknownReason.UnresolvedInputDimension => "unresolvedInputDimension",
            ComplexityUnknownReason.SourceAnalysisUnknown => "sourceAnalysisUnknown",
            ComplexityUnknownReason.InterproceduralAnalysisDisabled => "interproceduralAnalysisDisabled",
            ComplexityUnknownReason.BudgetExceeded => "budgetExceeded",
            ComplexityUnknownReason.CycleBoundary => "cycleBoundary",
            ComplexityUnknownReason.EvidenceLimitExceeded => "evidenceLimitExceeded",
            ComplexityUnknownReason.None => throw new ArgumentOutOfRangeException(nameof(reason)),
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
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
        return startComparison != 0 ? startComparison : StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
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

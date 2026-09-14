using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Tool.Project;
using ComplexityAnalysis.Tool.Reporting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ComplexityAnalysis.Tool.Duplicates;

internal sealed class DuplicateCodeDetector
{
    private const int MaximumAnchorsPerWindowGroup = 8;

    private readonly INormalizedSequenceHasher hasher;

    internal DuplicateCodeDetector()
        : this(new FnvNormalizedSequenceHasher())
    {
    }

    internal DuplicateCodeDetector(INormalizedSequenceHasher hasher)
    {
        this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    internal void AddDuplicateReport(
        ProjectReport report,
        int minimumTokens,
        int? minimumLines,
        CancellationToken cancellationToken)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        cancellationToken.ThrowIfCancellationRequested();

        List<IReadOnlyList<NormalizedToken>> streams = CreateTokenStreams(report, cancellationToken);
        int totalTokens = streams.Sum(stream => stream.Count);
        List<CloneCandidate> candidates = FindCandidates(streams, minimumTokens, minimumLines, cancellationToken);
        List<CloneGroupReport> groups = CreateGroups(candidates, totalTokens);
        Dictionary<string, List<(int Start, int End)>> duplicateRangesByFile = [];
        foreach (CloneGroupReport group in groups)
        {
            foreach (CloneOccurrenceReport occurrence in group.Occurrences)
            {
                if (!duplicateRangesByFile.TryGetValue(occurrence.FilePath, out List<(int Start, int End)>? ranges))
                {
                    ranges = [];
                    duplicateRangesByFile.Add(occurrence.FilePath, ranges);
                }

                ranges.Add((occurrence.Location.Start, occurrence.Location.Start + occurrence.Location.Length));
            }
        }

        int duplicateTokens = 0;
        foreach (AnalyzedProjectReport project in report.Projects)
        {
            int projectTokens = 0;
            int projectDuplicateTokens = 0;
            foreach (AnalyzedFileReport file in project.Files)
            {
                int fileTokens = streams.Where(stream => stream.Count > 0 && stream[0].FilePath == file.Path).Sum(stream => stream.Count);
                int fileDuplicateTokens = CountDuplicateTokens(streams, file.Path, duplicateRangesByFile);
                file.NormalizedTokenCount = fileTokens;
                file.DuplicateTokenCount = fileDuplicateTokens;
                file.DuplicateRate = fileTokens == 0
                    ? MetricReport<double>.Known(0)
                    : MetricReport<double>.Known((double)fileDuplicateTokens / fileTokens * 100.0);
                projectTokens += fileTokens;
                projectDuplicateTokens += fileDuplicateTokens;
            }

            project.DuplicateRate = projectTokens == 0
                ? MetricReport<double>.Known(0)
                : MetricReport<double>.Known((double)projectDuplicateTokens / projectTokens * 100.0);
            duplicateTokens += projectDuplicateTokens;
        }

        DuplicateSummaryReport duplicateSummary = new()
        {
            Enabled = true,
            MinimumTokens = minimumTokens,
            MinimumLines = minimumLines,
            TotalNormalizedTokens = totalTokens,
            DuplicateTokenCount = duplicateTokens,
            DuplicateRate = totalTokens == 0
                ? MetricReport<double>.Known(0)
                : MetricReport<double>.Known((double)duplicateTokens / totalTokens * 100.0),
        };
        duplicateSummary.CloneGroups.AddRange(groups);
        report.Duplicates = duplicateSummary;
    }

    private static int CountDuplicateTokens(
        List<IReadOnlyList<NormalizedToken>> streams,
        string filePath,
        Dictionary<string, List<(int Start, int End)>> duplicateRangesByFile)
    {
        if (!duplicateRangesByFile.TryGetValue(filePath, out List<(int Start, int End)>? ranges))
        {
            return 0;
        }

        int count = 0;
        foreach (IReadOnlyList<NormalizedToken> stream in streams.Where(stream => stream.Count > 0 && stream[0].FilePath == filePath))
        {
            foreach (NormalizedToken token in stream)
            {
                if (ranges.Any(range => token.Start >= range.Start && token.End <= range.End))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static List<IReadOnlyList<NormalizedToken>> CreateTokenStreams(
        ProjectReport report,
        CancellationToken cancellationToken)
    {
        List<IReadOnlyList<NormalizedToken>> streams = [];
        int streamId = 0;

        foreach (AnalyzedProjectReport project in report.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxTree[] syntaxTrees =
            [
                .. project.Files.Select(file =>
                {
                    string fullPath = Path.GetFullPath(file.Path);
                    return CSharpSyntaxTree.ParseText(
                        File.ReadAllText(fullPath),
                        new CSharpParseOptions(LanguageVersion.CSharp12, DocumentationMode.Parse, SourceCodeKind.Regular),
                        fullPath,
                        cancellationToken: cancellationToken);
                })
            ];
            CSharpCompilation compilation = CSharpCompilation.Create(
                project.Name,
                syntaxTrees,
                TrustedPlatformReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            foreach (SyntaxTree syntaxTree in syntaxTrees.OrderBy(tree => PathUtilities.Normalize(tree.FilePath), StringComparer.Ordinal))
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
                foreach (SyntaxNode node in syntaxTree.GetRoot(cancellationToken).DescendantNodes())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ExecutableMember.TryCreate(node, semanticModel, cancellationToken, out ExecutableMember? member)
                        && member is not null
                        && member.Body.HasBody)
                    {
                        IReadOnlyList<NormalizedToken> stream = CSharpDuplicateTokenNormalizer.Normalize(
                            member,
                            semanticModel,
                            project.Path,
                            PathUtilities.ToDisplayPath(syntaxTree.FilePath, Directory.GetCurrentDirectory()),
                            streamId,
                            cancellationToken);
                        if (stream.Count > 0)
                        {
                            streams.Add(stream);
                            streamId++;
                        }
                    }
                }
            }
        }

        return streams;
    }

    private List<CloneCandidate> FindCandidates(
        List<IReadOnlyList<NormalizedToken>> streams,
        int minimumTokens,
        int? minimumLines,
        CancellationToken cancellationToken)
    {
        Dictionary<ulong, List<WindowOccurrence>> index = [];
        foreach (IReadOnlyList<NormalizedToken> stream in streams)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int start = 0; start <= stream.Count - minimumTokens; start++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ulong hash = hasher.Hash(stream, start, minimumTokens);
                if (!index.TryGetValue(hash, out List<WindowOccurrence>? occurrences))
                {
                    occurrences = [];
                    index.Add(hash, occurrences);
                }

                occurrences.Add(new WindowOccurrence(stream, start));
            }
        }

        List<CloneCandidate> candidates = [];
        foreach (List<WindowOccurrence> bucket in index.Values.Where(bucket => bucket.Count > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (List<WindowOccurrence> exactWindowGroup in GroupByExactWindow(bucket, minimumTokens, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<WindowOccurrence> anchors = [];
                foreach (WindowOccurrence occurrence in exactWindowGroup
                    .OrderBy(occurrence => occurrence.SortKey, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bool comparedWithAnchor = false;
                    foreach (WindowOccurrence anchor in anchors)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (Overlaps(anchor, occurrence, minimumTokens))
                        {
                            continue;
                        }

                        CloneCandidate candidate = Extend(anchor, occurrence, minimumTokens, cancellationToken);
                        if (candidate.TokenCount >= minimumTokens
                            && (minimumLines is null || candidate.Left.LineCount >= minimumLines.Value && candidate.Right.LineCount >= minimumLines.Value)
                            && !candidate.Left.Overlaps(candidate.Right))
                        {
                            candidates.Add(candidate);
                        }

                        comparedWithAnchor = true;
                    }

                    if (!comparedWithAnchor && anchors.Count < MaximumAnchorsPerWindowGroup)
                    {
                        anchors.Add(occurrence);
                    }
                }
            }
        }

        candidates.Sort(CloneCandidate.Compare);
        return candidates;
    }

    private static IEnumerable<List<WindowOccurrence>> GroupByExactWindow(
        List<WindowOccurrence> bucket,
        int minimumTokens,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<WindowOccurrence>> exactGroups = [];
        foreach (WindowOccurrence occurrence in bucket)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string signature = CreateSignature(occurrence.Stream, occurrence.Start, minimumTokens, cancellationToken);
            if (!exactGroups.TryGetValue(signature, out List<WindowOccurrence>? occurrences))
            {
                occurrences = [];
                exactGroups.Add(signature, occurrences);
            }

            occurrences.Add(occurrence);
        }

        return exactGroups.Values.Where(group => group.Count > 1);
    }

    private static string CreateSignature(
        IReadOnlyList<NormalizedToken> stream,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        string[] values = new string[length];
        for (int offset = 0; offset < length; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            values[offset] = stream[start + offset].Value;
        }

        return string.Join("\u001f", values);
    }

    private static bool Overlaps(WindowOccurrence left, WindowOccurrence right, int length)
    {
        return left.Stream[left.Start].StreamId == right.Stream[right.Start].StreamId
            && left.Start < right.Start + length
            && right.Start < left.Start + length;
    }

    private static CloneCandidate Extend(
        WindowOccurrence left,
        WindowOccurrence right,
        int minimumTokens,
        CancellationToken cancellationToken)
    {
        int leftStart = left.Start;
        int rightStart = right.Start;
        while (leftStart > 0
            && rightStart > 0
            && StringComparer.Ordinal.Equals(left.Stream[leftStart - 1].Value, right.Stream[rightStart - 1].Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            leftStart--;
            rightStart--;
        }

        int length = minimumTokens;
        while (leftStart + length < left.Stream.Count
            && rightStart + length < right.Stream.Count
            && StringComparer.Ordinal.Equals(left.Stream[leftStart + length].Value, right.Stream[rightStart + length].Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            length++;
        }

        return new CloneCandidate(
            new CloneRange(left.Stream, leftStart, length),
            new CloneRange(right.Stream, rightStart, length),
            length);
    }

    private static List<CloneGroupReport> CreateGroups(List<CloneCandidate> candidates, int totalTokens)
    {
        List<MutableCloneGroup> mutableGroups = [];
        foreach (CloneCandidate candidate in candidates)
        {
            string signature = candidate.CreateSignature();
            MutableCloneGroup? group = mutableGroups.FirstOrDefault(existing => StringComparer.Ordinal.Equals(existing.Signature, signature));
            if (group is null)
            {
                group = new MutableCloneGroup(signature, candidate.TokenCount);
                mutableGroups.Add(group);
            }

            group.Add(candidate.Left);
            group.Add(candidate.Right);
        }

        List<MutableCloneGroup> selected = [];
        foreach (MutableCloneGroup group in mutableGroups
            .Where(group => group.Occurrences.Count > 1)
            .OrderByDescending(group => group.TokenCount)
            .ThenBy(group => group.FirstSortKey, StringComparer.Ordinal))
        {
            if (selected.Any(existing => existing.Overlaps(group)))
            {
                continue;
            }

            selected.Add(group);
        }

        selected.Sort((left, right) => StringComparer.Ordinal.Compare(left.FirstSortKey, right.FirstSortKey));
        List<CloneGroupReport> reports = [];
        for (int index = 0; index < selected.Count; index++)
        {
            MutableCloneGroup group = selected[index];
            CloneGroupReport report = new()
            {
                Id = "CLONE" + (index + 1).ToString("0000", CultureInfo.InvariantCulture),
                NormalizedTokenCount = group.TokenCount,
                OccurrenceCount = group.Occurrences.Count,
                DuplicateTokenCount = group.TokenCount * (group.Occurrences.Count - 1),
                DuplicatePercentage = totalTokens == 0
                    ? MetricReport<double>.Known(0)
                    : MetricReport<double>.Known(group.TokenCount * (group.Occurrences.Count - 1) / (double)totalTokens * 100.0),
            };
            foreach (CloneRange occurrence in group.Occurrences.OrderBy(occurrence => occurrence.SortKey, StringComparer.Ordinal))
            {
                report.Occurrences.Add(occurrence.ToReport());
            }

            reports.Add(report);
        }

        return reports;
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

    private sealed record WindowOccurrence(IReadOnlyList<NormalizedToken> Stream, int Start)
    {
        internal string SortKey => Stream[Start].FilePath + ":" + Start.ToString("D10", CultureInfo.InvariantCulture);
    }

    private sealed record CloneCandidate(CloneRange Left, CloneRange Right, int TokenCount)
    {
        internal string CreateSignature()
        {
            return string.Join("\u001f", Left.Tokens.Select(token => token.Value));
        }

        internal static int Compare(CloneCandidate left, CloneCandidate right)
        {
            int tokenCount = right.TokenCount.CompareTo(left.TokenCount);
            if (tokenCount != 0)
            {
                return tokenCount;
            }

            return StringComparer.Ordinal.Compare(left.Left.SortKey, right.Left.SortKey);
        }
    }

    private sealed class CloneRange
    {
        internal CloneRange(IReadOnlyList<NormalizedToken> stream, int start, int length)
        {
            Stream = stream;
            Start = start;
            Length = length;
        }

        internal IReadOnlyList<NormalizedToken> Stream
        {
            get;
        }

        internal int Start
        {
            get;
        }

        internal int Length
        {
            get;
        }

        internal int End => Start + Length;

        internal int LineCount => Last.EndLine - First.StartLine + 1;

        internal NormalizedToken First => Stream[Start];

        internal NormalizedToken Last => Stream[End - 1];

        internal IEnumerable<NormalizedToken> Tokens => Stream.Skip(Start).Take(Length);

        internal string SortKey => First.FilePath + ":" + First.Start.ToString("D10", CultureInfo.InvariantCulture);

        internal bool Overlaps(CloneRange other)
        {
            return First.StreamId == other.First.StreamId && Start < other.End && other.Start < End;
        }

        internal CloneOccurrenceReport ToReport()
        {
            return new CloneOccurrenceReport
            {
                ProjectPath = First.ProjectPath,
                FilePath = First.FilePath,
                Location = new SourceLocationReport
                {
                    FilePath = First.FilePath,
                    StartLine = First.StartLine,
                    StartColumn = First.StartColumn,
                    EndLine = Last.EndLine,
                    EndColumn = Last.EndColumn,
                    Start = First.Start,
                    Length = Last.End - First.Start,
                },
            };
        }
    }

    private sealed class MutableCloneGroup
    {
        private readonly Dictionary<string, CloneRange> occurrences = [];

        internal MutableCloneGroup(string signature, int tokenCount)
        {
            Signature = signature;
            TokenCount = tokenCount;
        }

        internal string Signature
        {
            get;
        }

        internal int TokenCount
        {
            get;
        }

        internal Dictionary<string, CloneRange>.ValueCollection Occurrences => occurrences.Values;

        internal string FirstSortKey => occurrences.Values.Select(occurrence => occurrence.SortKey).OrderBy(key => key, StringComparer.Ordinal).First();

        internal void Add(CloneRange range)
        {
            _ = occurrences.TryAdd(range.SortKey, range);
        }

        internal bool Overlaps(MutableCloneGroup other)
        {
            foreach (CloneRange left in Occurrences)
            {
                foreach (CloneRange right in other.Occurrences)
                {
                    if (left.Overlaps(right))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

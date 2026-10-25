using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Tool.Comparison;

internal static class SnapshotMemberMatcher
{
    internal static SnapshotMatchResult Match(
        ProjectReport baseline,
        ProjectReport current,
        CancellationToken cancellationToken = default)
    {
        _ = baseline ?? throw new ArgumentNullException(nameof(baseline));
        _ = current ?? throw new ArgumentNullException(nameof(current));

        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, List<MemberReport>> before = Index(baseline, cancellationToken);
        Dictionary<string, List<MemberReport>> after = Index(current, cancellationToken);
        SnapshotMatchResult result = new();

        foreach (string id in before.Keys.Union(after.Keys, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = before.TryGetValue(id, out List<MemberReport>? beforeMembers);
            _ = after.TryGetValue(id, out List<MemberReport>? afterMembers);
            beforeMembers ??= [];
            afterMembers ??= [];

            if (beforeMembers.Count > 1 || afterMembers.Count > 1)
            {
                result.AmbiguousIds.Add(GetOriginalId(beforeMembers, afterMembers));
            }
            else if (beforeMembers.Count == 1 && afterMembers.Count == 1)
            {
                result.Matches.Add(new SnapshotMemberMatch(
                    GetMemberId(beforeMembers[0]),
                    beforeMembers[0],
                    afterMembers[0]));
            }
            else if (beforeMembers.Count == 1)
            {
                result.Removed.Add(beforeMembers[0]);
            }
            else
            {
                result.Added.Add(afterMembers[0]);
            }
        }

        return result;
    }

    private static Dictionary<string, List<MemberReport>> Index(
        ProjectReport report,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<MemberReport>> index = new(StringComparer.Ordinal);
        foreach (AnalyzedProjectReport project in report.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string projectPath = project.Path.Replace('\\', '/');
            foreach (MemberReport member in project.Files.SelectMany(file => file.Members))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string key = projectPath + "\0" + GetMemberId(member);
                if (!index.TryGetValue(key, out List<MemberReport>? members))
                {
                    members = [];
                    index.Add(key, members);
                }

                members.Add(member);
            }
        }

        return index;
    }

    private static string GetOriginalId(
        List<MemberReport> before,
        List<MemberReport> after)
    {
        return before.Count > 0
            ? GetMemberId(before[0])
            : GetMemberId(after[0]);
    }

    private static string GetMemberId(MemberReport member)
    {
        return member.StableId ?? member.SymbolId;
    }
}

internal sealed class SnapshotMatchResult
{
    internal List<SnapshotMemberMatch> Matches { get; } = [];

    internal List<MemberReport> Added { get; } = [];

    internal List<MemberReport> Removed { get; } = [];

    internal List<string> AmbiguousIds { get; } = [];
}

internal sealed record SnapshotMemberMatch(string StableId, MemberReport Before, MemberReport After);

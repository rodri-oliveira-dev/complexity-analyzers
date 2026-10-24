using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Tool.Comparison;

internal static class SnapshotMemberMatcher
{
    internal static SnapshotMatchResult Match(ProjectReport baseline, ProjectReport current)
    {
        _ = baseline ?? throw new ArgumentNullException(nameof(baseline));
        _ = current ?? throw new ArgumentNullException(nameof(current));

        Dictionary<string, List<MemberReport>> before = Index(baseline);
        Dictionary<string, List<MemberReport>> after = Index(current);
        SnapshotMatchResult result = new();

        foreach (string id in before.Keys.Union(after.Keys, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
        {
            _ = before.TryGetValue(id, out List<MemberReport>? beforeMembers);
            _ = after.TryGetValue(id, out List<MemberReport>? afterMembers);
            beforeMembers ??= [];
            afterMembers ??= [];

            if (beforeMembers.Count > 1 || afterMembers.Count > 1)
            {
                result.AmbiguousIds.Add(id);
            }
            else if (beforeMembers.Count == 1 && afterMembers.Count == 1)
            {
                result.Matches.Add(new SnapshotMemberMatch(id, beforeMembers[0], afterMembers[0]));
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

    private static Dictionary<string, List<MemberReport>> Index(ProjectReport report)
    {
        Dictionary<string, List<MemberReport>> index = new(StringComparer.Ordinal);
        foreach (MemberReport member in report.Projects.SelectMany(project => project.Files).SelectMany(file => file.Members))
        {
            string id = member.StableId ?? member.SymbolId;
            if (!index.TryGetValue(id, out List<MemberReport>? members))
            {
                members = [];
                index.Add(id, members);
            }

            members.Add(member);
        }

        return index;
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

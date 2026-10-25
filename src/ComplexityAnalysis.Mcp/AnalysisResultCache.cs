using System.Text;
using System.Text.Json;

namespace ComplexityAnalysis.Mcp;

internal sealed class AnalysisResultCache
{
    private const int MaximumEntries = 4;
    private const int MaximumTotalBytes = 16 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly LinkedList<CacheEntry> _entries = [];
    private int _totalBytes;

    internal CacheRegistration Register(string entryPath, JsonElement report)
    {
        byte[] utf8Report = Encoding.UTF8.GetBytes(report.GetRawText());
        string analysisId = "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(utf8Report));
        if (utf8Report.Length > MaximumTotalBytes)
        {
            return new(analysisId, Reusable: false);
        }

        string normalizedEntryPath = Path.GetFullPath(entryPath);
        lock (_gate)
        {
            LinkedListNode<CacheEntry>? existing = Find(analysisId, normalizedEntryPath);
            if (existing is not null)
            {
                _entries.Remove(existing);
                _entries.AddFirst(existing);
                return new(analysisId, Reusable: true);
            }

            CacheEntry entry = new(analysisId, normalizedEntryPath, report.Clone(), utf8Report.Length);
            _ = _entries.AddFirst(entry);
            _totalBytes += entry.Size;
            while (_entries.Count > MaximumEntries || _totalBytes > MaximumTotalBytes)
            {
                CacheEntry oldest = _entries.Last!.Value;
                _entries.RemoveLast();
                _totalBytes -= oldest.Size;
            }
        }

        return new(analysisId, Reusable: true);
    }

    internal bool TryGet(string analysisId, string entryPath, out JsonElement report)
    {
        lock (_gate)
        {
            LinkedListNode<CacheEntry>? node = Find(analysisId, Path.GetFullPath(entryPath));
            if (node is null)
            {
                report = default;
                return false;
            }

            _entries.Remove(node);
            _entries.AddFirst(node);
            report = node.Value.Report.Clone();
            return true;
        }
    }

    private LinkedListNode<CacheEntry>? Find(string analysisId, string entryPath)
    {
        LinkedListNode<CacheEntry>? current = _entries.First;
        while (current is not null)
        {
            if (StringComparer.Ordinal.Equals(current.Value.AnalysisId, analysisId)
                && PathComparer.Equals(current.Value.EntryPath, entryPath))
            {
                return current;
            }

            current = current.Next;
        }

        return null;
    }

    private static StringComparer PathComparer
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record CacheEntry(string AnalysisId, string EntryPath, JsonElement Report, int Size);
}

internal readonly record struct CacheRegistration(string AnalysisId, bool Reusable);

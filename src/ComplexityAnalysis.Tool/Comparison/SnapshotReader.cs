using System.Text.Json;
using System.Text.Json.Serialization;

using ComplexityAnalysis.Tool.Reporting;

namespace ComplexityAnalysis.Tool.Comparison;

internal static class SnapshotReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    internal static async Task<ProjectReport> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        ProjectReport? report = await JsonSerializer.DeserializeAsync<ProjectReport>(stream, Options, cancellationToken);
        return report is null || !StringComparer.Ordinal.Equals(report.SchemaVersion, "complexity-snapshot/1.0")
            ? throw new InvalidOperationException("Unsupported complexity snapshot schema in '" + Path.GetFileName(path) + "'.")
            : report;
    }
}

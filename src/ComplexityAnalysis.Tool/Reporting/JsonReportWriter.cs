using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComplexityAnalysis.Tool.Reporting;

internal sealed class JsonReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string Write(ProjectReport report)
    {
        return JsonSerializer.Serialize(report, Options) + "\n";
    }

    internal static async Task<string> WriteAsync(
        ProjectReport report,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using MemoryStream stream = new();
        await JsonSerializer.SerializeAsync(stream, report, Options, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

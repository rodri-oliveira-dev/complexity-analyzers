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
}

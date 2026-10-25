using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using ComplexityAnalysis.Tool.Comparison;

namespace ComplexityAnalysis.Tool.Reporting;

internal static class SarifReportWriter
{
    private const string Schema = "https://json.schemastore.org/sarif-2.1.0.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static string Write(ProjectReport report)
    {
        List<SarifResult> results = [];
        foreach (AnalyzedProjectReport project in report.Projects)
        {
            foreach (AnalyzedFileReport file in project.Files)
            {
                foreach (MemberReport member in file.Members)
                {
                    if (!member.BigO.IsUnknown)
                    {
                        results.Add(Result(
                            "CA0001",
                            "note",
                            member.DisplayName + " has estimated complexity " + member.BigO.Value + ".",
                            project.Path + "|" + (member.StableId ?? member.SymbolId),
                            member.Location));
                    }
                    else if (member.Evidence?.UnknownReason is not null)
                    {
                        results.Add(Result(
                            "CA0002",
                            "note",
                            member.DisplayName + " remains Unknown: " + member.Evidence.UnknownReason + ".",
                            project.Path + "|" + (member.StableId ?? member.SymbolId),
                            member.Location));
                    }
                }
            }
        }

        return Serialize(results);
    }

    internal static string Write(ComplexityComparisonReport report)
    {
        List<SarifResult> results = [.. report.Findings.Select(finding => Result(
            RuleId(finding.Classification),
            finding.Severity,
            finding.Classification + ": " + finding.StableId + ". " + finding.Reason,
            finding.StableId,
            finding.Location))];
        return Serialize(results);
    }

    private static string Serialize(List<SarifResult> results)
    {
        SarifLog log = new()
        {
            Schema = Schema,
            Runs =
            [
                new SarifRun
                {
                    Tool = new SarifTool
                    {
                        Driver = new SarifDriver
                        {
                            Name = "ComplexityAnalysis.Tool",
                            InformationUri = "https://github.com/rodri-oliveira-dev/complexity-analyzers",
                            Rules = Rules,
                        },
                    },
                    Results = results,
                }
            ],
        };
        string json = JsonSerializer.Serialize(log, Options) + "\n";
        Validate(json);
        return json;
    }

    private static SarifResult Result(string ruleId, string level, string message, string identity, SourceLocationReport? location)
    {
        SarifResult result = new()
        {
            RuleId = ruleId,
            Level = level == "warning" ? "warning" : "note",
            Message = new SarifMessage { Text = message },
            PartialFingerprints = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["complexityMember/v1"] = Fingerprint(identity),
            },
        };
        if (location is not null)
        {
            result.Locations.Add(new SarifLocation
            {
                PhysicalLocation = new SarifPhysicalLocation
                {
                    ArtifactLocation = new SarifArtifactLocation { Uri = SanitizeUri(location.FilePath) },
                    Region = new SarifRegion
                    {
                        StartLine = Math.Max(1, location.StartLine),
                        StartColumn = Math.Max(1, location.StartColumn),
                        EndLine = Math.Max(1, location.EndLine),
                        EndColumn = Math.Max(1, location.EndColumn),
                    },
                },
            });
        }

        return result;
    }

    private static string SanitizeUri(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (Path.IsPathFullyQualified(path) || normalized.Split('/').Contains("..", StringComparer.Ordinal))
        {
            normalized = Path.GetFileName(path);
        }

        return string.Join('/', normalized.Split('/').Where(segment => segment.Length > 0).Select(Uri.EscapeDataString));
    }

    private static string Fingerprint(string identity)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static string RuleId(string classification)
    {
        return classification == "regression" ? "CA1001" : "CA1002";
    }

    private static void Validate(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("version").GetString() != "2.1.0"
            || root.GetProperty("runs").GetArrayLength() != 1
            || !root.GetProperty("runs")[0].TryGetProperty("tool", out _)
            || !root.GetProperty("runs")[0].TryGetProperty("results", out _))
        {
            throw new InvalidOperationException("The generated SARIF document is structurally invalid.");
        }
    }

    private static List<SarifRule> Rules
    {
        get;
    } =
    [
        new() { Id = "CA0001", Name = "complexity-estimate", ShortDescription = new SarifMessage { Text = "Known complexity estimate" } },
        new() { Id = "CA0002", Name = "complexity-unknown", ShortDescription = new SarifMessage { Text = "Explicit unknown complexity" } },
        new() { Id = "CA1001", Name = "asymptotic-regression", ShortDescription = new SarifMessage { Text = "Proven asymptotic regression" } },
        new() { Id = "CA1002", Name = "complexity-comparison", ShortDescription = new SarifMessage { Text = "Complexity comparison finding" } },
    ];
}

internal sealed class SarifLog
{
    [JsonPropertyName("$schema")]
    public required string Schema
    {
        get; init;
    }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "2.1.0";

    [JsonPropertyName("runs")]
    public required List<SarifRun> Runs
    {
        get; init;
    }
}

internal sealed class SarifRun
{
    [JsonPropertyName("tool")]
    public required SarifTool Tool
    {
        get; init;
    }

    [JsonPropertyName("results")]
    public required List<SarifResult> Results
    {
        get; init;
    }
}

internal sealed class SarifTool
{
    [JsonPropertyName("driver")]
    public required SarifDriver Driver
    {
        get; init;
    }
}

internal sealed class SarifDriver
{
    [JsonPropertyName("name")]
    public required string Name
    {
        get; init;
    }

    [JsonPropertyName("informationUri")]
    public required string InformationUri
    {
        get; init;
    }

    [JsonPropertyName("rules")]
    public required List<SarifRule> Rules
    {
        get; init;
    }
}

internal sealed class SarifRule
{
    [JsonPropertyName("id")]
    public required string Id
    {
        get; init;
    }

    [JsonPropertyName("name")]
    public required string Name
    {
        get; init;
    }

    [JsonPropertyName("shortDescription")]
    public required SarifMessage ShortDescription
    {
        get; init;
    }
}

internal sealed class SarifResult
{
    [JsonPropertyName("ruleId")]
    public required string RuleId
    {
        get; init;
    }

    [JsonPropertyName("level")]
    public required string Level
    {
        get; init;
    }

    [JsonPropertyName("message")]
    public required SarifMessage Message
    {
        get; init;
    }

    [JsonPropertyName("partialFingerprints")]
    public required Dictionary<string, string> PartialFingerprints
    {
        get; init;
    }

    [JsonPropertyName("locations")]
    public List<SarifLocation> Locations { get; } = [];
}

internal sealed class SarifMessage
{
    [JsonPropertyName("text")]
    public required string Text
    {
        get; init;
    }
}

internal sealed class SarifLocation
{
    [JsonPropertyName("physicalLocation")]
    public required SarifPhysicalLocation PhysicalLocation
    {
        get; init;
    }
}

internal sealed class SarifPhysicalLocation
{
    [JsonPropertyName("artifactLocation")]
    public required SarifArtifactLocation ArtifactLocation
    {
        get; init;
    }

    [JsonPropertyName("region")]
    public required SarifRegion Region
    {
        get; init;
    }
}

internal sealed class SarifArtifactLocation
{
    [JsonPropertyName("uri")]
    public required string Uri
    {
        get; init;
    }
}

internal sealed class SarifRegion
{
    [JsonPropertyName("startLine")]
    public int StartLine
    {
        get; init;
    }

    [JsonPropertyName("startColumn")]
    public int StartColumn
    {
        get; init;
    }

    [JsonPropertyName("endLine")]
    public int EndLine
    {
        get; init;
    }

    [JsonPropertyName("endColumn")]
    public int EndColumn
    {
        get; init;
    }
}

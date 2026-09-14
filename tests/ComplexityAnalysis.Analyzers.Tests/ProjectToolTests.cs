using System.Text.Json;

using ComplexityAnalysis.Tool.Cli;
using ComplexityAnalysis.Tool.Duplicates;
using ComplexityAnalysis.Tool.Project;
using ComplexityAnalysis.Tool.Reporting;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ProjectToolTests
{
    [Fact]
    public async Task Cli_analyzes_csproj_and_writes_deterministic_json()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public int Sum(int[] values)
                {
                    var total = 0;
                    foreach (var value in values)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);

        (int exitCode, string output, string error) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json");
        (int secondExitCode, string secondOutput, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Equal(ToolExitCodes.Success, secondExitCode);
        Assert.Equal(output, secondOutput);
        Assert.Equal(string.Empty, error);
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement member = document.RootElement
            .GetProperty("Projects")[0]
            .GetProperty("Files")[0]
            .GetProperty("Members")[0];
        Assert.Equal("Sum", member.GetProperty("DisplayName").GetString());
        Assert.Equal("known", member.GetProperty("CyclomaticComplexity").GetProperty("Status").GetString());
        Assert.Equal("known", member.GetProperty("Nloc").GetProperty("Status").GetString());
        Assert.Equal("known", member.GetProperty("Halstead").GetProperty("Status").GetString());
    }

    [Fact]
    public async Task Cli_keeps_unknown_big_o_explicit()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public int Search(int[] values)
                {
                    var index = 0;
                    while (System.DateTime.UtcNow.Ticks > index)
                    {
                        index++;
                    }

                    return index;
                }
            }
            """);

        (int exitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement bigO = document.RootElement
            .GetProperty("Projects")[0]
            .GetProperty("Files")[0]
            .GetProperty("Members")[0]
            .GetProperty("BigO");
        Assert.Equal("unknown", bigO.GetProperty("Status").GetString());
        Assert.True(bigO.GetProperty("IsUnknown").GetBoolean());
        Assert.Equal(JsonValueKind.Null, bigO.GetProperty("Value").ValueKind);
    }

    [Fact]
    public async Task Quality_gate_is_opt_in_and_returns_documented_exit_code_when_failed()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public int Branch(int value)
                {
                    if (value > 0)
                    {
                        return value;
                    }

                    return -value;
                }
            }
            """);

        (int successExitCode, _, _) = await RunCliAsync("analyze", fixture.ProjectPath);
        (int failedExitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--max-cyclomatic-complexity",
            "1");

        Assert.Equal(ToolExitCodes.Success, successExitCode);
        Assert.Equal(ToolExitCodes.QualityGateFailed, failedExitCode);
        Assert.Contains("FAIL cyclomaticComplexity Branch", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generated_code_and_build_outputs_are_excluded_by_default()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public void M()
                {
                }
            }
            """);
        fixture.WriteSource(
            "Generated.g.cs",
            """
            public sealed class Generated
            {
                public void G()
                {
                }
            }
            """);
        fixture.WriteSource(
            Path.Combine("bin", "Release", "BuildOutput.cs"),
            """
            public sealed class BuildOutput
            {
                public void B()
                {
                }
            }
            """);

        (int exitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("Alpha.cs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Generated.g.cs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildOutput.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slnx_and_sln_entry_points_resolve_projects_deterministically()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public void M()
                {
                }
            }
            """);
        string slnxPath = Path.Combine(fixture.DirectoryPath, "Fixture.slnx");
        await File.WriteAllTextAsync(
            slnxPath,
            """
            <Solution>
              <Folder Name="/src/">
                <Project Path="Fixture.csproj" />
              </Folder>
            </Solution>
            """);
        string slnPath = Path.Combine(fixture.DirectoryPath, "Fixture.sln");
        await File.WriteAllTextAsync(
            slnPath,
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Fixture", "Fixture.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """);

        (int slnxExitCode, string slnxOutput, _) = await RunCliAsync("analyze", slnxPath, "--format", "json");
        (int slnExitCode, string slnOutput, _) = await RunCliAsync("analyze", slnPath, "--format", "json");

        Assert.Equal(ToolExitCodes.Success, slnxExitCode);
        Assert.Equal(ToolExitCodes.Success, slnExitCode);
        Assert.Contains("Fixture.csproj", slnxOutput, StringComparison.Ordinal);
        Assert.Contains("Fixture.csproj", slnOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_returns_canceled_exit_code_when_token_is_canceled()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource("Alpha.cs", "public sealed class Alpha { public void M() { } }");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        (int exitCode, _, string error) = await RunCliAsync(
            cancellation.Token,
            "analyze",
            fixture.ProjectPath);

        Assert.Equal(ToolExitCodes.Canceled, exitCode);
        Assert.Contains("canceled", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_detection_reports_cross_file_renamed_clones()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public int Sum(int[] values)
                {
                    var total = 0;
                    foreach (var value in values)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);
        fixture.WriteSource(
            "Beta.cs",
            """
            public sealed class Beta
            {
                public int Add(int[] items)
                {
                    var result = 1;
                    foreach (var item in items)
                    {
                        result += item;
                    }

                    return result;
                }
            }
            """);

        (int exitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json",
            "--detect-duplicates",
            "--min-duplicate-tokens",
            "20");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement duplicates = document.RootElement.GetProperty("Duplicates");
        Assert.True(duplicates.GetProperty("Enabled").GetBoolean());
        Assert.True(duplicates.GetProperty("CloneGroups").GetArrayLength() >= 1);
        Assert.Equal(2, duplicates.GetProperty("CloneGroups")[0].GetProperty("OccurrenceCount").GetInt32());
    }

    [Fact]
    public async Task Duplicate_detection_preserves_operator_differences()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public int Sum(int[] values)
                {
                    var total = 0;
                    foreach (var value in values)
                    {
                        total += value;
                    }

                    return total;
                }
            }
            """);
        fixture.WriteSource(
            "Beta.cs",
            """
            public sealed class Beta
            {
                public int Subtract(int[] values)
                {
                    var total = 0;
                    foreach (var value in values)
                    {
                        total -= value;
                    }

                    return total;
                }
            }
            """);

        (int exitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json",
            "--detect-duplicates",
            "--min-duplicate-tokens",
            "20");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement groups = document.RootElement.GetProperty("Duplicates").GetProperty("CloneGroups");
        Assert.Equal(0, groups.GetArrayLength());
    }

    [Fact]
    public void Duplicate_detector_verifies_hash_collisions_before_reporting()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "CollisionA.cs",
            "public sealed class A { public int M(int value) { return value + 1; } }");
        fixture.WriteSource(
            "CollisionB.cs",
            "public sealed class B { public int M(int value) { return value - 1; } }");
        ProjectReport report = new()
        {
            EntryPoint = fixture.ProjectPath,
        };
        report.Projects.Add(new AnalyzedProjectReport
        {
            Name = "collision",
            Path = fixture.ProjectPath,
        });
        report.Projects[0].Files.Add(new AnalyzedFileReport
        {
            Path = Path.Combine(fixture.DirectoryPath, "CollisionA.cs"),
        });
        report.Projects[0].Files.Add(new AnalyzedFileReport
        {
            Path = Path.Combine(fixture.DirectoryPath, "CollisionB.cs"),
        });

        DuplicateCodeDetector detector = new(new ConstantHasher());
        detector.AddDuplicateReport(report, minimumTokens: 4, minimumLines: null, CancellationToken.None);

        Assert.Empty(report.Duplicates.CloneGroups);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunCliAsync(params string[] args)
    {
        return await RunCliAsync(CancellationToken.None, args);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunCliAsync(
        CancellationToken cancellationToken,
        params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = await ComplexityToolApplication.RunAsync(args, output, error, cancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    private sealed class ConstantHasher : INormalizedSequenceHasher
    {
        public ulong Hash(IReadOnlyList<NormalizedToken> tokens, int start, int length)
        {
            _ = tokens;
            _ = start;
            _ = length;
            return 42;
        }
    }

    private sealed class FixtureProject : IDisposable
    {
        private FixtureProject(string directoryPath)
        {
            DirectoryPath = directoryPath;
            ProjectPath = Path.Combine(directoryPath, "Fixture.csproj");
        }

        internal string DirectoryPath
        {
            get;
        }

        internal string ProjectPath
        {
            get;
        }

        internal static FixtureProject Create()
        {
            string directory = Path.Combine(Path.GetTempPath(), "complexity-tool-tests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(directory);
            return new FixtureProject(directory);
        }

        internal void WriteProject()
        {
            File.WriteAllText(
                ProjectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
        }

        internal void WriteSource(string relativePath, string source)
        {
            string fullPath = Path.Combine(DirectoryPath, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, source);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}

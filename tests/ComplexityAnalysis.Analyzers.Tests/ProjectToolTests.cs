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
    public async Task Big_o_quality_gate_recognizes_quadratic_notation_emitted_by_analyzer()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Alpha.cs",
            """
            public sealed class Alpha
            {
                public int CountPairs(int[] values)
                {
                    var count = 0;
                    for (var i = 0; i < values.Length; i++)
                    {
                        for (var j = 0; j < values.Length; j++)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
            """);

        (int exitCode, string output, string error) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--max-complexity",
            "linear");

        Assert.Equal(ToolExitCodes.QualityGateFailed, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Contains("FAIL bigO CountPairs", output, StringComparison.Ordinal);
        Assert.Contains("O(n\u00b2)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_complexity_gate_is_rejected_during_option_parsing()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();

        (int exitCode, string output, string error) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--max-complexity",
            "typo");

        Assert.Equal(ToolExitCodes.Error, exitCode);
        Assert.Equal(string.Empty, output);
        Assert.Contains("--max-complexity", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_options_parse_all_supported_switches()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "complexity-tool-output.json");

        bool parsed = ToolOptions.TryParse(
            [
                "analyze",
                "Fixture.csproj",
                "--format",
                "console",
                "--output",
                outputPath,
                "--include-generated",
                "--include-build-output",
                "--detect-duplicates",
                "--min-duplicate-tokens",
                "12",
                "--min-duplicate-lines",
                "3",
                "--max-complexity",
                "quadratic",
                "--max-cyclomatic-complexity",
                "4",
                "--max-nesting-depth",
                "2",
                "--max-method-nloc",
                "20",
                "--max-statement-count",
                "30",
                "--max-token-count",
                "100",
                "--max-parameters",
                "5",
                "--max-cognitive-complexity",
                "8",
                "--max-duplicate-rate",
                "12.5",
                "--max-duplicate-tokens",
                "50",
            ],
            out ToolOptions? options,
            out string? error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(ReportFormat.Console, options.Format);
        Assert.Equal(Path.GetFullPath(outputPath), options.OutputPath);
        Assert.True(options.IncludeGenerated);
        Assert.True(options.IncludeBuildOutput);
        Assert.True(options.DetectDuplicates);
        Assert.Equal(12, options.MinimumDuplicateTokens);
        Assert.Equal(3, options.MinimumDuplicateLines);
        Assert.Equal("n2", options.MaximumComplexity);
        Assert.Equal(4, options.MaximumCyclomaticComplexity);
        Assert.Equal(2, options.MaximumNestingDepth);
        Assert.Equal(20, options.MaximumMethodNloc);
        Assert.Equal(30, options.MaximumStatementCount);
        Assert.Equal(100, options.MaximumTokenCount);
        Assert.Equal(5, options.MaximumParameters);
        Assert.Equal(8, options.MaximumCognitiveComplexity);
        Assert.Equal(12.5, options.MaximumDuplicateRate);
        Assert.Equal(50, options.MaximumDuplicateTokens);
    }

    [Theory]
    [InlineData()]
    [InlineData("inspect", "Fixture.csproj")]
    [InlineData("analyze", "Fixture.csproj", "--format", "xml")]
    [InlineData("analyze", "Fixture.csproj", "--format")]
    [InlineData("analyze", "Fixture.csproj", "--unknown")]
    [InlineData("analyze", "Fixture.csproj", "--min-duplicate-tokens", "0")]
    [InlineData("analyze", "Fixture.csproj", "--max-nesting-depth", "-1")]
    [InlineData("analyze", "Fixture.csproj", "--max-duplicate-rate", "NaN")]
    public void Tool_options_reject_invalid_command_lines(params string[] args)
    {
        bool parsed = ToolOptions.TryParse(args, out ToolOptions? options, out string? error);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Quality_gate_evaluator_applies_metric_and_duplicate_thresholds()
    {
        Assert.True(ToolOptions.TryParse(
            [
                "analyze",
                "Fixture.csproj",
                "--max-complexity",
                "n",
                "--max-cyclomatic-complexity",
                "3",
                "--max-nesting-depth",
                "1",
                "--max-method-nloc",
                "10",
                "--max-statement-count",
                "20",
                "--max-token-count",
                "40",
                "--max-parameters",
                "2",
                "--max-cognitive-complexity",
                "5",
                "--max-duplicate-rate",
                "10",
                "--max-duplicate-tokens",
                "15",
            ],
            out ToolOptions? options,
            out _));
        ProjectReport report = CreateReportWithMember("M", "O(n log n)");
        report.Projects[0].Files[0].Members[0] = new MemberReport
        {
            DisplayName = "M",
            Kind = "method",
            SymbolId = "M",
            Location = Location("Alpha.cs"),
            BigO = BigOReport.Known("O(n log n)"),
            CyclomaticComplexity = MetricReport<int>.Known(4),
            MaximumNestingDepth = MetricReport<int>.Known(2),
            Nloc = MetricReport<int>.Known(11),
            StatementCount = MetricReport<int>.Known(21),
            TokenCount = MetricReport<int>.Known(41),
            ParameterCount = MetricReport<int>.Known(3),
            CognitiveComplexity = MetricReport<int>.Known(6),
        };
        report.Duplicates = new DuplicateSummaryReport
        {
            Enabled = true,
            MinimumTokens = 10,
            TotalNormalizedTokens = 100,
            DuplicateTokenCount = 20,
            DuplicateRate = MetricReport<double>.Known(20),
        };

        QualityGateEvaluator.Evaluate(report, options!);

        Assert.Contains(report.QualityGates, gate => gate.Metric == "bigO" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "cyclomaticComplexity" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "maximumNestingDepth" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "nloc" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "statementCount" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "tokenCount" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "parameterCount" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "cognitiveComplexity" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "duplicateRate" && !gate.Passed);
        Assert.Contains(report.QualityGates, gate => gate.Metric == "duplicateTokens" && !gate.Passed);
    }

    [Theory]
    [InlineData("constant", "O(1)")]
    [InlineData("log_n", "O(log n)")]
    [InlineData("n", "O(n)")]
    [InlineData("n_log_n", "O(n log n)")]
    [InlineData("n2", "O(n^2)")]
    [InlineData("n3", "O(n^3)")]
    [InlineData("exponential", "O(2^n)")]
    [InlineData("factorial", "O(n!)")]
    public void Quality_gate_evaluator_accepts_supported_big_o_thresholds(string threshold, string actual)
    {
        Assert.True(ToolOptions.TryParse(
            ["analyze", "Fixture.csproj", "--max-complexity", threshold],
            out ToolOptions? options,
            out _));
        ProjectReport report = CreateReportWithMember("M", actual);

        QualityGateEvaluator.Evaluate(report, options!);

        Assert.Contains(report.QualityGates, gate => gate.Metric == "bigO" && gate.Passed);
    }

    [Fact]
    public void Console_report_writer_renders_duplicate_and_quality_gate_sections()
    {
        ProjectReport report = CreateReportWithMember("M", "O(n)");
        report.Projects[0].DuplicateRate = MetricReport<double>.Known(25);
        report.Projects[0].Files[0].NormalizedTokenCount = 40;
        report.Projects[0].Files[0].DuplicateTokenCount = 10;
        report.Projects[0].Files[0].DuplicateRate = MetricReport<double>.Known(25);
        CloneGroupReport group = new()
        {
            Id = "CLONE0001",
            NormalizedTokenCount = 10,
            OccurrenceCount = 2,
            DuplicateTokenCount = 10,
            DuplicatePercentage = MetricReport<double>.Known(25),
        };
        group.Occurrences.Add(new CloneOccurrenceReport
        {
            ProjectPath = "Fixture.csproj",
            FilePath = "Alpha.cs",
            Location = Location("Alpha.cs"),
        });
        group.Occurrences.Add(new CloneOccurrenceReport
        {
            ProjectPath = "Fixture.csproj",
            FilePath = "Beta.cs",
            Location = Location("Beta.cs"),
        });
        report.Duplicates = new DuplicateSummaryReport
        {
            Enabled = true,
            MinimumTokens = 10,
            MinimumLines = 2,
            TotalNormalizedTokens = 40,
            DuplicateTokenCount = 10,
            DuplicateRate = MetricReport<double>.Known(25),
        };
        report.Duplicates.CloneGroups.Add(group);
        report.QualityGates.Add(new QualityGateReport
        {
            Metric = "duplicateRate",
            Subject = "project",
            Threshold = "10",
            Actual = "25",
            Passed = false,
        });

        string output = new ConsoleReportWriter().Write(report);

        Assert.Contains("Duplicates:", output, StringComparison.Ordinal);
        Assert.Contains("CLONE0001: 10 tokens, 2 occurrences", output, StringComparison.Ordinal);
        Assert.Contains("Alpha.cs:1:1", output, StringComparison.Ordinal);
        Assert.Contains("Quality Gates:", output, StringComparison.Ordinal);
        Assert.Contains("FAIL duplicateRate project actual=25 threshold=10", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_xml_inputs_return_documented_error_exit_code()
    {
        using FixtureProject fixture = FixtureProject.Create();
        await File.WriteAllTextAsync(fixture.ProjectPath, "<Project>");
        string slnxPath = Path.Combine(fixture.DirectoryPath, "Fixture.slnx");
        await File.WriteAllTextAsync(slnxPath, "<Solution>");

        (int projectExitCode, string projectOutput, string projectError) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath);
        (int solutionExitCode, string solutionOutput, string solutionError) = await RunCliAsync(
            "analyze",
            slnxPath);

        Assert.Equal(ToolExitCodes.Error, projectExitCode);
        Assert.Equal(ToolExitCodes.Error, solutionExitCode);
        Assert.Equal(string.Empty, projectOutput);
        Assert.Equal(string.Empty, solutionOutput);
        Assert.NotEqual(string.Empty, projectError);
        Assert.NotEqual(string.Empty, solutionError);
    }

    [Fact]
    public async Task Explicit_compile_items_are_additive_when_implicit_compile_items_are_enabled()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Linked/Linked.cs" />
              </ItemGroup>
            </Project>
            """);
        fixture.WriteSource("Alpha.cs", "public sealed class Alpha { public void M() { } }");
        fixture.WriteSource("Linked/Linked.cs", "public sealed class Linked { public void M() { } }");

        (int exitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("Alpha.cs", output, StringComparison.Ordinal);
        Assert.Contains("Linked.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_remove_globs_respect_directory_segments()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <Compile Remove="Generated/**/*.cs" />
              </ItemGroup>
            </Project>
            """);
        fixture.WriteSource("Alpha.cs", "public sealed class Alpha { public void M() { } }");
        fixture.WriteSource("Generated/GeneratedManual.cs", "public sealed class GeneratedManual { public void M() { } }");

        (int exitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json",
            "--include-generated");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.Contains("Alpha.cs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedManual.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_include_globs_respect_directory_segments_when_default_compile_items_are_disabled()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Sources/**/*.cs" />
              </ItemGroup>
            </Project>
            """);
        fixture.WriteSource("Alpha.cs", "public sealed class Alpha { public void M() { } }");
        fixture.WriteSource("Sources/Beta.cs", "public sealed class Beta { public void M() { } }");

        (int exitCode, string output, _) = await RunCliAsync(
            "analyze",
            fixture.ProjectPath,
            "--format",
            "json");

        Assert.Equal(ToolExitCodes.Success, exitCode);
        Assert.DoesNotContain("Alpha.cs", output, StringComparison.Ordinal);
        Assert.Contains("Beta.cs", output, StringComparison.Ordinal);
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

    [Fact]
    public void Duplicate_detector_checks_cancellation_while_indexing_repetitive_windows()
    {
        using FixtureProject fixture = FixtureProject.Create();
        fixture.WriteProject();
        fixture.WriteSource(
            "Repetitive.cs",
            """
            public sealed class Repetitive
            {
                public int M()
                {
                    var value = 0;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    value++;
                    return value;
                }
            }
            """);
        ProjectReport report = new()
        {
            EntryPoint = fixture.ProjectPath,
        };
        report.Projects.Add(new AnalyzedProjectReport
        {
            Name = "repetitive",
            Path = fixture.ProjectPath,
        });
        report.Projects[0].Files.Add(new AnalyzedFileReport
        {
            Path = Path.Combine(fixture.DirectoryPath, "Repetitive.cs"),
        });
        using CancellationTokenSource cancellation = new();
        DuplicateCodeDetector detector = new(new CancelingHasher(cancellation, cancelAfterHashes: 2));

        _ = Assert.Throws<OperationCanceledException>(() =>
            detector.AddDuplicateReport(report, minimumTokens: 2, minimumLines: null, cancellation.Token));
    }

    private static ProjectReport CreateReportWithMember(string memberName, string bigO)
    {
        ProjectReport report = new()
        {
            EntryPoint = "Fixture.csproj",
        };
        AnalyzedProjectReport project = new()
        {
            Name = "Fixture",
            Path = "Fixture.csproj",
        };
        AnalyzedFileReport file = new()
        {
            Path = "Alpha.cs",
        };
        file.Members.Add(new MemberReport
        {
            DisplayName = memberName,
            Kind = "method",
            SymbolId = memberName,
            Location = Location("Alpha.cs"),
            BigO = BigOReport.Known(bigO),
            CyclomaticComplexity = MetricReport<int>.Known(1),
            MaximumNestingDepth = MetricReport<int>.Known(0),
            Nloc = MetricReport<int>.Known(1),
            StatementCount = MetricReport<int>.Known(1),
            TokenCount = MetricReport<int>.Known(1),
            ParameterCount = MetricReport<int>.Known(0),
            CognitiveComplexity = MetricReport<int>.Known(0),
        });
        project.Files.Add(file);
        report.Projects.Add(project);
        return report;
    }

    private static SourceLocationReport Location(string path)
    {
        return new SourceLocationReport
        {
            FilePath = path,
            StartLine = 1,
            StartColumn = 1,
            EndLine = 1,
            EndColumn = 10,
            Start = 0,
            Length = 10,
        };
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

    private sealed class CancelingHasher : INormalizedSequenceHasher
    {
        private readonly CancellationTokenSource cancellation;
        private readonly int cancelAfterHashes;
        private int hashCount;

        internal CancelingHasher(CancellationTokenSource cancellation, int cancelAfterHashes)
        {
            this.cancellation = cancellation;
            this.cancelAfterHashes = cancelAfterHashes;
        }

        public ulong Hash(IReadOnlyList<NormalizedToken> tokens, int start, int length)
        {
            _ = tokens;
            _ = start;
            _ = length;
            hashCount++;
            if (hashCount >= cancelAfterHashes)
            {
                cancellation.Cancel();
            }

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
            WriteProject(
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

        internal void WriteProject(string project)
        {
            File.WriteAllText(ProjectPath, project);
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

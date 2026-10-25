using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text.Json;
using System.Xml.Linq;

using ModelContextProtocol.Client;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class McpPackageContractTests
{
    private const string PackageId = "ComplexityAnalysis.Mcp";
    private const string PackageVersion = "0.0.0-mcp-contract";

    [Fact]
    public async Task Local_package_installs_and_serves_all_tools_as_isolated_dotnet_tool()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        string packageDirectory = Path.Combine(root, "artifacts", "mcp-package-contract-tests");
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "complexity-mcp-package-" + Guid.NewGuid().ToString("N"));
        string installDirectory = Path.Combine(temporaryRoot, "install");
        string fixtureDirectory = Path.Combine(temporaryRoot, "fixture");
        _ = Directory.CreateDirectory(packageDirectory);
        _ = Directory.CreateDirectory(installDirectory);
        _ = Directory.CreateDirectory(fixtureDirectory);

        try
        {
            string packagePath = Path.Combine(packageDirectory, $"{PackageId}.{PackageVersion}.nupkg");
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            RepositoryTestSupport.RunDotNet(
                    root,
                    "pack",
                    "src/ComplexityAnalysis.Mcp/ComplexityAnalysis.Mcp.csproj",
                    "--configuration",
                    "Release",
                    "-p:Version=" + PackageVersion,
                    "-p:PackageVersion=" + PackageVersion,
                    "--output",
                    packageDirectory)
                .AssertSuccess("dotnet pack MCP");

            Assert.True(File.Exists(packagePath), string.Create(
                CultureInfo.InvariantCulture,
                $"Expected MCP package '{packagePath}'."));
            AssertPackageContract(packagePath);

            string nugetConfig = Path.Combine(temporaryRoot, "NuGet.config");
            File.WriteAllText(
                nugetConfig,
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="contract-package" value="{SecurityElement.Escape(packageDirectory)}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="contract-package">
                      <package pattern="ComplexityAnalysis.Mcp" />
                    </packageSource>
                    <packageSource key="nuget.org">
                      <package pattern="*" />
                    </packageSource>
                  </packageSourceMapping>
                </configuration>
                """);

            RepositoryTestSupport.RunDotNet(
                    root,
                    TimeSpan.FromMinutes(3),
                    "tool",
                    "install",
                    PackageId,
                    "--version",
                    PackageVersion,
                    "--tool-path",
                    installDirectory,
                    "--configfile",
                    nugetConfig)
                .AssertSuccess("dotnet tool install MCP");

            string executable = Path.Combine(
                installDirectory,
                OperatingSystem.IsWindows() ? "complexity-mcp.exe" : "complexity-mcp");
            AssertInformationalCommands(executable, root);
            WriteFixture(fixtureDirectory);

            StdioClientTransport transport = new(new StdioClientTransportOptions
            {
                Name = "complexity-analysis-installed-contract",
                Command = executable,
                Arguments = ["--root", fixtureDirectory],
            });
            await using McpClient client = await McpClient.CreateAsync(transport);
            IList<McpClientTool> tools = await client.ListToolsAsync();
            Assert.Equal(
                ["analyze_project", "compare_snapshots", "query_member_evidence"],
                tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));

            var analysisResult = await client.CallToolAsync(
                "analyze_project",
                new Dictionary<string, object?>
                {
                    ["path"] = "Fixture.csproj",
                    ["responseMode"] = "full",
                });
            JsonElement analysis = Assert.IsType<JsonElement>(analysisResult.StructuredContent);
            JsonElement report = analysis.GetProperty("report");
            string stableId = Assert.IsType<string>(report.GetProperty("Projects")[0]
                .GetProperty("Files")[0]
                .GetProperty("Members")[0]
                .GetProperty("StableId")
                .GetString());

            var evidenceResult = await client.CallToolAsync(
                "query_member_evidence",
                new Dictionary<string, object?>
                {
                    ["path"] = "Fixture.csproj",
                    ["stableId"] = stableId,
                    ["analysisId"] = analysis.GetProperty("analysisId").GetString(),
                });
            JsonElement evidence = Assert.IsType<JsonElement>(evidenceResult.StructuredContent);
            Assert.True(evidence.GetProperty("found").GetBoolean());
            Assert.Equal("cachedSnapshot", evidence.GetProperty("source").GetString());

            await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, "baseline.json"), report.GetRawText());
            await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, "current.json"), report.GetRawText());
            var comparisonResult = await client.CallToolAsync(
                "compare_snapshots",
                new Dictionary<string, object?>
                {
                    ["baselinePath"] = "baseline.json",
                    ["currentPath"] = "current.json",
                });
            JsonElement comparison = Assert.IsType<JsonElement>(comparisonResult.StructuredContent);
            Assert.Equal(
                "complexity-comparison/1.0",
                comparison.GetProperty("comparison").GetProperty("SchemaVersion").GetString());
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static void AssertInformationalCommands(string executable, string root)
    {
        ProcessResult help = RepositoryTestSupport.RunProcess(
            executable,
            root,
            TimeSpan.FromMinutes(1),
            "--help");
        help.AssertSuccess("installed complexity-mcp --help");
        Assert.Contains("complexity-mcp [options]", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("jsonrpc", help.StandardOutput, StringComparison.OrdinalIgnoreCase);

        ProcessResult version = RepositoryTestSupport.RunProcess(
            executable,
            root,
            TimeSpan.FromMinutes(1),
            "--version");
        version.AssertSuccess("installed complexity-mcp --version");
        Assert.Equal(PackageVersion, version.StandardOutput.Trim());
    }

    private static void AssertPackageContract(string packagePath)
    {
        using ZipArchive package = ZipFile.OpenRead(packagePath);
        Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("/DotnetToolSettings.xml", StringComparison.Ordinal));
        Assert.Contains(package.Entries, entry => entry.FullName == "tools/net10.0/any/ComplexityAnalysis.Mcp.dll");
        Assert.Contains(package.Entries, entry => entry.FullName == "tools/net10.0/any/ComplexityAnalysis.Tool.dll");
        Assert.Contains(package.Entries, entry => entry.FullName == "tools/net10.0/any/ComplexityAnalysis.Analyzers.dll");
        Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("/ModelContextProtocol.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Entries, entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal));
        Assert.Contains(package.Entries, entry => entry.FullName == "README.md");
        Assert.Contains(package.Entries, entry => entry.FullName == "README.pt-BR.md");
        Assert.Contains(package.Entries, entry => entry.FullName == "icon.png");

        ZipArchiveEntry readmeEntry = Assert.Single(package.Entries, entry => entry.FullName == "README.md");
        using (StreamReader reader = new(readmeEntry.Open()))
        {
            string readme = reader.ReadToEnd();
            Assert.Contains("dotnet tool install --global ComplexityAnalysis.Mcp", readme, StringComparison.Ordinal);
            Assert.Contains("complexity-mcp --root", readme, StringComparison.Ordinal);
            Assert.Contains("query_member_evidence", readme, StringComparison.Ordinal);
        }

        ZipArchiveEntry nuspecEntry = Assert.Single(package.Entries, entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using Stream stream = nuspecEntry.Open();
        XDocument nuspec = XDocument.Load(stream);
        XElement metadata = nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
        Assert.Equal(PackageId, ElementValue(metadata, "id"));
        Assert.Equal(PackageVersion, ElementValue(metadata, "version"));
        Assert.Equal("README.md", ElementValue(metadata, "readme"));
        Assert.Equal("MIT", ElementValue(metadata, "license"));
        Assert.Equal("icon.png", ElementValue(metadata, "icon"));
        Assert.Contains("mcp", ElementValue(metadata, "tags"), StringComparison.Ordinal);
        Assert.Equal(
            "https://github.com/rodri-oliveira-dev/complexity-analyzers",
            metadata.Elements().Single(element => element.Name.LocalName == "repository").Attribute("url")?.Value);
        Assert.Equal(
            "DotnetTool",
            metadata.Elements().Single(element => element.Name.LocalName == "packageTypes").Elements().Single().Attribute("name")?.Value);
        Assert.DoesNotContain(metadata.Elements(), element => element.Name.LocalName == "dependencies");
    }

    private static string ElementValue(XElement metadata, string name)
    {
        return metadata.Elements().Single(element => element.Name.LocalName == name).Value;
    }

    private static void WriteFixture(string directory)
    {
        File.WriteAllText(
            Path.Combine(directory, "Fixture.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(
            Path.Combine(directory, "Fixture.cs"),
            "public static class Fixture { public static int Sum(int[] values) { int total = 0; foreach (int value in values) { total += value; } return total; } }");
    }
}

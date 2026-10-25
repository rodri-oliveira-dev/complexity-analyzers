using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text.Json;
using System.Xml.Linq;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ToolPackageContractTests
{
    private const string PackageId = "ComplexityAnalysis.Tool";
    private const string PackageVersion = "0.0.0-tool-contract";

    [Fact]
    public void Local_package_installs_and_analyzes_sample_as_isolated_dotnet_tool()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        string packageDirectory = Path.Combine(root, "artifacts", "tool-package-contract-tests");
        string installDirectory = Path.Combine(Path.GetTempPath(), "complexity-tool-contract-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(packageDirectory);
        _ = Directory.CreateDirectory(installDirectory);

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
                    "src/ComplexityAnalysis.Tool/ComplexityAnalysis.Tool.csproj",
                    "--configuration",
                    "Release",
                    "-p:Version=" + PackageVersion,
                    "-p:PackageVersion=" + PackageVersion,
                    "--output",
                    packageDirectory)
                .AssertSuccess("dotnet pack tool");

            Assert.True(File.Exists(packagePath), string.Create(CultureInfo.InvariantCulture, $"Expected tool package '{packagePath}'."));
            AssertToolPackageContract(packagePath);

            string nugetConfig = Path.Combine(installDirectory, "NuGet.config");
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
                      <package pattern="ComplexityAnalysis.Tool" />
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
                .AssertSuccess("dotnet tool install");

            string executable = Path.Combine(
                installDirectory,
                OperatingSystem.IsWindows() ? "complexity-analysis.exe" : "complexity-analysis");
            string sample = Path.Combine(root, "samples", "ComplexityAnalysis.Sample", "ComplexityAnalysis.Sample.csproj");
            ProcessResult execution = RepositoryTestSupport.RunProcess(
                executable,
                root,
                TimeSpan.FromMinutes(2),
                "analyze",
                sample,
                "--format",
                "json");

            execution.AssertSuccess("installed complexity-analysis tool");
            using JsonDocument report = JsonDocument.Parse(execution.StandardOutput);
            Assert.Equal("1.0", report.RootElement.GetProperty("SchemaVersion").GetString());
            Assert.NotEmpty(report.RootElement.GetProperty("Projects").EnumerateArray());

            ProcessResult help = RepositoryTestSupport.RunProcess(
                executable,
                root,
                TimeSpan.FromMinutes(1),
                "--help");
            help.AssertSuccess("installed complexity-analysis --help");
            Assert.Contains("complexity-analysis analyze", help.StandardOutput, StringComparison.Ordinal);

            ProcessResult version = RepositoryTestSupport.RunProcess(
                executable,
                root,
                TimeSpan.FromMinutes(1),
                "--version");
            version.AssertSuccess("installed complexity-analysis --version");
            Assert.Equal(PackageVersion, version.StandardOutput.Trim());
        }
        finally
        {
            if (Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, recursive: true);
            }
        }
    }

    private static void AssertToolPackageContract(string packagePath)
    {
        using ZipArchive package = ZipFile.OpenRead(packagePath);
        Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("/DotnetToolSettings.xml", StringComparison.Ordinal));
        Assert.Contains(package.Entries, entry => StringComparer.Ordinal.Equals(entry.FullName, "tools/net10.0/any/ComplexityAnalysis.Tool.dll"));
        Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("/ComplexityAnalysis.Analyzers.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Entries, entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal));
        Assert.Contains(package.Entries, entry => entry.FullName == "README.md");
        Assert.Contains(package.Entries, entry => entry.FullName == "README.pt-BR.md");
        Assert.Contains(package.Entries, entry => entry.FullName == "icon.png");

        ZipArchiveEntry readmeEntry = Assert.Single(package.Entries, entry => entry.FullName == "README.md");
        using (StreamReader readmeReader = new(readmeEntry.Open()))
        {
            string readme = readmeReader.ReadToEnd();
            Assert.Contains("dotnet tool install --global ComplexityAnalysis.Tool", readme, StringComparison.Ordinal);
            Assert.Contains("complexity-analysis compare", readme, StringComparison.Ordinal);
        }

        ZipArchiveEntry nuspecEntry = Assert.Single(package.Entries, entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using Stream stream = nuspecEntry.Open();
        XDocument nuspec = XDocument.Load(stream);
        XElement metadata = nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
        Assert.Equal(PackageId, metadata.Elements().Single(element => element.Name.LocalName == "id").Value);
        Assert.Equal(PackageVersion, metadata.Elements().Single(element => element.Name.LocalName == "version").Value);
        Assert.Equal("README.md", metadata.Elements().Single(element => element.Name.LocalName == "readme").Value);
        Assert.Equal("MIT", metadata.Elements().Single(element => element.Name.LocalName == "license").Value);
        Assert.Equal("icon.png", metadata.Elements().Single(element => element.Name.LocalName == "icon").Value);
        Assert.Contains("complexity", metadata.Elements().Single(element => element.Name.LocalName == "tags").Value, StringComparison.Ordinal);
        Assert.Equal(
            "https://github.com/rodri-oliveira-dev/complexity-analyzers",
            metadata.Elements().Single(element => element.Name.LocalName == "repository").Attribute("url")?.Value);
        Assert.Equal("DotnetTool", metadata.Elements().Single(element => element.Name.LocalName == "packageTypes").Elements().Single().Attribute("name")?.Value);
    }
}

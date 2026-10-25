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
        Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("/ComplexityAnalysis.Tool.dll", StringComparison.Ordinal));
        Assert.Contains(package.Entries, entry => entry.FullName.EndsWith("/ComplexityAnalysis.Analyzers.dll", StringComparison.Ordinal));
        Assert.Contains(package.Entries, entry => entry.FullName == "README.md");
        Assert.Contains(package.Entries, entry => entry.FullName == "icon.png");

        ZipArchiveEntry nuspecEntry = Assert.Single(package.Entries, entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using Stream stream = nuspecEntry.Open();
        XDocument nuspec = XDocument.Load(stream);
        XElement metadata = nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
        Assert.Equal(PackageId, metadata.Elements().Single(element => element.Name.LocalName == "id").Value);
        Assert.Equal(PackageVersion, metadata.Elements().Single(element => element.Name.LocalName == "version").Value);
        Assert.Equal("DotnetTool", metadata.Elements().Single(element => element.Name.LocalName == "packageTypes").Elements().Single().Attribute("name")?.Value);
    }
}

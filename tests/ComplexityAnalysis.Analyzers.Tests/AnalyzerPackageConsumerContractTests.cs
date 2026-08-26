using System.Globalization;
using System.Text.Json;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class AnalyzerPackageConsumerContractTests
{
    private const string PackageId = "ComplexityAnalysis.Analyzers";
    private const string PackageVersion = "0.0.0-consumer-contract";
    private const string AnalyzerDllPath = "analyzers/dotnet/cs/ComplexityAnalysis.Analyzers.dll";
    private const string ConsumerTargetFramework = "net10.0";

    [Fact]
    public void Consumer_project_loads_real_package_executes_diagnostics_and_has_no_runtime_assets()
    {
        PackageArtifacts artifacts = CreatePackageArtifacts();
        string consumerDirectory = Path.Combine(
            Path.GetTempPath(),
            "ComplexityAnalysisConsumerContract-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(consumerDirectory);

        try
        {
            RepositoryTestSupport.RunDotNet(
                    consumerDirectory,
                    "new",
                    "console",
                    "--framework",
                    ConsumerTargetFramework,
                    "--no-restore")
                .AssertSuccess("dotnet new console");
            WriteConsumerBuildIsolationFiles(consumerDirectory);
            RepositoryTestSupport.RunDotNet(consumerDirectory, "new", "nugetconfig")
                .AssertSuccess("dotnet new nugetconfig");
            string nugetConfig = FindNuGetConfig(consumerDirectory);
            RepositoryTestSupport.RunDotNet(
                    consumerDirectory,
                    "nuget",
                    "add",
                    "source",
                    artifacts.PackageDirectory,
                    "--name",
                    "local-complexity-analyzers",
                    "--configfile",
                    nugetConfig)
                .AssertSuccess("dotnet nuget add source");
            RepositoryTestSupport.RunDotNet(
                    consumerDirectory,
                    "add",
                    "package",
                    PackageId,
                    "--version",
                    PackageVersion,
                    "--source",
                    artifacts.PackageDirectory,
                    "--no-restore")
                .AssertSuccess("dotnet add package");

            File.WriteAllText(
                Path.Combine(consumerDirectory, ".editorconfig"),
                """
                root = true

                [*.cs]
                complexity_analyzers.maximum_complexity = n
                dotnet_diagnostic.BIG1006.severity = warning
                dotnet_diagnostic.BIG9000.severity = warning
                """);
            File.WriteAllText(
                Path.Combine(consumerDirectory, "Program.cs"),
                """
                using System;

                public static class Program
                {
                    public static void Main()
                    {
                        Console.WriteLine(new Sample().Quadratic(new[] { 1, 2, 3 }));
                    }
                }

                public sealed class Sample
                {
                    public int Quadratic(int[] values)
                    {
                        var total = 0;
                        foreach (var outer in values)
                        {
                            foreach (var inner in values)
                            {
                                total += outer + inner;
                            }
                        }

                        return total;
                    }
                }
                """);

            RepositoryTestSupport.RunDotNet(consumerDirectory, "restore", "--configfile", nugetConfig)
                .AssertSuccess("dotnet restore");
            ProcessResult build = RepositoryTestSupport.RunDotNet(
                consumerDirectory,
                TimeSpan.FromMinutes(3),
                "build",
                "--configuration",
                "Release",
                "--no-restore");

            build.AssertSuccess("dotnet build");
            string buildOutput = build.StandardOutput + Environment.NewLine + build.StandardError;
            Assert.DoesNotContain("CS8032", buildOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("CS9057", buildOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("AD0001", buildOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("AD0002", buildOutput, StringComparison.Ordinal);
            Assert.Contains("BIG9000", buildOutput, StringComparison.Ordinal);
            Assert.Contains("BIG1006", buildOutput, StringComparison.Ordinal);

            AssertPackageAssetsDoNotBecomeCompileOrRuntimeAssets(consumerDirectory);
        }
        finally
        {
            if (Directory.Exists(consumerDirectory))
            {
                Directory.Delete(consumerDirectory, recursive: true);
            }
        }
    }

    private static PackageArtifacts CreatePackageArtifacts()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        string outputDirectory = Path.Combine(root, "artifacts", "consumer-contract-tests");
        _ = Directory.CreateDirectory(outputDirectory);

        string packagePath = Path.Combine(outputDirectory, $"{PackageId}.{PackageVersion}.nupkg");
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        RepositoryTestSupport.RunDotNet(
                root,
                "pack",
                "src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj",
                "--configuration",
                "Release",
                "--no-build",
                "-p:PackageVersion=" + PackageVersion,
                "--output",
                outputDirectory)
            .AssertSuccess("dotnet pack");

        Assert.True(File.Exists(packagePath), string.Create(CultureInfo.InvariantCulture, $"Expected package '{packagePath}' to exist."));
        return new PackageArtifacts(outputDirectory);
    }

    private static void AssertPackageAssetsDoNotBecomeCompileOrRuntimeAssets(string consumerDirectory)
    {
        string assetsPath = Path.Combine(consumerDirectory, "obj", "project.assets.json");
        Assert.True(File.Exists(assetsPath), string.Create(CultureInfo.InvariantCulture, $"Expected assets file '{assetsPath}' to exist."));

        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        JsonElement packageTarget = FindPackageTarget(assets.RootElement);

        AssertJsonPropertyEmptyOrAbsent(packageTarget, "compile");
        AssertJsonPropertyEmptyOrAbsent(packageTarget, "runtime");
        AssertJsonPropertyEmptyOrAbsent(packageTarget, "runtimeTargets");

        JsonElement library = assets.RootElement
            .GetProperty("libraries")
            .GetProperty($"{PackageId}/{PackageVersion}");
        JsonElement files = library.GetProperty("files");
        Assert.Contains(
            files.EnumerateArray(),
            file => string.Equals(file.GetString()?.Replace('\\', '/'), AnalyzerDllPath, StringComparison.Ordinal));

        if (library.TryGetProperty("dependencies", out JsonElement dependencies))
        {
            Assert.DoesNotContain(
                dependencies.EnumerateObject(),
                dependency => dependency.Name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        }
    }

    private static JsonElement FindPackageTarget(JsonElement root)
    {
        string packageKey = $"{PackageId}/{PackageVersion}";
        foreach (JsonProperty target in root.GetProperty("targets").EnumerateObject())
        {
            if (target.Value.TryGetProperty(packageKey, out JsonElement packageTarget))
            {
                return packageTarget;
            }
        }

        throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Could not find '{packageKey}' in project.assets.json targets."));
    }

    private static void AssertJsonPropertyEmptyOrAbsent(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return;
        }

        Assert.Empty(property.EnumerateObject());
    }

    private static string FindNuGetConfig(string consumerDirectory)
    {
        string lowerCasePath = Path.Combine(consumerDirectory, "nuget.config");
        if (File.Exists(lowerCasePath))
        {
            return lowerCasePath;
        }

        string upperCasePath = Path.Combine(consumerDirectory, "NuGet.config");
        return File.Exists(upperCasePath)
            ? upperCasePath
            : throw new InvalidOperationException("NuGet config template did not create NuGet.config or nuget.config.");
    }

    private static void WriteConsumerBuildIsolationFiles(string consumerDirectory)
    {
        File.WriteAllText(Path.Combine(consumerDirectory, "Directory.Build.props"), "<Project />");
        File.WriteAllText(Path.Combine(consumerDirectory, "Directory.Build.targets"), "<Project />");
        File.WriteAllText(
            Path.Combine(consumerDirectory, "Directory.Packages.props"),
            """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>
            """);
    }

    private sealed record PackageArtifacts(string PackageDirectory);
}

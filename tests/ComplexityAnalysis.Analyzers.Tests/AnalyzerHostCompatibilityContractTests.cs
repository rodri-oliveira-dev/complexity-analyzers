using System.Text.Json;
using System.Xml.Linq;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class AnalyzerHostCompatibilityContractTests
{
    private const string BuildSdkVersion = "10.0.400";
    private const string AnalyzerTargetFramework = "netstandard2.0";
    private const string TestTargetFramework = "net10.0";
    private const string LanguageVersion = "12.0";
    private const string RoslynCSharpVersion = "4.8.0";
    private const string RoslynAnalyzersVersion = "3.11.0";

    [Fact]
    public void Repository_build_time_contract_matches_the_documented_baseline()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        XDocument buildProps = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        XDocument analyzerProject = XDocument.Load(Path.Combine(root, "src", "ComplexityAnalysis.Analyzers", "ComplexityAnalysis.Analyzers.csproj"));
        XDocument testProject = XDocument.Load(Path.Combine(root, "tests", "ComplexityAnalysis.Analyzers.Tests", "ComplexityAnalysis.Analyzers.Tests.csproj"));
        XDocument performanceProject = XDocument.Load(Path.Combine(root, "performance", "ComplexityAnalysis.Analyzers.Performance", "ComplexityAnalysis.Analyzers.Performance.csproj"));

        Assert.Equal(BuildSdkVersion, globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString());
        Assert.Equal("latestFeature", globalJson.RootElement.GetProperty("sdk").GetProperty("rollForward").GetString());
        Assert.False(globalJson.RootElement.GetProperty("sdk").GetProperty("allowPrerelease").GetBoolean());
        Assert.Equal(LanguageVersion, RequiredElementValue(buildProps, "LangVersion"));

        Assert.Equal(AnalyzerTargetFramework, RequiredElementValue(analyzerProject, "TargetFramework"));
        Assert.Equal(TestTargetFramework, RequiredElementValue(testProject, "TargetFramework"));
        Assert.Equal(TestTargetFramework, RequiredElementValue(performanceProject, "TargetFramework"));
    }

    [Fact]
    public void Roslyn_authoring_dependencies_are_pinned_private_and_workspace_free()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        XDocument centralPackages = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        XDocument analyzerProject = XDocument.Load(Path.Combine(root, "src", "ComplexityAnalysis.Analyzers", "ComplexityAnalysis.Analyzers.csproj"));

        AssertPackageVersion(centralPackages, "Microsoft.CodeAnalysis.CSharp", RoslynCSharpVersion);
        AssertPackageVersion(centralPackages, "Microsoft.CodeAnalysis.Analyzers", RoslynAnalyzersVersion);

        AssertPackageReferenceIsPrivate(analyzerProject, "Microsoft.CodeAnalysis.CSharp");
        AssertPackageReferenceIsPrivate(analyzerProject, "Microsoft.CodeAnalysis.Analyzers");

        string[] packageIds =
        [
            .. centralPackages
                .Descendants("PackageVersion")
                .Select(element => element.Attribute("Include")?.Value)
                .Concat(analyzerProject.Descendants("PackageReference").Select(element => element.Attribute("Include")?.Value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
        ];

        Assert.DoesNotContain(packageIds, packageId => packageId.Contains("Workspaces", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyzer_package_project_keeps_analyzer_only_packaging_contract()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        XDocument analyzerProject = XDocument.Load(Path.Combine(root, "src", "ComplexityAnalysis.Analyzers", "ComplexityAnalysis.Analyzers.csproj"));

        Assert.Equal("false", RequiredElementValue(analyzerProject, "GeneratePackageOnBuild"));
        Assert.Equal("false", RequiredElementValue(analyzerProject, "IncludeBuildOutput"));
        Assert.Equal("true", RequiredElementValue(analyzerProject, "SuppressDependenciesWhenPacking"));
        Assert.Equal("true", RequiredElementValue(analyzerProject, "DevelopmentDependency"));
        Assert.Equal("embedded", RequiredElementValue(analyzerProject, "DebugType"));

        XElement analyzerDllItem = Assert.Single(
            analyzerProject.Descendants("None"),
            element => string.Equals(element.Attribute("Include")?.Value, "$(OutputPath)$(AssemblyName).dll", StringComparison.Ordinal));
        Assert.Equal("true", analyzerDllItem.Attribute("Pack")?.Value);
        Assert.Equal("analyzers/dotnet/cs/", analyzerDllItem.Attribute("PackagePath")?.Value);
    }

    [Fact]
    public void Ci_host_matrix_keeps_real_package_consumer_validation_for_supported_sdk_hosts()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "complexity-analyzers-ci.yml"));
        string compatibilityJob = RequiredBlock(workflow, "  compatibility:", "  performance:");

        Assert.Contains("needs: package", compatibilityJob, StringComparison.Ordinal);
        Assert.Contains("- sdk: 8.0.x", compatibilityJob, StringComparison.Ordinal);
        Assert.Contains("target_framework: net8.0", compatibilityJob, StringComparison.Ordinal);
        Assert.Contains("- sdk: 9.0.x", compatibilityJob, StringComparison.Ordinal);
        Assert.Contains("target_framework: net9.0", compatibilityJob, StringComparison.Ordinal);
        Assert.Contains("- sdk: 10.0.x", compatibilityJob, StringComparison.Ordinal);
        Assert.Contains("target_framework: net10.0", compatibilityJob, StringComparison.Ordinal);
        Assert.Contains("Validate-AnalyzerPackageConsumer.ps1", compatibilityJob, StringComparison.Ordinal);
        Assert.DoesNotContain("continue-on-error", compatibilityJob, StringComparison.Ordinal);
    }

    [Fact]
    public void Dependabot_policy_keeps_roslyn_updates_conservative()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        string dependabot = File.ReadAllText(Path.Combine(root, ".github", "dependabot.yml"));

        Assert.Contains("roslyn:", dependabot, StringComparison.Ordinal);
        Assert.Contains("Microsoft.CodeAnalysis*", dependabot, StringComparison.Ordinal);
        Assert.Contains("dependency-name: \"Microsoft.CodeAnalysis.CSharp\"", dependabot, StringComparison.Ordinal);
        Assert.Contains("versions:", dependabot, StringComparison.Ordinal);
        Assert.Contains("\"> 4.8.0\"", dependabot, StringComparison.Ordinal);
        Assert.Contains("dependency-name: \"Microsoft.CodeAnalysis.Analyzers\"", dependabot, StringComparison.Ordinal);
        Assert.Contains("version-update:semver-major", dependabot, StringComparison.Ordinal);
    }

    private static string RequiredElementValue(XDocument document, string elementName)
    {
        XElement? element = document.Descendants(elementName).FirstOrDefault();
        Assert.NotNull(element);

        return element.Value;
    }

    private static void AssertPackageVersion(
        XDocument document,
        string packageId,
        string expectedVersion)
    {
        XElement packageVersion = Assert.Single(
            document.Descendants("PackageVersion"),
            element => string.Equals(element.Attribute("Include")?.Value, packageId, StringComparison.Ordinal));

        Assert.Equal(expectedVersion, packageVersion.Attribute("Version")?.Value);
    }

    private static void AssertPackageReferenceIsPrivate(
        XDocument project,
        string packageId)
    {
        XElement packageReference = Assert.Single(
            project.Descendants("PackageReference"),
            element => string.Equals(element.Attribute("Include")?.Value, packageId, StringComparison.Ordinal));

        Assert.Equal("all", packageReference.Attribute("PrivateAssets")?.Value);
        Assert.Null(packageReference.Attribute("Version"));
    }

    private static string RequiredBlock(
        string text,
        string start,
        string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Expected to find block start '{start}'.");

        int endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Expected to find block end '{end}'.");

        return text[startIndex..endIndex];
    }
}

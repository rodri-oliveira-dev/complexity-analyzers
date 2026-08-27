using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class AnalyzerPackageContractTests
{
    private const string PackageId = "ComplexityAnalysis.Analyzers";
    private const string PackageVersion = "0.0.0-package-contract";
    private const string AnalyzerDllPath = "analyzers/dotnet/cs/ComplexityAnalysis.Analyzers.dll";
    private const string PackageIconPath = "icon.png";
    private const string PackageReadmePath = "README.md";
    private const string PackageTitle = "Complexity Analysis Analyzers for C#";
    private const string PackageDescription = "Roslyn analyzers for C# that detect algorithmic and structural code complexity at compile time, including Big-O estimation, cyclomatic complexity, nesting depth, method-size metrics, costly LINQ/BCL patterns, interprocedural analysis, and recurrence solving.";
    private const string PackageTags = "roslyn roslyn-analyzer analyzer csharp dotnet static-analysis code-analysis code-quality complexity algorithmic-complexity big-o cyclomatic-complexity code-metrics nesting-depth performance linq";

    private static readonly Lazy<PackageArtifacts> Artifacts = new(
        CreatePackageArtifacts,
        LazyThreadSafetyMode.ExecutionAndPublication);

    [Fact]
    public void Package_contains_the_analyzer_only_at_the_analyzer_asset_path()
    {
        PackageArtifacts artifacts = Artifacts.Value;
        using ZipArchive package = ZipFile.OpenRead(artifacts.PackagePath);
        string[] entries = GetEntryNames(package);
        string[] dllEntries =
        [
            .. entries.Where(entry => entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        ];

        Assert.Contains(AnalyzerDllPath, entries);
        Assert.Equal([AnalyzerDllPath], dllEntries);
        Assert.DoesNotContain("lib/netstandard2.0/ComplexityAnalysis.Analyzers.dll", entries);
        Assert.DoesNotContain(entries, entry => entry.StartsWith("lib/", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.StartsWith("runtimes/", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Package_contains_readme_and_expected_public_metadata()
    {
        PackageArtifacts artifacts = Artifacts.Value;
        using ZipArchive package = ZipFile.OpenRead(artifacts.PackagePath);
        XDocument nuspec = ReadNuspec(package);
        XElement metadata = GetMetadata(nuspec);
        XNamespace ns = metadata.Name.Namespace;

        string[] entries = GetEntryNames(package);
        Assert.Contains(PackageReadmePath, entries);
        ZipArchiveEntry icon = Assert.Single(package.Entries, entry => entry.FullName == PackageIconPath);
        Assert.True(icon.Length > 0, "The package icon entry should contain the approved PNG asset.");
        Assert.Equal(PackageId, ElementValue(metadata, ns, "id"));
        Assert.Equal(PackageVersion, ElementValue(metadata, ns, "version"));
        Assert.Equal(PackageTitle, ElementValue(metadata, ns, "title"));
        Assert.Equal("Rodrigo de Oliveira", ElementValue(metadata, ns, "authors"));
        Assert.Equal("true", ElementValue(metadata, ns, "developmentDependency"));
        Assert.Equal(PackageReadmePath, ElementValue(metadata, ns, "readme"));
        Assert.Equal(PackageIconPath, ElementValue(metadata, ns, "icon"));
        Assert.Equal("https://github.com/rodri-oliveira-dev/complexity-analyzers", ElementValue(metadata, ns, "projectUrl"));
        Assert.Equal(PackageDescription, ElementValue(metadata, ns, "description"));
        Assert.Equal(PackageTags, ElementValue(metadata, ns, "tags"));
        Assert.Null(metadata.Element(ns + "iconUrl"));
        Assert.Null(metadata.Element(ns + "releaseNotes"));
        Assert.Null(metadata.Element(ns + "copyright"));
    }

    [Fact]
    public void Package_contains_license_and_repository_metadata()
    {
        PackageArtifacts artifacts = Artifacts.Value;
        using ZipArchive package = ZipFile.OpenRead(artifacts.PackagePath);
        XElement metadata = GetMetadata(ReadNuspec(package));
        XNamespace ns = metadata.Name.Namespace;

        XElement license = Assert.Single(metadata.Elements(ns + "license"));
        Assert.Equal("expression", license.Attribute("type")?.Value);
        Assert.Equal("MIT", license.Value);

        XElement repository = Assert.Single(metadata.Elements(ns + "repository"));
        Assert.Equal("git", repository.Attribute("type")?.Value);
        Assert.Equal("https://github.com/rodri-oliveira-dev/complexity-analyzers", repository.Attribute("url")?.Value);
        Assert.False(string.IsNullOrWhiteSpace(repository.Attribute("commit")?.Value));
    }

    [Fact]
    public void Package_does_not_expose_development_or_inherited_dependencies_to_consumers()
    {
        PackageArtifacts artifacts = Artifacts.Value;
        using ZipArchive package = ZipFile.OpenRead(artifacts.PackagePath);
        string[] entries = GetEntryNames(package);
        XElement metadata = GetMetadata(ReadNuspec(package));
        XNamespace ns = metadata.Name.Namespace;

        Assert.DoesNotContain(entries, entry => entry.EndsWith("/ComplexityAnalysis.Core.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.EndsWith("/ComplexityAnalysis.Roslyn.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.EndsWith("/ComplexityAnalysis.Solver.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.EndsWith("/ComplexityAnalysis.Engine.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.EndsWith("/Microsoft.CodeAnalysis.dll", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.EndsWith("/Microsoft.CodeAnalysis.CSharp.dll", StringComparison.Ordinal));

        XElement[] dependencies = [.. metadata.Descendants(ns + "dependency")];
        Assert.DoesNotContain(
            dependencies,
            dependency => dependency.Attribute("id")?.Value.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true);
        Assert.Empty(dependencies);
    }

    [Fact]
    public void Package_does_not_emit_a_symbol_package_for_the_current_analyzer_layout()
    {
        PackageArtifacts artifacts = Artifacts.Value;

        Assert.False(
            File.Exists(artifacts.SymbolPackagePath),
            ".snupkg generation is intentionally disabled because the current analyzer package keeps IncludeBuildOutput=false and packages the DLL only under analyzers/dotnet/cs/.");
    }

    private static PackageArtifacts CreatePackageArtifacts()
    {
        string root = RepositoryTestSupport.FindRepositoryRoot();
        string outputDirectory = Path.Combine(root, "artifacts", "package-contract-tests");
        _ = Directory.CreateDirectory(outputDirectory);

        string packagePath = Path.Combine(outputDirectory, $"{PackageId}.{PackageVersion}.nupkg");
        string symbolPackagePath = Path.Combine(outputDirectory, $"{PackageId}.{PackageVersion}.snupkg");
        DeleteIfExists(packagePath);
        DeleteIfExists(symbolPackagePath);

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
        return new PackageArtifacts(packagePath, symbolPackagePath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string[] GetEntryNames(ZipArchive archive)
    {
        return [.. archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .OrderBy(entry => entry, StringComparer.Ordinal)];
    }

    private static XDocument ReadNuspec(ZipArchive archive)
    {
        ZipArchiveEntry entry = Assert.Single(archive.Entries, entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static XElement GetMetadata(XDocument nuspec)
    {
        XElement? package = nuspec.Root;
        Assert.NotNull(package);

        XNamespace ns = package.Name.Namespace;
        XElement? metadata = package.Element(ns + "metadata");
        Assert.NotNull(metadata);

        return metadata;
    }

    private static string ElementValue(XElement metadata, XNamespace ns, string name)
    {
        XElement? element = metadata.Element(ns + name);
        Assert.NotNull(element);

        return element.Value;
    }

    private sealed record PackageArtifacts(string PackagePath, string SymbolPackagePath);
}

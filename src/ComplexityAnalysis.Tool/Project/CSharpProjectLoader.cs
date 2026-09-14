using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using ComplexityAnalysis.Tool.Cli;

namespace ComplexityAnalysis.Tool.Project;

internal static class CSharpProjectLoader
{
    private static readonly string[] BuildOutputDirectories =
    [
        "bin",
        "obj",
        "artifacts",
        "TestResults",
        "packages",
    ];

    private static readonly string[] InfrastructureDirectories =
    [
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "node_modules",
    ];

    internal static async Task<CSharpProjectSource> LoadAsync(
        string projectPath,
        ToolOptions options,
        CancellationToken cancellationToken)
    {
        _ = options ?? throw new ArgumentNullException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();

        string fullProjectPath = Path.GetFullPath(projectPath);
        string projectDirectory = Path.GetDirectoryName(fullProjectPath) ?? Directory.GetCurrentDirectory();
        XDocument projectDocument = XDocument.Load(fullProjectPath);
        CSharpProjectSource project = new()
        {
            Name = Path.GetFileNameWithoutExtension(fullProjectPath),
            ProjectPath = fullProjectPath,
            ProjectDirectory = projectDirectory,
        };

        foreach (string sourcePath in ResolveCompileFiles(projectDocument, projectDirectory, options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text = await File.ReadAllTextAsync(sourcePath, cancellationToken);
            if (!options.IncludeGenerated && IsGenerated(sourcePath, text))
            {
                continue;
            }

            project.Files.Add(new CSharpSourceFile
            {
                Path = sourcePath,
                Text = text,
            });
        }

        project.Files.Sort((left, right) => StringComparer.Ordinal.Compare(
            PathUtilities.Normalize(left.Path),
            PathUtilities.Normalize(right.Path)));
        return project;
    }

    private static IReadOnlyList<string> ResolveCompileFiles(
        XDocument projectDocument,
        string projectDirectory,
        ToolOptions options)
    {
        List<string> includes =
        [
            .. projectDocument
                .Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(SplitItemList)
        ];
        List<string> removes =
        [
            .. projectDocument
                .Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => element.Attribute("Remove")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(SplitItemList)
        ];

        IEnumerable<string> files = includes.Count == 0
            ? Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            : includes.SelectMany(include => ExpandInclude(projectDirectory, include));

        HashSet<string> removed = new(
            removes.SelectMany(remove => ExpandInclude(projectDirectory, remove)),
            StringComparer.OrdinalIgnoreCase);
        return
        [
            .. files
                .Select(Path.GetFullPath)
                .Where(File.Exists)
                .Where(path => !removed.Contains(path))
                .Where(path => options.IncludeBuildOutput || !IsBuildOutput(path))
                .Where(path => !IsInfrastructureOutput(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => PathUtilities.Normalize(path), StringComparer.Ordinal)
        ];
    }

    private static IEnumerable<string> SplitItemList(string? value)
    {
        if (value is null)
        {
            yield break;
        }

        foreach (string item in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return item;
        }
    }

    private static IEnumerable<string> ExpandInclude(string projectDirectory, string include)
    {
        string normalized = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (normalized.Contains('*', StringComparison.Ordinal))
        {
            if (StringComparer.Ordinal.Equals(normalized, "**" + Path.DirectorySeparatorChar + "*.cs")
                || StringComparer.Ordinal.Equals(normalized, "**/*.cs"))
            {
                return Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
            }

            string fileName = Path.GetFileName(normalized).Replace("*", string.Empty, StringComparison.Ordinal);
            return Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => fileName.Length == 0 || Path.GetFileName(path).Contains(fileName, StringComparison.OrdinalIgnoreCase));
        }

        return [Path.GetFullPath(Path.Combine(projectDirectory, normalized))];
    }

    private static bool IsBuildOutput(string path)
    {
        foreach (string directory in BuildOutputDirectories)
        {
            if (PathUtilities.IsInDirectory(path, directory))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInfrastructureOutput(string path)
    {
        foreach (string directory in InfrastructureDirectories)
        {
            if (PathUtilities.IsInDirectory(path, directory))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGenerated(string path, string text)
    {
        string fileName = Path.GetFileName(path);
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || text.AsSpan(0, Math.Min(text.Length, 1024)).Contains("<auto-generated", StringComparison.OrdinalIgnoreCase);
    }
}

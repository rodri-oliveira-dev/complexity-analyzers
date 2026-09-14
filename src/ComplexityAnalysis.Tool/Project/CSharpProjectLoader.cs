using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        IEnumerable<string> implicitFiles = HasImplicitCompileItems(projectDocument)
            ? Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            : [];
        IEnumerable<string> explicitFiles = includes.SelectMany(include => ExpandInclude(projectDirectory, include));
        IEnumerable<string> files = implicitFiles.Concat(explicitFiles);

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
            return ExpandGlob(projectDirectory, normalized);
        }

        return [Path.GetFullPath(Path.Combine(projectDirectory, normalized))];
    }

    private static IEnumerable<string> ExpandGlob(string projectDirectory, string normalizedPattern)
    {
        string normalizedProjectDirectory = Path.GetFullPath(projectDirectory);
        string pattern = PathUtilities.Normalize(normalizedPattern);
        int firstWildcard = pattern.IndexOfAny(['*', '?']);
        int lastSeparatorBeforeWildcard = pattern.LastIndexOf('/', Math.Max(0, firstWildcard));
        string basePrefix = lastSeparatorBeforeWildcard >= 0
            ? pattern[..(lastSeparatorBeforeWildcard + 1)]
            : string.Empty;
        string searchRoot = Path.GetFullPath(Path.Combine(normalizedProjectDirectory, basePrefix.Replace('/', Path.DirectorySeparatorChar)));
        if (!Directory.Exists(searchRoot))
        {
            return [];
        }

        string relativePattern = lastSeparatorBeforeWildcard >= 0
            ? pattern[(lastSeparatorBeforeWildcard + 1)..]
            : pattern;
        Regex regex = new("^" + GlobToRegex(relativePattern) + "$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return Directory.EnumerateFiles(searchRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => regex.IsMatch(PathUtilities.Normalize(Path.GetRelativePath(searchRoot, path))));
    }

    private static string GlobToRegex(string pattern)
    {
        StringBuilder builder = new();
        for (int index = 0; index < pattern.Length; index++)
        {
            char current = pattern[index];
            if (current == '*')
            {
                bool isDoubleStar = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (isDoubleStar)
                {
                    index++;
                    if (index + 1 < pattern.Length && pattern[index + 1] == '/')
                    {
                        index++;
                        _ = builder.Append("(?:.*/)?");
                    }
                    else
                    {
                        _ = builder.Append(".*");
                    }
                }
                else
                {
                    _ = builder.Append("[^/]*");
                }
            }
            else if (current == '?')
            {
                _ = builder.Append("[^/]");
            }
            else
            {
                _ = builder.Append(Regex.Escape(current.ToString()));
            }
        }

        return builder.ToString();
    }

    private static bool HasImplicitCompileItems(XDocument projectDocument)
    {
        return !IsFalse(ReadProperty(projectDocument, "EnableDefaultItems"))
            && !IsFalse(ReadProperty(projectDocument, "EnableDefaultCompileItems"));
    }

    private static string? ReadProperty(XDocument projectDocument, string propertyName)
    {
        return projectDocument
            .Descendants()
            .Where(element => element.Name.LocalName == propertyName)
            .Select(element => element.Value)
            .LastOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool IsFalse(string? value)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(value?.Trim(), "false");
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

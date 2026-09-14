using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ComplexityAnalysis.Tool.Project;

internal static class ProjectEntryResolver
{
    internal static IReadOnlyList<string> ResolveProjectPaths(string entryPath)
    {
        if (!File.Exists(entryPath))
        {
            throw new InvalidOperationException("Input path does not exist: " + entryPath);
        }

        string extension = Path.GetExtension(entryPath);
        if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".csproj"))
        {
            return [Path.GetFullPath(entryPath)];
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".slnx"))
        {
            return ResolveSlnxProjectPaths(entryPath);
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".sln"))
        {
            return ResolveSlnProjectPaths(entryPath);
        }

        throw new InvalidOperationException("Supported inputs are .csproj, .slnx, and .sln files.");
    }

    private static IReadOnlyList<string> ResolveSlnxProjectPaths(string solutionPath)
    {
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        XDocument document = XDocument.Load(solutionPath);
        return
        [
            .. document
                .Descendants("Project")
                .Select(element => element.Attribute("Path")?.Value)
                .Where(path => path is not null && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path!)))
                .OrderBy(path => PathUtilities.Normalize(path), StringComparer.Ordinal)
        ];
    }

    private static List<string> ResolveSlnProjectPaths(string solutionPath)
    {
        string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
        List<string> projectPaths = [];
        foreach (string line in File.ReadLines(solutionPath))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("Project(", StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = trimmed.Split(',');
            if (parts.Length < 2)
            {
                continue;
            }

            string candidate = parts[1].Trim().Trim('"');
            if (candidate.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                projectPaths.Add(Path.GetFullPath(Path.Combine(solutionDirectory, candidate)));
            }
        }

        projectPaths.Sort((left, right) => StringComparer.Ordinal.Compare(PathUtilities.Normalize(left), PathUtilities.Normalize(right)));
        return projectPaths;
    }
}

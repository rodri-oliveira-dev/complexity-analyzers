using System;
using System.IO;

namespace ComplexityAnalysis.Tool.Project;

internal static class PathUtilities
{
    internal static string Normalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    internal static string ToDisplayPath(string path, string baseDirectory)
    {
        string relative = Path.GetRelativePath(baseDirectory, path);
        return Normalize(relative == "." ? Path.GetFileName(path) : relative);
    }

    internal static bool IsInDirectory(string path, string directoryName)
    {
        string[] segments = Normalize(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(segment, directoryName))
            {
                return true;
            }
        }

        return false;
    }
}

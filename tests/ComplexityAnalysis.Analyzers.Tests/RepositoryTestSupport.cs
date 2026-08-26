using System.Diagnostics;
using System.Globalization;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

internal static class RepositoryTestSupport
{
    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ComplexityAnalysis.Analyzers.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    internal static ProcessResult RunDotNet(
        string workingDirectory,
        params string[] arguments)
    {
        return RunDotNet(
            workingDirectory,
            timeout: TimeSpan.FromMinutes(2),
            arguments);
    }

    internal static ProcessResult RunDotNet(
        string workingDirectory,
        TimeSpan timeout,
        params string[] arguments)
    {
        return RunProcess(
            DotNetFileName,
            workingDirectory,
            timeout,
            arguments);
    }

    internal static ProcessResult RunProcess(
        string fileName,
        string workingDirectory,
        TimeSpan timeout,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        AddDotNetRootToEnvironment(startInfo);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Failed to start '{fileName}'."));
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{fileName} {string.Join(" ", arguments)}' timed out after {timeout.TotalSeconds} seconds."));
        }

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    internal static void AssertSuccess(
        this ProcessResult result,
        string commandDescription)
    {
        Assert.True(
            result.ExitCode == 0,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{commandDescription} failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}"));
    }

    private static string DotNetFileName
    {
        get;
    } = ResolveDotNetFileName();

    private static string ResolveDotNetFileName()
    {
        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath) && File.Exists(hostPath))
        {
            return hostPath;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            string executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            string userDotNet = Path.Combine(userProfile, ".dotnet", executableName);
            if (File.Exists(userDotNet))
            {
                return userDotNet;
            }
        }

        return "dotnet";
    }

    private static void AddDotNetRootToEnvironment(ProcessStartInfo startInfo)
    {
        if (!Path.IsPathFullyQualified(DotNetFileName))
        {
            return;
        }

        string? dotNetRoot = Path.GetDirectoryName(DotNetFileName);
        if (string.IsNullOrWhiteSpace(dotNetRoot))
        {
            return;
        }

        startInfo.Environment["DOTNET_ROOT"] = dotNetRoot;
        string path = startInfo.Environment.TryGetValue("PATH", out string? existingPath)
            ? existingPath ?? string.Empty
            : string.Empty;
        string[] pathEntries = path.Length == 0
            ? [dotNetRoot]
            : [dotNetRoot, path];
        startInfo.Environment["PATH"] = string.Join(
            Path.PathSeparator,
            pathEntries);
    }
}

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

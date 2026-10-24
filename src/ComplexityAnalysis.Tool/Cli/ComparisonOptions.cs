namespace ComplexityAnalysis.Tool.Cli;

internal sealed class ComparisonOptions
{
    private ComparisonOptions(string baselinePath, string currentPath)
    {
        BaselinePath = baselinePath;
        CurrentPath = currentPath;
    }

    internal string BaselinePath
    {
        get;
    }

    internal string CurrentPath
    {
        get;
    }

    internal ReportFormat Format { get; private set; } = ReportFormat.Console;

    internal string? OutputPath
    {
        get; private set;
    }

    internal bool FailOnRegression
    {
        get; private set;
    }

    internal static bool TryParse(string[] args, out ComparisonOptions? options, out string? error)
    {
        options = null;
        error = null;
        if (args.Length < 3 || !StringComparer.Ordinal.Equals(args[0], "compare"))
        {
            error = "Usage: complexity compare <baseline.json> <current.json> [--format console|json] [--output <path>] [--fail-on-regression]";
            return false;
        }

        ComparisonOptions parsed = new(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
        for (int index = 3; index < args.Length; index++)
        {
            string option = args[index];
            switch (option)
            {
                case "--fail-on-regression":
                    parsed.FailOnRegression = true;
                    break;
                case "--format":
                    if (!TryReadValue(args, ref index, option, out string value, out error))
                    {
                        return false;
                    }

                    if (StringComparer.OrdinalIgnoreCase.Equals(value, "json"))
                    {
                        parsed.Format = ReportFormat.Json;
                    }
                    else if (StringComparer.OrdinalIgnoreCase.Equals(value, "console"))
                    {
                        parsed.Format = ReportFormat.Console;
                    }
                    else
                    {
                        error = "Format must be 'console' or 'json'.";
                        return false;
                    }

                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, option, out string outputPath, out error))
                    {
                        return false;
                    }

                    parsed.OutputPath = Path.GetFullPath(outputPath);
                    break;
                default:
                    error = "Unknown option '" + option + "'.";
                    return false;
            }
        }

        options = parsed;
        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, string option, out string value, out string? error)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            error = option + " requires a value.";
            return false;
        }

        value = args[++index];
        error = null;
        return true;
    }
}

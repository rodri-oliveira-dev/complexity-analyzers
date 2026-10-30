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
            error = "Usage: complexity-analysis compare <baseline.json> <current.json> [--format console|json|sarif] [--output <path>] [--fail-on-regression]";
            return false;
        }

        ComparisonOptions parsed = new(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
        for (int index = 3; index < args.Length; index++)
        {
            if (!TryParseOption(args, ref index, parsed, out error))
            {
                return false;
            }
        }

        options = parsed;
        return true;
    }

    private static bool TryParseOption(
        string[] args,
        ref int index,
        ComparisonOptions parsed,
        out string? error)
    {
        string option = args[index];
        switch (option)
        {
            case "--fail-on-regression":
                parsed.FailOnRegression = true;
                error = null;
                return true;
            case "--format":
                return TryParseFormat(args, ref index, option, parsed, out error);
            case "--output":
                if (!TryReadValue(args, ref index, option, out string outputPath, out error))
                {
                    return false;
                }

                parsed.OutputPath = Path.GetFullPath(outputPath);
                return true;
            default:
                error = "Unknown option '" + option + "'.";
                return false;
        }
    }

    private static bool TryParseFormat(
        string[] args,
        ref int index,
        string option,
        ComparisonOptions parsed,
        out string? error)
    {
        if (!TryReadValue(args, ref index, option, out string value, out error)
            || !ToolOptions.TryParseFormat(value, out ReportFormat format))
        {
            error ??= "Format must be 'console', 'json', or 'sarif'.";
            return false;
        }

        parsed.Format = format;
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

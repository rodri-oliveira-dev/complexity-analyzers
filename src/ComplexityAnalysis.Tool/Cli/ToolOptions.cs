using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ComplexityAnalysis.Tool.Cli;

internal sealed class ToolOptions
{
    private const int DefaultMinimumDuplicateTokens = 40;

    private ToolOptions(string entryPath)
    {
        EntryPath = entryPath;
    }

    internal string EntryPath
    {
        get;
    }

    internal ReportFormat Format
    {
        get;
        private set;
    } = ReportFormat.Console;

    internal string? OutputPath
    {
        get;
        private set;
    }

    internal bool IncludeGenerated
    {
        get;
        private set;
    }

    internal bool IncludeBuildOutput
    {
        get;
        private set;
    }

    internal bool DetectDuplicates
    {
        get;
        private set;
    }

    internal int MinimumDuplicateTokens
    {
        get;
        private set;
    } = DefaultMinimumDuplicateTokens;

    internal int? MinimumDuplicateLines
    {
        get;
        private set;
    }

    internal string? MaximumComplexity
    {
        get;
        private set;
    }

    internal int? MaximumCyclomaticComplexity
    {
        get;
        private set;
    }

    internal int? MaximumNestingDepth
    {
        get;
        private set;
    }

    internal int? MaximumMethodNloc
    {
        get;
        private set;
    }

    internal int? MaximumStatementCount
    {
        get;
        private set;
    }

    internal int? MaximumTokenCount
    {
        get;
        private set;
    }

    internal int? MaximumParameters
    {
        get;
        private set;
    }

    internal int? MaximumCognitiveComplexity
    {
        get;
        private set;
    }

    internal double? MaximumDuplicateRate
    {
        get;
        private set;
    }

    internal int? MaximumDuplicateTokens
    {
        get;
        private set;
    }

    internal static bool TryParse(
        string[] args,
        out ToolOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        if (args.Length < 2 || !StringComparer.Ordinal.Equals(args[0], "analyze"))
        {
            error = "Usage: complexity analyze <project.csproj|solution.slnx|solution.sln> [options]";
            return false;
        }

        string entryPath = args[1];
        if (entryPath.Length == 0)
        {
            error = "The input path cannot be empty.";
            return false;
        }

        ToolOptions parsed = new(Path.GetFullPath(entryPath));
        for (int index = 2; index < args.Length; index++)
        {
            string current = args[index];
            switch (current)
            {
                case "--format":
                    if (!TryReadValue(args, ref index, current, out string? formatValue, out error)
                        || !TryParseFormat(formatValue, out ReportFormat format))
                    {
                        error ??= "Format must be 'console' or 'json'.";
                        return false;
                    }

                    parsed.Format = format;
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, current, out string? outputPath, out error))
                    {
                        return false;
                    }

                    parsed.OutputPath = Path.GetFullPath(outputPath);
                    break;
                case "--include-generated":
                    parsed.IncludeGenerated = true;
                    break;
                case "--include-build-output":
                    parsed.IncludeBuildOutput = true;
                    break;
                case "--detect-duplicates":
                    parsed.DetectDuplicates = true;
                    break;
                case "--min-duplicate-tokens":
                    if (!TryReadPositiveInt(args, ref index, current, out int minimumDuplicateTokens, out error))
                    {
                        return false;
                    }

                    parsed.MinimumDuplicateTokens = minimumDuplicateTokens;
                    parsed.DetectDuplicates = true;
                    break;
                case "--min-duplicate-lines":
                    if (!TryReadPositiveInt(args, ref index, current, out int minimumDuplicateLines, out error))
                    {
                        return false;
                    }

                    parsed.MinimumDuplicateLines = minimumDuplicateLines;
                    parsed.DetectDuplicates = true;
                    break;
                case "--max-complexity":
                    if (!TryReadValue(args, ref index, current, out string? maximumComplexity, out error))
                    {
                        return false;
                    }

                    parsed.MaximumComplexity = maximumComplexity;
                    break;
                case "--max-cyclomatic-complexity":
                    if (!TryReadPositiveInt(args, ref index, current, out int maximumCyclomaticComplexity, out error))
                    {
                        return false;
                    }

                    parsed.MaximumCyclomaticComplexity = maximumCyclomaticComplexity;
                    break;
                case "--max-nesting-depth":
                    if (!TryReadNonNegativeInt(args, ref index, current, out int maximumNestingDepth, out error))
                    {
                        return false;
                    }

                    parsed.MaximumNestingDepth = maximumNestingDepth;
                    break;
                case "--max-method-nloc":
                    if (!TryReadPositiveInt(args, ref index, current, out int maximumMethodNloc, out error))
                    {
                        return false;
                    }

                    parsed.MaximumMethodNloc = maximumMethodNloc;
                    break;
                case "--max-statement-count":
                    if (!TryReadPositiveInt(args, ref index, current, out int maximumStatementCount, out error))
                    {
                        return false;
                    }

                    parsed.MaximumStatementCount = maximumStatementCount;
                    break;
                case "--max-token-count":
                    if (!TryReadPositiveInt(args, ref index, current, out int maximumTokenCount, out error))
                    {
                        return false;
                    }

                    parsed.MaximumTokenCount = maximumTokenCount;
                    break;
                case "--max-parameters":
                    if (!TryReadNonNegativeInt(args, ref index, current, out int maximumParameters, out error))
                    {
                        return false;
                    }

                    parsed.MaximumParameters = maximumParameters;
                    break;
                case "--max-cognitive-complexity":
                    if (!TryReadNonNegativeInt(args, ref index, current, out int maximumCognitiveComplexity, out error))
                    {
                        return false;
                    }

                    parsed.MaximumCognitiveComplexity = maximumCognitiveComplexity;
                    break;
                case "--max-duplicate-rate":
                    if (!TryReadNonNegativeDouble(args, ref index, current, out double maximumDuplicateRate, out error))
                    {
                        return false;
                    }

                    parsed.MaximumDuplicateRate = maximumDuplicateRate;
                    parsed.DetectDuplicates = true;
                    break;
                case "--max-duplicate-tokens":
                    if (!TryReadPositiveInt(args, ref index, current, out int maximumDuplicateTokens, out error))
                    {
                        return false;
                    }

                    parsed.MaximumDuplicateTokens = maximumDuplicateTokens;
                    parsed.DetectDuplicates = true;
                    break;
                default:
                    error = "Unknown option '" + current + "'.";
                    return false;
            }
        }

        options = parsed;
        return true;
    }

    private static bool TryParseFormat(string value, out ReportFormat format)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(value, "json"))
        {
            format = ReportFormat.Json;
            return true;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(value, "console"))
        {
            format = ReportFormat.Console;
            return true;
        }

        format = ReportFormat.Console;
        return false;
    }

    private static bool TryReadPositiveInt(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        out int value,
        out string? error)
    {
        if (!TryReadNonNegativeInt(args, ref index, option, out value, out error))
        {
            return false;
        }

        if (value == 0)
        {
            error = option + " must be greater than zero.";
            return false;
        }

        return true;
    }

    private static bool TryReadNonNegativeInt(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        out int value,
        out string? error)
    {
        value = 0;
        if (!TryReadValue(args, ref index, option, out string? rawValue, out error))
        {
            return false;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            || value < 0)
        {
            error = option + " must be a non-negative integer.";
            return false;
        }

        return true;
    }

    private static bool TryReadNonNegativeDouble(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        out double value,
        out string? error)
    {
        value = 0;
        if (!TryReadValue(args, ref index, option, out string? rawValue, out error))
        {
            return false;
        }

        if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || value < 0)
        {
            error = option + " must be a non-negative number.";
            return false;
        }

        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        out string value,
        out string? error)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            error = option + " requires a value.";
            return false;
        }

        index++;
        value = args[index];
        error = null;
        return true;
    }
}

internal enum ReportFormat
{
    Console,
    Json,
}

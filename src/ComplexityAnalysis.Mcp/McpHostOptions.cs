using System.Globalization;

namespace ComplexityAnalysis.Mcp;

internal sealed record McpHostOptions(
    string AllowedRoot,
    TimeSpan RequestTimeout,
    long MaximumInputBytes,
    int MaximumFiles)
{
    internal const int DefaultTimeoutSeconds = 30;
    internal const long DefaultMaximumInputBytes = 50 * 1024 * 1024;
    internal const int DefaultMaximumFiles = 2_000;
    internal const long MaximumInputBytesLimit = 1024L * 1024 * 1024;
    internal const int MaximumFilesLimit = 100_000;

    internal static bool TryParse(string[] args, out McpHostOptions? options, out string? error)
    {
        string root = Directory.GetCurrentDirectory();
        int timeoutSeconds = DefaultTimeoutSeconds;
        long maximumBytes = DefaultMaximumInputBytes;
        int maximumFiles = DefaultMaximumFiles;

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (!TryReadOptionValue(args, ref index, option, out string? value, out error)
                || !TryApplyOption(
                    option,
                    value,
                    ref root,
                    ref timeoutSeconds,
                    ref maximumBytes,
                    ref maximumFiles,
                    out error))
            {
                options = null;
                return false;
            }
        }

        if (!TryResolveRoot(root, out string? fullRoot, out error))
        {
            options = null;
            return false;
        }

        options = new(fullRoot, TimeSpan.FromSeconds(timeoutSeconds), maximumBytes, maximumFiles);
        error = null;
        return true;
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string option,
        out string value,
        out string? error)
    {
        if (option is not ("--root" or "--timeout-seconds" or "--max-input-bytes" or "--max-files"))
        {
            value = string.Empty;
            error = "Unknown option '" + option + "'.";
            return false;
        }

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

    private static bool TryApplyOption(
        string option,
        string value,
        ref string root,
        ref int timeoutSeconds,
        ref long maximumBytes,
        ref int maximumFiles,
        out string? error)
    {
        switch (option)
        {
            case "--root":
                root = value;
                error = null;
                return true;
            case "--timeout-seconds":
                return TryParseBoundedInt(value, 300, option, ref timeoutSeconds, out error);
            case "--max-input-bytes":
                return TryParseMaximumBytes(value, ref maximumBytes, out error);
            case "--max-files":
                return TryParseBoundedInt(value, MaximumFilesLimit, option, ref maximumFiles, out error);
            default:
                throw new InvalidOperationException("Validated startup option was not handled.");
        }
    }

    private static bool TryParseBoundedInt(
        string value,
        int maximum,
        string option,
        ref int target,
        out string? error)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            || parsed < 1
            || parsed > maximum)
        {
            error = option + " must be between 1 and " + maximum.ToString(CultureInfo.InvariantCulture) + ".";
            return false;
        }

        target = parsed;
        error = null;
        return true;
    }

    private static bool TryParseMaximumBytes(string value, ref long target, out string? error)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
            || parsed < 1
            || parsed > MaximumInputBytesLimit)
        {
            error = "--max-input-bytes must be between 1 and 1073741824.";
            return false;
        }

        target = parsed;
        error = null;
        return true;
    }

    private static bool TryResolveRoot(string root, out string fullRoot, out string? error)
    {
        try
        {
            fullRoot = Path.GetFullPath(root);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullRoot = string.Empty;
            error = "The allowed root path is invalid.";
            return false;
        }

        if (!Directory.Exists(fullRoot))
        {
            error = "The allowed root does not exist.";
            return false;
        }

        error = null;
        return true;
    }
}

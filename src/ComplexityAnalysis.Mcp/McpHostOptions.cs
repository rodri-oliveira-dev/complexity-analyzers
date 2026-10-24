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

    internal static bool TryParse(string[] args, out McpHostOptions? options, out string? error)
    {
        string root = Directory.GetCurrentDirectory();
        int timeoutSeconds = DefaultTimeoutSeconds;
        long maximumBytes = DefaultMaximumInputBytes;
        int maximumFiles = DefaultMaximumFiles;

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (index + 1 >= args.Length)
            {
                options = null;
                error = option + " requires a value.";
                return false;
            }

            string value = args[++index];
            switch (option)
            {
                case "--root":
                    root = value;
                    break;
                case "--timeout-seconds":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out timeoutSeconds)
                        || timeoutSeconds is < 1 or > 300)
                    {
                        options = null;
                        error = "--timeout-seconds must be between 1 and 300.";
                        return false;
                    }

                    break;
                case "--max-input-bytes":
                    if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out maximumBytes)
                        || maximumBytes < 1)
                    {
                        options = null;
                        error = "--max-input-bytes must be greater than zero.";
                        return false;
                    }

                    break;
                case "--max-files":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out maximumFiles)
                        || maximumFiles < 1)
                    {
                        options = null;
                        error = "--max-files must be greater than zero.";
                        return false;
                    }

                    break;
                default:
                    options = null;
                    error = "Unknown option '" + option + "'.";
                    return false;
            }
        }

        string fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            options = null;
            error = "The allowed root does not exist.";
            return false;
        }

        options = new(fullRoot, TimeSpan.FromSeconds(timeoutSeconds), maximumBytes, maximumFiles);
        error = null;
        return true;
    }
}

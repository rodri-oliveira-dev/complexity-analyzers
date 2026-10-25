namespace ComplexityAnalysis.Mcp;

internal sealed class AllowedPathPolicy
{
    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly StringComparison _pathComparison;
    private readonly long _maximumInputBytes;
    private readonly int _maximumFiles;

    internal AllowedPathPolicy(McpHostOptions options)
    {
        _root = Path.TrimEndingDirectorySeparator(options.AllowedRoot);
        _rootPrefix = Path.EndsInDirectorySeparator(_root)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        _maximumInputBytes = options.MaximumInputBytes;
        _maximumFiles = options.MaximumFiles;
    }

    internal string ResolveInput(string path, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Input path cannot be empty.");
        }

        string candidate;
        try
        {
            candidate = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(path, _root);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("Input path is invalid.");
        }

        try
        {
            ValidateBoundary(candidate);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("Input path cannot be accessed within the allowed root.");
        }

        if (!File.Exists(candidate))
        {
            throw new ArgumentException("Input file does not exist within the allowed root.");
        }

        if (extensions.Length > 0
            && !extensions.Contains(Path.GetExtension(candidate), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Input file type is not supported.");
        }

        return candidate;
    }

    internal OperationScope CreateOperationScope()
    {
        return new(this);
    }

    private void ValidateBoundary(string candidate)
    {
        string normalized;
        try
        {
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("Input path is invalid.");
        }
        if (!normalized.Equals(_root, _pathComparison)
            && !normalized.StartsWith(_rootPrefix, _pathComparison))
        {
            throw new ArgumentException("Path is outside the allowed root.");
        }

        RejectReparsePointTraversal(normalized);
    }

    private void RejectReparsePointTraversal(string candidate)
    {
        string? current = candidate;
        while (current is not null && !current.Equals(_root, _pathComparison))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Symbolic-link traversal is not allowed.");
            }

            current = Path.GetDirectoryName(current);
        }
    }

    internal sealed class OperationScope
    {
        private readonly AllowedPathPolicy _owner;
        private readonly HashSet<string> _files;
        private long _totalBytes;

        internal OperationScope(AllowedPathPolicy owner)
        {
            _owner = owner;
            _files = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        }

        internal void Validate(string path)
        {
            _owner.ValidateBoundary(path);
            if (!File.Exists(path) || !_files.Add(Path.GetFullPath(path)))
            {
                return;
            }

            if (_files.Count > _owner._maximumFiles)
            {
                throw new ArgumentException("The analysis exceeds the configured file limit.");
            }

            _totalBytes = checked(_totalBytes + new FileInfo(path).Length);
            if (_totalBytes > _owner._maximumInputBytes)
            {
                throw new ArgumentException("The analysis exceeds the configured input-size limit.");
            }
        }
    }
}

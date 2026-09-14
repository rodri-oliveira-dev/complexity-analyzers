namespace ComplexityAnalysis.Tool.Duplicates;

internal sealed record NormalizedToken(
    string Value,
    string ProjectPath,
    string FilePath,
    int StreamId,
    int Index,
    int Start,
    int End,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

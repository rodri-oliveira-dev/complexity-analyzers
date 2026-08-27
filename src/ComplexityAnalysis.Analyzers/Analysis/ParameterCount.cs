namespace ComplexityAnalysis.Analyzers.Analysis;

internal readonly struct ParameterCount
{
    internal ParameterCount(int value)
    {
        Value = value;
    }

    internal int Value
    {
        get;
    }
}

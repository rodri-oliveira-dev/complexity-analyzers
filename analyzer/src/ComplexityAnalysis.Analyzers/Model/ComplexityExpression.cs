namespace ComplexityAnalysis.Analyzers.Model;

internal abstract class ComplexityExpression
{
    internal abstract string ToBigONotation();

    public sealed override string ToString()
    {
        return ToBigONotation();
    }
}

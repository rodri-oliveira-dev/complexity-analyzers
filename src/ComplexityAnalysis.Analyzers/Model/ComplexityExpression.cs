namespace ComplexityAnalysis.Analyzers.Model;

internal abstract class ComplexityExpression
{
    internal virtual string ToBigONotation()
    {
        return "O(" + ToBigOBody() + ")";
    }

    internal abstract string ToBigOBody();

    public sealed override string ToString()
    {
        return ToBigONotation();
    }
}

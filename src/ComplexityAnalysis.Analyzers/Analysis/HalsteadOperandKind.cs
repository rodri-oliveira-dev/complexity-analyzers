namespace ComplexityAnalysis.Analyzers.Analysis;

internal enum HalsteadOperandKind
{
    Identifier,
    Parameter,
    Local,
    Field,
    Property,
    Method,
    Event,
    TypeName,
    NumericLiteral,
    StringLiteral,
    CharacterLiteral,
    BooleanLiteral,
    NullLiteral,
    Constant,
    PatternVariable,
    Discard,
}

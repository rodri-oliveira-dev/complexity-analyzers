using System;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class LoopBoundAnalyzer
{
    private readonly MethodAnalysisContext context;

    internal LoopBoundAnalyzer(MethodAnalysisContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    internal LoopBoundAnalysisResult AnalyzeFor(ForStatementSyntax forStatement)
    {
        _ = forStatement ?? throw new ArgumentNullException(nameof(forStatement));

        CancellationToken cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        return !TryGetLoopVariable(forStatement, out ISymbol? loopVariable, out ExpressionSyntax? initializer)
            || loopVariable is null
            || initializer is null
            || !TryResolveBoundExpression(initializer, out BoundExpression initialBound)
            || !TryAnalyzeCondition(forStatement.Condition, loopVariable, out ForConditionAnalysis condition)
            || !TryAnalyzeIncrementors(forStatement.Incrementors, loopVariable, out LoopDirection progressDirection)
            || condition.Direction != progressDirection
            || !HasSupportedLinearRange(initialBound, condition)
            ? LoopBoundAnalysisResult.Unknown()
            : condition.Direction == LoopDirection.Increasing
            ? CreateLinearResult(condition.Bound)
            : CreateLinearResult(initialBound);
    }

    internal LoopBoundAnalysisResult AnalyzeForEach(ForEachStatementSyntax forEachStatement)
    {
        _ = forEachStatement ?? throw new ArgumentNullException(nameof(forEachStatement));

        context.CancellationToken.ThrowIfCancellationRequested();

        return !TryResolveInputDimension(forEachStatement.Expression, out ComplexityVariable? variable)
            || variable is null
            ? LoopBoundAnalysisResult.Unknown()
            : LoopBoundAnalysisResult.Linear(variable);
    }

    private static LoopBoundAnalysisResult CreateLinearResult(BoundExpression bound)
    {
        return bound.Variable is null
            ? LoopBoundAnalysisResult.ConstantBound()
            : LoopBoundAnalysisResult.Linear(bound.Variable);
    }

    private static bool HasSupportedLinearRange(
        BoundExpression initialBound,
        ForConditionAnalysis condition)
    {
        return condition.Direction == LoopDirection.Increasing
            ? initialBound.Variable is null
            : condition.Bound.Variable is null;
    }

    private bool TryAnalyzeCondition(
        ExpressionSyntax? condition,
        ISymbol loopVariable,
        out ForConditionAnalysis analysis)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        analysis = default;

        if (condition is not BinaryExpressionSyntax binary
            || !IsLoopComparison(binary.Kind()))
        {
            return false;
        }

        ExpressionSyntax left = UnwrapParentheses(binary.Left);
        ExpressionSyntax right = UnwrapParentheses(binary.Right);
        bool variableOnLeft = IsSymbolReference(left, loopVariable);
        bool variableOnRight = IsSymbolReference(right, loopVariable);

        if (variableOnLeft == variableOnRight)
        {
            return false;
        }

        ExpressionSyntax boundExpression = variableOnLeft ? right : left;
        if (!TryResolveBoundExpression(boundExpression, out BoundExpression bound))
        {
            return false;
        }

        LoopDirection direction = GetConditionDirection(binary.Kind(), variableOnLeft);
        analysis = new ForConditionAnalysis(direction, bound);
        return true;
    }

    private bool TryAnalyzeIncrementors(
        SeparatedSyntaxList<ExpressionSyntax> incrementors,
        ISymbol loopVariable,
        out LoopDirection direction)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        direction = default;

        if (incrementors.Count != 1)
        {
            return false;
        }

        ExpressionSyntax incrementor = UnwrapParentheses(incrementors[0]);

        if (incrementor is PostfixUnaryExpressionSyntax postfixUnary
            && IsSymbolReference(postfixUnary.Operand, loopVariable))
        {
            if (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression))
            {
                direction = LoopDirection.Increasing;
                return true;
            }

            if (postfixUnary.IsKind(SyntaxKind.PostDecrementExpression))
            {
                direction = LoopDirection.Decreasing;
                return true;
            }
        }

        if (incrementor is PrefixUnaryExpressionSyntax prefixUnary
            && IsSymbolReference(prefixUnary.Operand, loopVariable))
        {
            if (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression))
            {
                direction = LoopDirection.Increasing;
                return true;
            }

            if (prefixUnary.IsKind(SyntaxKind.PreDecrementExpression))
            {
                direction = LoopDirection.Decreasing;
                return true;
            }
        }

        if (incrementor is AssignmentExpressionSyntax assignment
            && IsSymbolReference(assignment.Left, loopVariable)
            && TryGetPositiveIntegerConstant(assignment.Right, out _))
        {
            if (assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            {
                direction = LoopDirection.Increasing;
                return true;
            }

            if (assignment.IsKind(SyntaxKind.SubtractAssignmentExpression))
            {
                direction = LoopDirection.Decreasing;
                return true;
            }
        }

        return false;
    }

    private bool TryGetLoopVariable(
        ForStatementSyntax forStatement,
        out ISymbol? loopVariable,
        out ExpressionSyntax? initializer)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        loopVariable = null;
        initializer = null;

        if (forStatement.Declaration is null
            || forStatement.Declaration.Variables.Count != 1)
        {
            return false;
        }

        VariableDeclaratorSyntax variable = forStatement.Declaration.Variables[0];
        if (variable.Initializer?.Value is null)
        {
            return false;
        }

        loopVariable = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken);
        initializer = variable.Initializer.Value;

        return loopVariable is not null;
    }

    private bool TryResolveBoundExpression(ExpressionSyntax expression, out BoundExpression bound)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        bound = default;

        if (TryGetNonNegativeIntegerConstant(expression, out _))
        {
            bound = BoundExpression.Constant;
            return true;
        }

        if (TryResolveInputDimension(expression, out ComplexityVariable? variable))
        {
            bound = new BoundExpression(variable);
            return true;
        }

        return false;
    }

    private bool TryResolveInputDimension(ExpressionSyntax expression, out ComplexityVariable? variable)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        variable = null;

        if (expression is IdentifierNameSyntax)
        {
            SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken);
            if (symbolInfo.Symbol is not null
                && context.TryGetInputSizeVariable(symbolInfo.Symbol, out ComplexityVariable directVariable))
            {
                variable = directVariable;
                return true;
            }

            return false;
        }

        if (expression is MemberAccessExpressionSyntax memberAccess
            && StringComparer.Ordinal.Equals(memberAccess.Name.Identifier.ValueText, "Length")
            && TryResolveInputDimension(memberAccess.Expression, out ComplexityVariable? receiverVariable)
            && IsArrayOrString(memberAccess.Expression))
        {
            variable = receiverVariable;
            return true;
        }

        return false;
    }

    private bool IsSymbolReference(ExpressionSyntax expression, ISymbol symbol)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken);

        return symbolInfo.Symbol is not null
            && SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, symbol);
    }

    private bool IsArrayOrString(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
        return type?.TypeKind == TypeKind.Array
            || type?.SpecialType == SpecialType.System_String;
    }

    private bool TryGetNonNegativeIntegerConstant(ExpressionSyntax expression, out long value)
    {
        if (TryGetIntegerConstant(expression, out value)
            && value >= 0)
        {
            return true;
        }

        value = default;
        return false;
    }

    private bool TryGetPositiveIntegerConstant(ExpressionSyntax expression, out long value)
    {
        if (TryGetIntegerConstant(expression, out value)
            && value > 0)
        {
            return true;
        }

        value = default;
        return false;
    }

    private bool TryGetIntegerConstant(ExpressionSyntax expression, out long value)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        Optional<object?> constantValue = context.SemanticModel.GetConstantValue(
            UnwrapParentheses(expression),
            context.CancellationToken);

        if (constantValue.HasValue
            && constantValue.Value is not null
            && TryConvertIntegerConstant(constantValue.Value, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryConvertIntegerConstant(object value, out long integer)
    {
        switch (value)
        {
            case sbyte sbyteValue:
                integer = sbyteValue;
                return true;
            case byte byteValue:
                integer = byteValue;
                return true;
            case short shortValue:
                integer = shortValue;
                return true;
            case ushort ushortValue:
                integer = ushortValue;
                return true;
            case int intValue:
                integer = intValue;
                return true;
            case uint uintValue:
                integer = uintValue;
                return true;
            case long longValue:
                integer = longValue;
                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                integer = (long)ulongValue;
                return true;
            default:
                integer = default;
                return false;
        }
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool IsLoopComparison(SyntaxKind kind)
    {
        return kind is SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression;
    }

    private static LoopDirection GetConditionDirection(SyntaxKind kind, bool variableOnLeft)
    {
        return kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            ? variableOnLeft
                ? LoopDirection.Increasing
                : LoopDirection.Decreasing
            : kind is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
            ? variableOnLeft
                ? LoopDirection.Decreasing
                : LoopDirection.Increasing
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported loop comparison.");
    }

    private readonly struct BoundExpression
    {
        internal BoundExpression(ComplexityVariable? variable)
        {
            Variable = variable;
        }

        internal ComplexityVariable? Variable
        {
            get;
        }

        internal static BoundExpression Constant
        {
            get;
        } = new(null);
    }

    private readonly struct ForConditionAnalysis
    {
        internal ForConditionAnalysis(LoopDirection direction, BoundExpression bound)
        {
            Direction = direction;
            Bound = bound;
        }

        internal LoopDirection Direction
        {
            get;
        }

        internal BoundExpression Bound
        {
            get;
        }
    }

    private enum LoopDirection
    {
        Increasing,
        Decreasing,
    }
}

internal sealed class LoopBoundAnalysisResult
{
    private LoopBoundAnalysisResult(
        bool isAnalyzable,
        ComplexityExpression iterationComplexity,
        ComplexityVariable? dimension,
        LoopBoundPattern pattern,
        bool isConstantBound)
    {
        IsAnalyzable = isAnalyzable;
        IterationComplexity = iterationComplexity;
        Dimension = dimension;
        Pattern = pattern;
        IsConstantBound = isConstantBound;
    }

    internal bool IsAnalyzable
    {
        get;
    }

    internal ComplexityExpression IterationComplexity
    {
        get;
    }

    internal ComplexityVariable? Dimension
    {
        get;
    }

    internal LoopBoundPattern Pattern
    {
        get;
    }

    internal bool IsConstantBound
    {
        get;
    }

    internal static LoopBoundAnalysisResult Linear(ComplexityVariable variable)
    {
        _ = variable ?? throw new ArgumentNullException(nameof(variable));

        return new LoopBoundAnalysisResult(
            true,
            ComplexityFactory.Linear(variable),
            variable,
            LoopBoundPattern.Linear,
            false);
    }

    internal static LoopBoundAnalysisResult ConstantBound()
    {
        return new LoopBoundAnalysisResult(
            true,
            ComplexityFactory.Constant(),
            null,
            LoopBoundPattern.Linear,
            true);
    }

    internal static LoopBoundAnalysisResult Unknown()
    {
        return new LoopBoundAnalysisResult(
            false,
            ComplexityFactory.Unknown(),
            null,
            LoopBoundPattern.Unknown,
            false);
    }
}

internal enum LoopBoundPattern
{
    Unknown,
    Linear,
}

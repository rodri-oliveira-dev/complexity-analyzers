using System;
using System.Linq;
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
            || !TryResolveBoundExpression(initializer, out LoopBoundExpression initialBound)
            || !TryAnalyzeCondition(forStatement.Condition, loopVariable, out LoopConditionAnalysis condition)
            || !TryAnalyzeIncrementors(forStatement.Incrementors, loopVariable, out LoopProgression progression)
            ? LoopBoundAnalysisResult.Unknown()
            : CreateLoopResult(initialBound, condition, progression);
    }

    internal LoopBoundAnalysisResult AnalyzeForEach(ForEachStatementSyntax forEachStatement)
    {
        _ = forEachStatement ?? throw new ArgumentNullException(nameof(forEachStatement));

        context.CancellationToken.ThrowIfCancellationRequested();

        if (new KnownOperationComplexityAnalyzer(context).TryAnalyzeForEachSequence(
            forEachStatement.Expression,
            out LoopBoundAnalysisResult knownOperationResult))
        {
            return knownOperationResult;
        }

        return !TryResolveInputDimension(forEachStatement.Expression, out ComplexityVariable? variable)
            || variable is null
            ? LoopBoundAnalysisResult.Unknown()
            : LoopBoundAnalysisResult.Linear(variable);
    }

    internal LoopBoundAnalysisResult AnalyzeWhile(WhileStatementSyntax whileStatement)
    {
        _ = whileStatement ?? throw new ArgumentNullException(nameof(whileStatement));

        return AnalyzeConditionControlledLoop(whileStatement.Condition, whileStatement.Statement);
    }

    internal LoopBoundAnalysisResult AnalyzeDoWhile(DoStatementSyntax doStatement)
    {
        _ = doStatement ?? throw new ArgumentNullException(nameof(doStatement));

        return AnalyzeConditionControlledLoop(doStatement.Condition, doStatement.Statement);
    }

    internal bool TryResolveBoundExpression(ExpressionSyntax expression, out LoopBoundExpression bound)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        bound = default;

        if (TryGetNonNegativeIntegerConstant(expression, out long constantValue))
        {
            bound = LoopBoundExpression.Constant(constantValue);
            return true;
        }

        if (TryResolveInputDimension(expression, out ComplexityVariable? variable)
            && variable is not null)
        {
            bound = LoopBoundExpression.VariableBound(variable);
            return true;
        }

        return false;
    }

    private LoopBoundAnalysisResult AnalyzeConditionControlledLoop(
        ExpressionSyntax condition,
        StatementSyntax body)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        return !TryGetConditionLoopVariable(condition, out ISymbol? loopVariable)
            || loopVariable is null
            || !context.TryGetLocalLoopBound(loopVariable, out LoopBoundExpression initialBound)
            || !TryAnalyzeCondition(condition, loopVariable, out LoopConditionAnalysis conditionAnalysis)
            || !TryAnalyzeBodyProgression(body, loopVariable, out LoopProgression progression)
            ? LoopBoundAnalysisResult.Unknown()
            : CreateLoopResult(initialBound, conditionAnalysis, progression);
    }

    private static LoopBoundAnalysisResult CreateLoopResult(
        LoopBoundExpression initialBound,
        LoopConditionAnalysis condition,
        LoopProgression progression)
    {
        return condition.Direction != progression.Direction
            ? LoopBoundAnalysisResult.Unknown()
            : progression.Pattern switch
            {
                LoopBoundPattern.Unknown => LoopBoundAnalysisResult.Unknown(),
                LoopBoundPattern.Linear => CreateLinearResult(initialBound, condition),
                LoopBoundPattern.Logarithmic => CreateLogarithmicResult(initialBound, condition),
                _ => LoopBoundAnalysisResult.Unknown(),
            };
    }

    private static LoopBoundAnalysisResult CreateLinearResult(
        LoopBoundExpression initialBound,
        LoopConditionAnalysis condition)
    {
        if (condition.Direction == LoopDirection.Increasing)
        {
            return initialBound.IsConstant
                ? condition.Bound.IsVariable
                    ? LoopBoundAnalysisResult.Linear(condition.Bound.Variable!)
                    : LoopBoundAnalysisResult.ConstantBound()
                : LoopBoundAnalysisResult.Unknown();
        }

        return condition.Bound.IsConstant
            ? initialBound.IsVariable
                ? LoopBoundAnalysisResult.Linear(initialBound.Variable!)
                : LoopBoundAnalysisResult.ConstantBound()
            : LoopBoundAnalysisResult.Unknown();
    }

    private static LoopBoundAnalysisResult CreateLogarithmicResult(
        LoopBoundExpression initialBound,
        LoopConditionAnalysis condition)
    {
        if (condition.Direction == LoopDirection.Increasing)
        {
            return initialBound.IsPositiveConstant
                ? condition.Bound.IsVariable
                    ? LoopBoundAnalysisResult.Logarithmic(condition.Bound.Variable!)
                    : LoopBoundAnalysisResult.ConstantBound()
                : LoopBoundAnalysisResult.Unknown();
        }

        return condition.Bound.IsConstant
            ? initialBound.IsVariable
                ? LoopBoundAnalysisResult.Logarithmic(initialBound.Variable!)
                : LoopBoundAnalysisResult.ConstantBound()
            : LoopBoundAnalysisResult.Unknown();
    }

    private bool TryAnalyzeCondition(
        ExpressionSyntax? condition,
        ISymbol loopVariable,
        out LoopConditionAnalysis analysis)
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
        if (!TryResolveBoundExpression(boundExpression, out LoopBoundExpression bound))
        {
            return false;
        }

        LoopDirection direction = GetConditionDirection(binary.Kind(), variableOnLeft);
        analysis = new LoopConditionAnalysis(direction, bound);
        return true;
    }

    private bool TryAnalyzeIncrementors(
        SeparatedSyntaxList<ExpressionSyntax> incrementors,
        ISymbol loopVariable,
        out LoopProgression progression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        progression = default;

        return incrementors.Count == 1
            && TryAnalyzeProgression(incrementors[0], loopVariable, out progression);
    }

    private bool TryAnalyzeBodyProgression(
        StatementSyntax body,
        ISymbol loopVariable,
        out LoopProgression progression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        progression = default;
        bool found = false;

        return TryAnalyzeUnconditionalProgressionStatement(body, loopVariable, ref found, ref progression)
            && found;
    }

    private bool TryAnalyzeUnconditionalProgressionStatement(
        StatementSyntax statement,
        ISymbol loopVariable,
        ref bool found,
        ref LoopProgression progression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (statement is BlockSyntax block)
        {
            foreach (StatementSyntax childStatement in block.Statements)
            {
                if (!TryAnalyzeUnconditionalProgressionStatement(childStatement, loopVariable, ref found, ref progression))
                {
                    return false;
                }
            }

            return true;
        }

        if (statement is ExpressionStatementSyntax expressionStatement)
        {
            ExpressionSyntax expression = expressionStatement.Expression;
            if (!TargetsLoopVariableMutation(expression, loopVariable))
            {
                return !StatementContainsLoopVariableMutation(statement, loopVariable);
            }

            if (found
                || !TryAnalyzeProgression(expression, loopVariable, out progression))
            {
                progression = default;
                return false;
            }

            found = true;
            return true;
        }

        return !StatementContainsLoopVariableMutation(statement, loopVariable);
    }

    private bool StatementContainsLoopVariableMutation(StatementSyntax statement, ISymbol loopVariable)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        foreach (ExpressionSyntax expression in statement.DescendantNodes().OfType<ExpressionSyntax>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TargetsLoopVariableMutation(expression, loopVariable))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryAnalyzeProgression(
        ExpressionSyntax expression,
        ISymbol loopVariable,
        out LoopProgression progression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        progression = default;

        if (expression is PostfixUnaryExpressionSyntax postfixUnary
            && IsSymbolReference(postfixUnary.Operand, loopVariable))
        {
            if (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression))
            {
                progression = LoopProgression.Linear(LoopDirection.Increasing);
                return true;
            }

            if (postfixUnary.IsKind(SyntaxKind.PostDecrementExpression))
            {
                progression = LoopProgression.Linear(LoopDirection.Decreasing);
                return true;
            }
        }

        if (expression is PrefixUnaryExpressionSyntax prefixUnary
            && IsSymbolReference(prefixUnary.Operand, loopVariable))
        {
            if (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression))
            {
                progression = LoopProgression.Linear(LoopDirection.Increasing);
                return true;
            }

            if (prefixUnary.IsKind(SyntaxKind.PreDecrementExpression))
            {
                progression = LoopProgression.Linear(LoopDirection.Decreasing);
                return true;
            }
        }

        return expression is AssignmentExpressionSyntax assignment
            && IsSymbolReference(assignment.Left, loopVariable)
            && TryAnalyzeAssignmentProgression(assignment, loopVariable, out progression);
    }

    private bool TryAnalyzeAssignmentProgression(
        AssignmentExpressionSyntax assignment,
        ISymbol loopVariable,
        out LoopProgression progression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        progression = default;

        if (TryGetPositiveIntegerConstant(assignment.Right, out _))
        {
            if (assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            {
                progression = LoopProgression.Linear(LoopDirection.Increasing);
                return true;
            }

            if (assignment.IsKind(SyntaxKind.SubtractAssignmentExpression))
            {
                progression = LoopProgression.Linear(LoopDirection.Decreasing);
                return true;
            }
        }

        if (TryGetIntegerConstantGreaterThanOne(assignment.Right, out _))
        {
            if (assignment.IsKind(SyntaxKind.MultiplyAssignmentExpression))
            {
                progression = LoopProgression.Logarithmic(LoopDirection.Increasing);
                return true;
            }

            if (assignment.IsKind(SyntaxKind.DivideAssignmentExpression))
            {
                progression = LoopProgression.Logarithmic(LoopDirection.Decreasing);
                return true;
            }
        }

        return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Right is BinaryExpressionSyntax binary
            && TryAnalyzeSelfAssignment(binary, loopVariable, out progression);
    }

    private bool TryAnalyzeSelfAssignment(
        BinaryExpressionSyntax binary,
        ISymbol loopVariable,
        out LoopProgression progression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        progression = default;

        if (binary.IsKind(SyntaxKind.AddExpression))
        {
            if ((IsSymbolReference(binary.Left, loopVariable)
                    && TryGetPositiveIntegerConstant(binary.Right, out _))
                || (IsSymbolReference(binary.Right, loopVariable)
                    && TryGetPositiveIntegerConstant(binary.Left, out _)))
            {
                progression = LoopProgression.Linear(LoopDirection.Increasing);
                return true;
            }
        }

        if (binary.IsKind(SyntaxKind.SubtractExpression)
            && IsSymbolReference(binary.Left, loopVariable)
            && TryGetPositiveIntegerConstant(binary.Right, out _))
        {
            progression = LoopProgression.Linear(LoopDirection.Decreasing);
            return true;
        }

        if (binary.IsKind(SyntaxKind.MultiplyExpression))
        {
            if ((IsSymbolReference(binary.Left, loopVariable)
                    && TryGetIntegerConstantGreaterThanOne(binary.Right, out _))
                || (IsSymbolReference(binary.Right, loopVariable)
                    && TryGetIntegerConstantGreaterThanOne(binary.Left, out _)))
            {
                progression = LoopProgression.Logarithmic(LoopDirection.Increasing);
                return true;
            }
        }

        if (binary.IsKind(SyntaxKind.DivideExpression)
            && IsSymbolReference(binary.Left, loopVariable)
            && TryGetIntegerConstantGreaterThanOne(binary.Right, out _))
        {
            progression = LoopProgression.Logarithmic(LoopDirection.Decreasing);
            return true;
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

    private bool TryGetConditionLoopVariable(ExpressionSyntax condition, out ISymbol? loopVariable)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        loopVariable = null;

        if (condition is not BinaryExpressionSyntax binary
            || !IsLoopComparison(binary.Kind()))
        {
            return false;
        }

        ISymbol? leftSymbol = TryGetReferencedSymbol(binary.Left);
        ISymbol? rightSymbol = TryGetReferencedSymbol(binary.Right);

        if (leftSymbol is not null && rightSymbol is null)
        {
            loopVariable = leftSymbol;
            return true;
        }

        if (rightSymbol is not null && leftSymbol is null)
        {
            loopVariable = rightSymbol;
            return true;
        }

        return false;
    }

    private bool TryResolveInputDimension(ExpressionSyntax expression, out ComplexityVariable? variable)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        if (new KnownOperationComplexityAnalyzer(context).TryResolveInputDimension(expression, out variable)
            && variable is not null)
        {
            return true;
        }

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

            if (symbolInfo.Symbol is not null
                && context.TryGetLocalLoopBound(symbolInfo.Symbol, out LoopBoundExpression localBound)
                && localBound.IsVariable)
            {
                variable = localBound.Variable;
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

    private bool TargetsLoopVariableMutation(ExpressionSyntax expression, ISymbol loopVariable)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);

        return expression switch
        {
            AssignmentExpressionSyntax assignment => IsSymbolReference(assignment.Left, loopVariable),
            PostfixUnaryExpressionSyntax postfixUnary when postfixUnary.IsKind(SyntaxKind.PostIncrementExpression)
                || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression) => IsSymbolReference(postfixUnary.Operand, loopVariable),
            PrefixUnaryExpressionSyntax prefixUnary when prefixUnary.IsKind(SyntaxKind.PreIncrementExpression)
                || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression) => IsSymbolReference(prefixUnary.Operand, loopVariable),
            _ => false,
        };
    }

    private ISymbol? TryGetReferencedSymbol(ExpressionSyntax expression)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        expression = UnwrapParentheses(expression);
        if (expression is not IdentifierNameSyntax)
        {
            return null;
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken);
        return symbolInfo.Symbol is ILocalSymbol
            ? symbolInfo.Symbol
            : null;
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

    private bool TryGetIntegerConstantGreaterThanOne(ExpressionSyntax expression, out long value)
    {
        if (TryGetIntegerConstant(expression, out value)
            && value > 1)
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

    private readonly struct LoopConditionAnalysis
    {
        internal LoopConditionAnalysis(LoopDirection direction, LoopBoundExpression bound)
        {
            Direction = direction;
            Bound = bound;
        }

        internal LoopDirection Direction
        {
            get;
        }

        internal LoopBoundExpression Bound
        {
            get;
        }
    }

    private readonly struct LoopProgression
    {
        private LoopProgression(LoopDirection direction, LoopBoundPattern pattern)
        {
            Direction = direction;
            Pattern = pattern;
        }

        internal LoopDirection Direction
        {
            get;
        }

        internal LoopBoundPattern Pattern
        {
            get;
        }

        internal static LoopProgression Linear(LoopDirection direction)
        {
            return new LoopProgression(direction, LoopBoundPattern.Linear);
        }

        internal static LoopProgression Logarithmic(LoopDirection direction)
        {
            return new LoopProgression(direction, LoopBoundPattern.Logarithmic);
        }
    }

    private enum LoopDirection
    {
        Increasing,
        Decreasing,
    }
}

internal readonly struct LoopBoundExpression
{
    private LoopBoundExpression(ComplexityVariable? variable, long? constantValue)
    {
        Variable = variable;
        ConstantValue = constantValue;
    }

    internal ComplexityVariable? Variable
    {
        get;
    }

    internal long? ConstantValue
    {
        get;
    }

    internal bool IsConstant => ConstantValue.HasValue;

    internal bool IsPositiveConstant => ConstantValue > 0;

    internal bool IsVariable => Variable is not null;

    internal static LoopBoundExpression Constant(long value)
    {
        return new LoopBoundExpression(null, value);
    }

    internal static LoopBoundExpression VariableBound(ComplexityVariable variable)
    {
        _ = variable ?? throw new ArgumentNullException(nameof(variable));

        return new LoopBoundExpression(variable, null);
    }
}

internal sealed class LoopBoundAnalysisResult
{
    private LoopBoundAnalysisResult(
        bool isAnalyzable,
        ComplexityExpression iterationComplexity,
        ComplexityExpression enumerationComplexity,
        ComplexityVariable? dimension,
        LoopBoundPattern pattern,
        bool isConstantBound)
    {
        IsAnalyzable = isAnalyzable;
        IterationComplexity = iterationComplexity;
        EnumerationComplexity = enumerationComplexity;
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

    internal ComplexityExpression EnumerationComplexity
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
            ComplexityFactory.Constant(),
            variable,
            LoopBoundPattern.Linear,
            false);
    }

    internal static LoopBoundAnalysisResult Logarithmic(ComplexityVariable variable)
    {
        _ = variable ?? throw new ArgumentNullException(nameof(variable));

        return new LoopBoundAnalysisResult(
            true,
            ComplexityFactory.LogN(variable),
            ComplexityFactory.Constant(),
            variable,
            LoopBoundPattern.Logarithmic,
            false);
    }

    internal static LoopBoundAnalysisResult Enumerable(
        ComplexityExpression iterationComplexity,
        ComplexityExpression enumerationComplexity)
    {
        _ = iterationComplexity ?? throw new ArgumentNullException(nameof(iterationComplexity));
        _ = enumerationComplexity ?? throw new ArgumentNullException(nameof(enumerationComplexity));

        return new LoopBoundAnalysisResult(
            true,
            iterationComplexity,
            enumerationComplexity,
            null,
            LoopBoundPattern.Linear,
            false);
    }

    internal static LoopBoundAnalysisResult ConstantBound()
    {
        return new LoopBoundAnalysisResult(
            true,
            ComplexityFactory.Constant(),
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
    Logarithmic,
}

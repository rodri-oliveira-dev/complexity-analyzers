using System;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Configuration;

internal sealed class ComplexityThreshold : IEquatable<ComplexityThreshold>
{
    internal static readonly ComplexityThreshold None = new(ComplexityThresholdKind.None);
    internal static readonly ComplexityThreshold Constant = new(ComplexityThresholdKind.Constant);
    internal static readonly ComplexityThreshold LogN = new(ComplexityThresholdKind.LogN);
    internal static readonly ComplexityThreshold Linear = new(ComplexityThresholdKind.N);
    internal static readonly ComplexityThreshold NLogN = new(ComplexityThresholdKind.NLogN);
    internal static readonly ComplexityThreshold Quadratic = new(ComplexityThresholdKind.N2);
    internal static readonly ComplexityThreshold Cubic = new(ComplexityThresholdKind.N3);
    internal static readonly ComplexityThreshold Exponential = new(ComplexityThresholdKind.Exponential);
    internal static readonly ComplexityThreshold Factorial = new(ComplexityThresholdKind.Factorial);

    private ComplexityThreshold(ComplexityThresholdKind kind)
    {
        Kind = kind;
    }

    internal ComplexityThresholdKind Kind
    {
        get;
    }

    internal bool TryCreateExpression(out ComplexityExpression expression)
    {
        switch (Kind)
        {
            case ComplexityThresholdKind.None:
                expression = ComplexityFactory.Unknown();
                return false;
            case ComplexityThresholdKind.Constant:
                expression = ComplexityFactory.Constant();
                return true;
            case ComplexityThresholdKind.LogN:
                expression = ComplexityFactory.LogN(ComplexityVariable.N);
                return true;
            case ComplexityThresholdKind.N:
                expression = ComplexityFactory.Linear(ComplexityVariable.N);
                return true;
            case ComplexityThresholdKind.NLogN:
                expression = ComplexityFactory.NLogN(ComplexityVariable.N);
                return true;
            case ComplexityThresholdKind.N2:
                expression = ComplexityFactory.Polynomial(ComplexityVariable.N, 2);
                return true;
            case ComplexityThresholdKind.N3:
                expression = ComplexityFactory.Polynomial(ComplexityVariable.N, 3);
                return true;
            case ComplexityThresholdKind.Exponential:
                expression = ComplexityFactory.Exponential(ComplexityVariable.N, 2);
                return true;
            case ComplexityThresholdKind.Factorial:
                expression = ComplexityFactory.Factorial(ComplexityVariable.N);
                return true;
            default:
                expression = ComplexityFactory.Unknown();
                return false;
        }
    }

    internal static ComplexityThreshold ParseOrDefault(string value, ComplexityThreshold defaultValue)
    {
        _ = defaultValue ?? throw new ArgumentNullException(nameof(defaultValue));

        return value.Trim() switch
        {
            "none" => None,
            "constant" => Constant,
            "log_n" => LogN,
            "n" => Linear,
            "n_log_n" => NLogN,
            "n2" => Quadratic,
            "n3" => Cubic,
            "exponential" => Exponential,
            "factorial" => Factorial,
            _ => defaultValue,
        };
    }

    public bool Equals(ComplexityThreshold? other)
    {
        return other is not null && Kind == other.Kind;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComplexityThreshold);
    }

    public override int GetHashCode()
    {
        return Kind.GetHashCode();
    }

    public override string ToString()
    {
        return Kind switch
        {
            ComplexityThresholdKind.None => "none",
            ComplexityThresholdKind.Constant => "constant",
            ComplexityThresholdKind.LogN => "log_n",
            ComplexityThresholdKind.N => "n",
            ComplexityThresholdKind.NLogN => "n_log_n",
            ComplexityThresholdKind.N2 => "n2",
            ComplexityThresholdKind.N3 => "n3",
            ComplexityThresholdKind.Exponential => "exponential",
            ComplexityThresholdKind.Factorial => "factorial",
            _ => throw new InvalidOperationException("Unknown complexity threshold kind."),
        };
    }
}

internal enum ComplexityThresholdKind
{
    None,
    Constant,
    LogN,
    N,
    NLogN,
    N2,
    N3,
    Exponential,
    Factorial,
}

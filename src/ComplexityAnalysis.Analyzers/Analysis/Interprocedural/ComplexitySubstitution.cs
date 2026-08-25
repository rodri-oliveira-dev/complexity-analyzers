using System;
using System.Collections.Immutable;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.Interprocedural;

internal static class ComplexitySubstitution
{
    internal static ComplexityExpression Substitute(
        ComplexityExpression expression,
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings,
        CancellationToken cancellationToken)
    {
        _ = expression ?? throw new ArgumentNullException(nameof(expression));
        _ = bindings ?? throw new ArgumentNullException(nameof(bindings));

        cancellationToken.ThrowIfCancellationRequested();

        return expression switch
        {
            ConstantComplexity => expression,
            UnknownComplexity => ComplexityFactory.Unknown(),
            PolynomialLogComplexity polynomialLog => SubstitutePolynomialLog(polynomialLog, bindings, cancellationToken),
            ExponentialComplexity exponential => SubstituteExponential(exponential, bindings, cancellationToken),
            FactorialComplexity factorial => SubstituteFactorial(factorial, bindings, cancellationToken),
            CompositeComplexity composite => SubstituteComposite(composite, bindings, cancellationToken),
            _ => ComplexityFactory.Unknown(),
        };
    }

    private static ComplexityExpression SubstitutePolynomialLog(
        PolynomialLogComplexity expression,
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings,
        CancellationToken cancellationToken)
    {
        return TryResolveBinding(expression.Variable, bindings, cancellationToken, out BindingResolution binding)
            ? binding.Kind switch
            {
                BindingResolutionKind.Constant => ComplexityFactory.Constant(),
                BindingResolutionKind.Variable => new PolynomialLogComplexity(
                    binding.Variable!,
                    expression.PolynomialDegree,
                    expression.LogExponent),
                BindingResolutionKind.Unknown => ComplexityFactory.Unknown(),
                _ => ComplexityFactory.Unknown(),
            }
            : ComplexityFactory.Unknown();
    }

    private static ComplexityExpression SubstituteExponential(
        ExponentialComplexity expression,
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings,
        CancellationToken cancellationToken)
    {
        return TryResolveBinding(expression.Variable, bindings, cancellationToken, out BindingResolution binding)
            ? binding.Kind switch
            {
                BindingResolutionKind.Constant => ComplexityFactory.Constant(),
                BindingResolutionKind.Variable => ComplexityFactory.Exponential(binding.Variable!, expression.Base),
                BindingResolutionKind.Unknown => ComplexityFactory.Unknown(),
                _ => ComplexityFactory.Unknown(),
            }
            : ComplexityFactory.Unknown();
    }

    private static ComplexityExpression SubstituteFactorial(
        FactorialComplexity expression,
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings,
        CancellationToken cancellationToken)
    {
        return TryResolveBinding(expression.Variable, bindings, cancellationToken, out BindingResolution binding)
            ? binding.Kind switch
            {
                BindingResolutionKind.Constant => ComplexityFactory.Constant(),
                BindingResolutionKind.Variable => ComplexityFactory.Factorial(binding.Variable!),
                BindingResolutionKind.Unknown => ComplexityFactory.Unknown(),
                _ => ComplexityFactory.Unknown(),
            }
            : ComplexityFactory.Unknown();
    }

    private static ComplexityExpression SubstituteComposite(
        CompositeComplexity expression,
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings,
        CancellationToken cancellationToken)
    {
        ComplexityExpression left = Substitute(expression.Left, bindings, cancellationToken);
        ComplexityExpression right = Substitute(expression.Right, bindings, cancellationToken);

        return expression.Operation switch
        {
            ComplexityOperation.Sequential => ComplexityComposer.Sequential(left, right),
            ComplexityOperation.Nested => ComplexityComposer.Nested(left, right),
            ComplexityOperation.Maximum => ComplexityComposer.Branching(left, right),
            _ => ComplexityFactory.Unknown(),
        };
    }

    private static bool TryResolveBinding(
        ComplexityVariable variable,
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings,
        CancellationToken cancellationToken,
        out BindingResolution resolution)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!bindings.TryGetValue(variable, out ComplexityExpression? replacement))
        {
            resolution = BindingResolution.Unknown();
            return false;
        }

        resolution = replacement switch
        {
            ConstantComplexity => BindingResolution.Constant(),
            PolynomialLogComplexity { PolynomialDegree: 1, LogExponent: 0 } polynomialLog =>
                BindingResolution.ForVariable(polynomialLog.Variable),
            UnknownComplexity => BindingResolution.Unknown(),
            _ => BindingResolution.Unknown(),
        };
        return true;
    }

    private readonly struct BindingResolution
    {
        private BindingResolution(BindingResolutionKind kind, ComplexityVariable? variable)
        {
            Kind = kind;
            Variable = variable;
        }

        internal BindingResolutionKind Kind
        {
            get;
        }

        internal ComplexityVariable? Variable
        {
            get;
        }

        internal static BindingResolution Constant()
        {
            return new BindingResolution(BindingResolutionKind.Constant, null);
        }

        internal static BindingResolution ForVariable(ComplexityVariable variable)
        {
            return new BindingResolution(BindingResolutionKind.Variable, variable);
        }

        internal static BindingResolution Unknown()
        {
            return new BindingResolution(BindingResolutionKind.Unknown, null);
        }
    }

    private enum BindingResolutionKind
    {
        Constant,
        Variable,
        Unknown,
    }
}

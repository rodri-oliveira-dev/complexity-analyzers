using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ArgumentComplexityBinderTests
{
    [Fact]
    public void Direct_argument_binding_maps_callee_n_to_caller_n()
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int[] items)
                {
                    Helper(items);
                }

                void Helper(int[] values)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(n)", result.ToBigONotation());
    }

    [Fact]
    public void Secondary_input_binding_preserves_caller_m()
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int[] first, int[] second)
                {
                    Helper(second);
                }

                void Helper(int[] values)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(m)", result.ToBigONotation());
    }

    [Fact]
    public void Multiple_parameter_binding_preserves_each_argument_dimension()
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int[] left, int[] right)
                {
                    Compare(right, left);
                }

                void Compare(int[] first, int[] second)
                {
                }
            }
            """,
            calleeName: "Compare");
        ComplexityExpression calleeTemplate = ComplexityComposer.Nested(
            ComplexityFactory.Linear(ComplexityVariable.N),
            ComplexityFactory.Linear(ComplexityVariable.M));

        ComplexityExpression result = SubstituteCalleeTemplate(facts, calleeTemplate);

        Assert.Equal("O(m \u00b7 n)", result.ToBigONotation());
    }

    [Fact]
    public void Constant_argument_reduces_size_dependent_template_to_constant()
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int[] items)
                {
                    Helper(10);
                }

                void Helper(int count)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(1)", result.ToBigONotation());
    }

    [Fact]
    public void Length_argument_binds_to_receiver_dimension()
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int[] items)
                {
                    Helper(items.Length);
                }

                void Helper(int count)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(n)", result.ToBigONotation());
    }

    [Fact]
    public void Count_argument_binds_to_known_list_receiver_dimension()
    {
        BindingFacts facts = CreateFacts(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void Caller(List<int> list)
                {
                    Helper(list.Count);
                }

                void Helper(int count)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(n)", result.ToBigONotation());
    }

    [Fact]
    public void Reduced_extension_receiver_binds_to_original_this_parameter_dimension()
    {
        BindingFacts facts = CreateFacts(
            """
            public static class Extensions
            {
                public static void Helper(this int[] values)
                {
                }
            }

            public sealed class Sample
            {
                void Caller(int[] items)
                {
                    items.Helper();
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(n)", result.ToBigONotation());
    }

    [Fact]
    public void Non_dimension_parameters_keep_positional_slots_when_binding_arguments()
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int[] items)
                {
                    Helper(true, items);
                }

                void Helper(bool enabled, int[] values)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(n)", result.ToBigONotation());
    }

    [Theory]
    [InlineData("count - 1")]
    [InlineData("count + 1")]
    [InlineData("count / 2")]
    [InlineData("count * 2")]
    [InlineData("2 * count")]
    public void Simple_affine_and_scaled_arguments_preserve_linear_dimension(string argument)
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int count)
                {
                    Helper(
            """ + argument + """
                    );
                }

                void Helper(int size)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("O(n)", result.ToBigONotation());
    }

    [Fact]
    public void Unknown_argument_relation_substitutes_to_unknown()
    {
        BindingFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void Caller(int[] items)
                {
                    Helper(GetCount());
                }

                int GetCount() => 10;

                void Helper(int count)
                {
                }
            }
            """);

        ComplexityExpression result = SubstituteCalleeTemplate(
            facts,
            ComplexityFactory.Linear(ComplexityVariable.N));

        Assert.Equal("Unknown", result.ToBigONotation());
    }

    [Fact]
    public void Composite_substitution_rewrites_each_closed_model_operand()
    {
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings =
            ImmutableDictionary.CreateRange(
                [
                    Pair(ComplexityVariable.N, ComplexityFactory.Linear(ComplexityVariable.M)),
                    Pair(ComplexityVariable.M, ComplexityFactory.Linear(ComplexityVariable.N))
                ]);
        ComplexityExpression template = ComplexityComposer.Sequential(
            ComplexityFactory.Linear(ComplexityVariable.N),
            ComplexityFactory.Linear(ComplexityVariable.M));

        ComplexityExpression result = ComplexitySubstitution.Substitute(
            template,
            bindings,
            CancellationToken.None);

        Assert.Equal("O(m + n)", result.ToBigONotation());
    }

    [Fact]
    public void Substitution_supports_all_current_complexity_expression_shapes()
    {
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings =
            ImmutableDictionary.CreateRange(
                [Pair(ComplexityVariable.N, ComplexityFactory.Linear(ComplexityVariable.M))]);

        Assert.Equal("O(1)", Substitute(ComplexityFactory.Constant(), bindings));
        Assert.Equal("O(log m)", Substitute(ComplexityFactory.LogN(ComplexityVariable.N), bindings));
        Assert.Equal("O(m log m)", Substitute(ComplexityFactory.NLogN(ComplexityVariable.N), bindings));
        Assert.Equal("O(2^m)", Substitute(ComplexityFactory.Exponential(ComplexityVariable.N, 2), bindings));
        Assert.Equal("O(m!)", Substitute(ComplexityFactory.Factorial(ComplexityVariable.N), bindings));
        Assert.Equal("Unknown", Substitute(ComplexityFactory.Unknown(), bindings));
        Assert.Equal(
            "O(m\u00b2)",
            Substitute(
                ComplexityComposer.Nested(
                    ComplexityFactory.Linear(ComplexityVariable.N),
                    ComplexityFactory.Linear(ComplexityVariable.N)),
                bindings));
    }

    [Fact]
    public void Cached_template_expression_remains_unaltered_after_substitution()
    {
        ComplexityExpression template = ComplexityComposer.Sequential(
            ComplexityFactory.Linear(ComplexityVariable.N),
            ComplexityFactory.Linear(ComplexityVariable.M));
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings =
            ImmutableDictionary.CreateRange(
                [
                    Pair(ComplexityVariable.N, ComplexityFactory.Linear(ComplexityVariable.M)),
                    Pair(ComplexityVariable.M, ComplexityFactory.Constant())
                ]);

        ComplexityExpression result = ComplexitySubstitution.Substitute(
            template,
            bindings,
            CancellationToken.None);

        Assert.Equal("O(m)", result.ToBigONotation());
        Assert.Equal("O(n + m)", template.ToBigONotation());
    }

    [Fact]
    public void Substitution_formatting_is_culture_independent_and_deterministic()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
            ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings =
                ImmutableDictionary.CreateRange(
                    [Pair(ComplexityVariable.N, ComplexityFactory.Linear(ComplexityVariable.M))]);

            string first = Substitute(
                ComplexityFactory.Exponential(ComplexityVariable.N, 1.5),
                bindings);
            string second = Substitute(
                ComplexityFactory.Exponential(ComplexityVariable.N, 1.5),
                bindings);

            Assert.Equal("O(1.5^m)", first);
            Assert.Equal(first, second);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    private static ComplexityExpression SubstituteCalleeTemplate(
        BindingFacts facts,
        ComplexityExpression calleeTemplate)
    {
        MethodAnalysisContext callerContext = MethodAnalysisContext.Create(
            facts.SemanticModel,
            facts.Caller,
            CancellationToken.None);
        ImmutableDictionary<IParameterSymbol, ComplexityVariable>.Builder parameterVariables =
            ImmutableDictionary.CreateBuilder<IParameterSymbol, ComplexityVariable>(SymbolEqualityComparer.Default);
        foreach (KeyValuePair<ISymbol, ComplexityVariable> pair in new InputSizeResolver(
            facts.SemanticModel,
            CancellationToken.None).ResolveParameterVariables(facts.Callee))
        {
            if (pair.Key is IParameterSymbol parameter)
            {
                parameterVariables[parameter] = pair.Value;
            }
        }

        MethodComplexityTemplate template = new(
            calleeTemplate,
            parameterVariables.ToImmutable());
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings =
            new ArgumentComplexityBinder().Bind(
                facts.Invocation,
                facts.Target,
                facts.Callee,
                template,
                callerContext,
                CancellationToken.None);

        return ComplexitySubstitution.Substitute(
            calleeTemplate,
            bindings,
            CancellationToken.None);
    }

    private static string Substitute(
        ComplexityExpression expression,
        ImmutableDictionary<ComplexityVariable, ComplexityExpression> bindings)
    {
        return ComplexitySubstitution.Substitute(
            expression,
            bindings,
            CancellationToken.None).ToBigONotation();
    }

    private static KeyValuePair<ComplexityVariable, ComplexityExpression> Pair(
        ComplexityVariable variable,
        ComplexityExpression expression)
    {
        return new KeyValuePair<ComplexityVariable, ComplexityExpression>(variable, expression);
    }

    private static BindingFacts CreateFacts(string source, string calleeName = "Helper")
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ArgumentComplexityBinderTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        ImmutableArray<MethodDeclarationSyntax> methods =
        [
            .. syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
        ];
        IMethodSymbol caller = GetMethodSymbol(semanticModel, methods, "Caller");
        IMethodSymbol callee = GetMethodSymbol(semanticModel, methods, calleeName);
        InvocationExpressionSyntax invocation = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(invocationSyntax => semanticModel.GetSymbolInfo(
                invocationSyntax,
                CancellationToken.None).Symbol is IMethodSymbol target
                && SymbolEqualityComparer.Default.Equals(
                    (target.ReducedFrom ?? target).OriginalDefinition,
                    callee.OriginalDefinition));
        IMethodSymbol target = semanticModel.GetSymbolInfo(invocation, CancellationToken.None).Symbol as IMethodSymbol
            ?? throw new InvalidOperationException("Expected invocation to resolve to a method symbol.");

        return new BindingFacts(semanticModel, caller, callee, target, invocation);
    }

    private static IMethodSymbol GetMethodSymbol(
        SemanticModel semanticModel,
        ImmutableArray<MethodDeclarationSyntax> methods,
        string name)
    {
        return methods
            .Where(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, name))
            .Select(method => semanticModel.GetDeclaredSymbol(method, CancellationToken.None))
            .Where(symbol => symbol is not null)
            .Cast<IMethodSymbol>()
            .Single();
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } = CreateTrustedPlatformReferences();

    private static ImmutableArray<MetadataReference> CreateTrustedPlatformReferences()
    {
        string trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? string.Empty;

        return
        [
            .. trustedPlatformAssemblies.Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record BindingFacts(
        SemanticModel SemanticModel,
        IMethodSymbol Caller,
        IMethodSymbol Callee,
        IMethodSymbol Target,
        InvocationExpressionSyntax Invocation);
}

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Configuration;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityAnalyzerOptionsReaderTests
{
    [Fact]
    public void Defaults_preserve_phase_six_behavior()
    {
        ComplexityAnalyzerOptions options = ReadOptions();

        Assert.True(options.InterproceduralAnalysisEnabled);
        Assert.True(options.RecursionAnalysisEnabled);
        Assert.Equal(5, options.MaxCallDepth);
        Assert.Equal(32, options.MaxMethodsPerRoot);
        Assert.Equal(ComplexityThreshold.None, options.MaximumComplexity);
        Assert.Null(options.MaximumCyclomaticComplexity);
        Assert.Equal(CyclomaticComplexityAnalysisMode.Standard, options.CyclomaticComplexityMode);
        Assert.Null(options.MaximumNestingDepth);
        Assert.Null(options.MaximumMethodNloc);
        Assert.Null(options.MaximumStatementCount);
        Assert.Null(options.MaximumTokenCount);
        Assert.Null(options.MaximumParameters);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    [InlineData(" TrUe ", true)]
    public void Boolean_options_accept_true_false_case_insensitively(string value, bool expected)
    {
        ComplexityAnalyzerOptions options = ReadOptions(
            (ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, value),
            (ComplexityAnalyzerOptionsReader.RecursionAnalysisKey, value));

        Assert.Equal(expected, options.InterproceduralAnalysisEnabled);
        Assert.Equal(expected, options.RecursionAnalysisEnabled);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("truthy")]
    public void Invalid_boolean_options_fall_back_to_defaults(string value)
    {
        ComplexityAnalyzerOptions options = ReadOptions(
            (ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, value),
            (ComplexityAnalyzerOptionsReader.RecursionAnalysisKey, value));

        Assert.True(options.InterproceduralAnalysisEnabled);
        Assert.True(options.RecursionAnalysisEnabled);
    }

    [Theory]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "0", 0)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "16", 16)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, " 7 ", 7)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "0", 0)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "128", 128)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "64", 64)]
    public void Valid_integer_options_are_read_with_bounds(string key, string value, int expected)
    {
        ComplexityAnalyzerOptions options = ReadOptions((key, value));

        int actual = key == ComplexityAnalyzerOptionsReader.MaxCallDepthKey
            ? options.MaxCallDepth
            : options.MaxMethodsPerRoot;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "-1", 5)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "+1", 5)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "17", 5)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "not-a-number", 5)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "1.5", 5)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "1 2", 5)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "-1", 32)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "+1", 32)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "129", 32)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "not-a-number", 32)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "1.5", 32)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "1 2", 32)]
    public void Invalid_integer_options_fall_back_to_defaults(string key, string value, int expected)
    {
        ComplexityAnalyzerOptions options = ReadOptions((key, value));

        int actual = key == ComplexityAnalyzerOptionsReader.MaxCallDepthKey
            ? options.MaxCallDepth
            : options.MaxMethodsPerRoot;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("none", "none", null)]
    [InlineData("constant", "constant", "O(1)")]
    [InlineData("log_n", "log_n", "O(log n)")]
    [InlineData("n", "n", "O(n)")]
    [InlineData("n_log_n", "n_log_n", "O(n log n)")]
    [InlineData("n2", "n2", "O(n\u00b2)")]
    [InlineData("n3", "n3", "O(n\u00b3)")]
    [InlineData("exponential", "exponential", "O(2^n)")]
    [InlineData("factorial", "factorial", "O(n!)")]
    public void Maximum_complexity_accepts_only_approved_values(
        string value,
        string expectedThreshold,
        string? expectedBigO)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, value));

        Assert.Equal(expectedThreshold, options.MaximumComplexity.ToString());
        Assert.Equal(expectedBigO is not null, options.MaximumComplexity.TryCreateExpression(out ComplexityExpression expression));
        if (expectedBigO is not null)
        {
            Assert.Equal(expectedBigO, expression.ToBigONotation());
        }
    }

    [Theory]
    [InlineData("O(n^2 log n)")]
    [InlineData("n^2")]
    [InlineData("N")]
    [InlineData("linear")]
    [InlineData("")]
    public void Unknown_threshold_values_fall_back_to_default_none(string value)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, value));

        Assert.Equal(ComplexityThreshold.None, options.MaximumComplexity);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData(" 42 ", 42)]
    [InlineData("2147483647", int.MaxValue)]
    public void Maximum_cyclomatic_complexity_accepts_positive_integers(string value, int expected)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, value));

        Assert.Equal(expected, options.MaximumCyclomaticComplexity);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.5")]
    [InlineData("1 2")]
    [InlineData("")]
    [InlineData("ten")]
    public void Invalid_maximum_cyclomatic_complexity_falls_back_to_unset(string value)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, value));

        Assert.Null(options.MaximumCyclomaticComplexity);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData(" 42 ", 42)]
    [InlineData("2147483647", int.MaxValue)]
    public void Maximum_nesting_depth_accepts_non_negative_integers(string value, int expected)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.MaximumNestingDepthKey, value));

        Assert.Equal(expected, options.MaximumNestingDepth);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.5")]
    [InlineData("1 2")]
    [InlineData("999999999999")]
    [InlineData("")]
    [InlineData("ten")]
    public void Invalid_maximum_nesting_depth_falls_back_to_unset(string value)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.MaximumNestingDepthKey, value));

        Assert.Null(options.MaximumNestingDepth);
    }

    [Theory]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "0", 0)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "1", 1)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, " 42 ", 42)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "2147483647", int.MaxValue)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "0", 0)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "1", 1)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, " 42 ", 42)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "2147483647", int.MaxValue)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "0", 0)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "1", 1)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, " 42 ", 42)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "2147483647", int.MaxValue)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "0", 0)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1", 1)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, " 42 ", 42)]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "2147483647", int.MaxValue)]
    public void Method_size_thresholds_accept_non_negative_integers(
        string key,
        string value,
        int expected)
    {
        ComplexityAnalyzerOptions options = ReadOptions((key, value));

        int? actual = key switch
        {
            ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey => options.MaximumMethodNloc,
            ComplexityAnalyzerOptionsReader.MaximumStatementCountKey => options.MaximumStatementCount,
            ComplexityAnalyzerOptionsReader.MaximumTokenCountKey => options.MaximumTokenCount,
            _ => options.MaximumParameters,
        };

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "-1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "+1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "1.5")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "1 2")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "999999999999")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "ten")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "-1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "+1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "1.5")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "1 2")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "999999999999")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "ten")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "-1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "+1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "1.5")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "1 2")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "999999999999")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "ten")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "-1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "+1")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1.5")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "1 2")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "999999999999")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "")]
    [InlineData(ComplexityAnalyzerOptionsReader.MaximumParametersKey, "ten")]
    public void Invalid_method_size_thresholds_fall_back_to_unset(
        string key,
        string value)
    {
        ComplexityAnalyzerOptions options = ReadOptions((key, value));

        Assert.Null(options.MaximumMethodNloc);
        Assert.Null(options.MaximumStatementCount);
        Assert.Null(options.MaximumTokenCount);
        Assert.Null(options.MaximumParameters);
    }

    [Theory]
    [InlineData("standard", "Standard")]
    [InlineData("modified_mccabe", "ModifiedMcCabe")]
    [InlineData(" modified_mccabe ", "ModifiedMcCabe")]
    public void Cyclomatic_complexity_mode_accepts_documented_values(
        string value,
        string expected)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, value));

        Assert.Equal(expected, options.CyclomaticComplexityMode.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Standard")]
    [InlineData("modified")]
    [InlineData("mccabe")]
    public void Invalid_cyclomatic_complexity_mode_falls_back_to_standard(string value)
    {
        ComplexityAnalyzerOptions options = ReadOptions((ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, value));

        Assert.Equal(CyclomaticComplexityAnalysisMode.Standard, options.CyclomaticComplexityMode);
    }

    [Fact]
    public void Tree_specific_configuration_wins_over_global_configuration()
    {
        SyntaxTree syntaxTree = Parse("public sealed class Sample { }", "Sample.cs");
        var provider = new TestAnalyzerConfigOptionsProvider(
            GlobalOptions(
                (ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, "true"),
                (ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "4"),
                (ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n"),
                (ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "5"),
                (ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, "standard"),
                (ComplexityAnalyzerOptionsReader.MaximumNestingDepthKey, "4"),
                (ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "40"),
                (ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "41"),
                (ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "42"),
                (ComplexityAnalyzerOptionsReader.MaximumParametersKey, "43")),
            ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>.Empty.Add(
                syntaxTree,
                Options(
                    (ComplexityAnalyzerOptionsReader.InterproceduralAnalysisKey, "false"),
                    (ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "9"),
                    (ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n2"),
                    (ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "3"),
                    (ComplexityAnalyzerOptionsReader.CyclomaticComplexityModeKey, "modified_mccabe"),
                    (ComplexityAnalyzerOptionsReader.MaximumNestingDepthKey, "2"),
                    (ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "20"),
                    (ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "21"),
                    (ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "22"),
                    (ComplexityAnalyzerOptionsReader.MaximumParametersKey, "23"))));

        ComplexityAnalyzerOptions options = ComplexityAnalyzerOptionsReader.Read(provider, syntaxTree);

        Assert.False(options.InterproceduralAnalysisEnabled);
        Assert.Equal(9, options.MaxCallDepth);
        Assert.Equal(ComplexityThreshold.Quadratic, options.MaximumComplexity);
        Assert.Equal(3, options.MaximumCyclomaticComplexity);
        Assert.Equal(CyclomaticComplexityAnalysisMode.ModifiedMcCabe, options.CyclomaticComplexityMode);
        Assert.Equal(2, options.MaximumNestingDepth);
        Assert.Equal(20, options.MaximumMethodNloc);
        Assert.Equal(21, options.MaximumStatementCount);
        Assert.Equal(22, options.MaximumTokenCount);
        Assert.Equal(23, options.MaximumParameters);
    }

    [Fact]
    public void Configurations_are_independent_between_syntax_trees()
    {
        SyntaxTree firstTree = Parse("public sealed class First { }", "First.cs");
        SyntaxTree secondTree = Parse("public sealed class Second { }", "Second.cs");
        var provider = new TestAnalyzerConfigOptionsProvider(
            GlobalOptions((ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "3")),
            ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions>.Empty
                .Add(firstTree, Options((ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n")))
                .Add(secondTree, Options(
                    (ComplexityAnalyzerOptionsReader.MaximumComplexityKey, "n3"),
                    (ComplexityAnalyzerOptionsReader.MaximumCyclomaticComplexityKey, "12"),
                    (ComplexityAnalyzerOptionsReader.MaximumNestingDepthKey, "0"),
                    (ComplexityAnalyzerOptionsReader.MaximumMethodNlocKey, "10"),
                    (ComplexityAnalyzerOptionsReader.MaximumStatementCountKey, "11"),
                    (ComplexityAnalyzerOptionsReader.MaximumTokenCountKey, "12"),
                    (ComplexityAnalyzerOptionsReader.MaximumParametersKey, "13"))));

        ComplexityAnalyzerOptions firstOptions = ComplexityAnalyzerOptionsReader.Read(provider, firstTree);
        ComplexityAnalyzerOptions secondOptions = ComplexityAnalyzerOptionsReader.Read(provider, secondTree);

        Assert.Equal(3, firstOptions.MaxCallDepth);
        Assert.Equal(3, secondOptions.MaxCallDepth);
        Assert.Equal(ComplexityThreshold.Linear, firstOptions.MaximumComplexity);
        Assert.Equal(ComplexityThreshold.Cubic, secondOptions.MaximumComplexity);
        Assert.Null(firstOptions.MaximumCyclomaticComplexity);
        Assert.Equal(12, secondOptions.MaximumCyclomaticComplexity);
        Assert.Null(firstOptions.MaximumNestingDepth);
        Assert.Equal(0, secondOptions.MaximumNestingDepth);
        Assert.Null(firstOptions.MaximumMethodNloc);
        Assert.Equal(10, secondOptions.MaximumMethodNloc);
        Assert.Null(firstOptions.MaximumStatementCount);
        Assert.Equal(11, secondOptions.MaximumStatementCount);
        Assert.Null(firstOptions.MaximumTokenCount);
        Assert.Equal(12, secondOptions.MaximumTokenCount);
        Assert.Null(firstOptions.MaximumParameters);
        Assert.Equal(13, secondOptions.MaximumParameters);
    }

    [Fact]
    public void Numeric_parsing_uses_invariant_culture()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

            ComplexityAnalyzerOptions options = ReadOptions(
                (ComplexityAnalyzerOptionsReader.MaxCallDepthKey, "12"),
                (ComplexityAnalyzerOptionsReader.MaxMethodsPerRootKey, "1,5"));

            Assert.Equal(12, options.MaxCallDepth);
            Assert.Equal(32, options.MaxMethodsPerRoot);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Options_model_is_immutable_and_contains_no_syntax_nodes()
    {
        Type[] optionTypes =
        [
            typeof(ComplexityAnalyzerOptions),
            typeof(ComplexityThreshold),
        ];

        foreach (Type type in optionTypes)
        {
            Assert.True(type.IsSealed, type.Name + " should remain sealed for immutable option semantics.");

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                Assert.Null(property.SetMethod);
                Assert.False(typeof(SyntaxNode).IsAssignableFrom(property.PropertyType));
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                Assert.True(field.IsInitOnly || field.IsLiteral, type.Name + "." + field.Name + " should not be mutable shared state.");
                Assert.False(typeof(SyntaxNode).IsAssignableFrom(field.FieldType));
            }
        }
    }

    private static ComplexityAnalyzerOptions ReadOptions(params (string Key, string Value)[] globalOptions)
    {
        SyntaxTree syntaxTree = Parse("public sealed class Sample { }", "Sample.cs");
        var provider = new TestAnalyzerConfigOptionsProvider(
            GlobalOptions(globalOptions),
            []);

        return ComplexityAnalyzerOptionsReader.Read(provider, syntaxTree);
    }

    private static TestAnalyzerConfigOptions GlobalOptions(params (string Key, string Value)[] options)
    {
        return Options(options);
    }

    private static TestAnalyzerConfigOptions Options(params (string Key, string Value)[] options)
    {
        return new TestAnalyzerConfigOptions(options);
    }

    private static SyntaxTree Parse(string source, string path)
    {
        return CSharpSyntaxTree.ParseText(source, path: path);
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions();
        private readonly ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions> treeOptions;

        internal TestAnalyzerConfigOptionsProvider(
            AnalyzerConfigOptions globalOptions,
            ImmutableDictionary<SyntaxTree, AnalyzerConfigOptions> treeOptions)
        {
            GlobalOptions = globalOptions;
            this.treeOptions = treeOptions;
        }

        public override AnalyzerConfigOptions GlobalOptions
        {
            get;
        }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return treeOptions.TryGetValue(tree, out AnalyzerConfigOptions? options)
                ? options
                : EmptyOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return EmptyOptions;
        }
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> values;

        internal TestAnalyzerConfigOptions(params (string Key, string Value)[] values)
        {
            ImmutableDictionary<string, string>.Builder builder =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

            foreach ((string key, string value) in values)
            {
                builder[key] = value;
            }

            this.values = builder.ToImmutable();
        }

        public override bool TryGetValue(string key, out string value)
        {
            return values.TryGetValue(key, out value!);
        }
    }
}

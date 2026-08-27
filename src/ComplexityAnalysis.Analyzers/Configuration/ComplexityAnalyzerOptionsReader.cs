using System;
using System.Globalization;

using ComplexityAnalysis.Analyzers.Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ComplexityAnalysis.Analyzers.Configuration;

internal static class ComplexityAnalyzerOptionsReader
{
    internal const string InterproceduralAnalysisKey = "complexity_analyzers.interprocedural_analysis";
    internal const string RecursionAnalysisKey = "complexity_analyzers.recursion_analysis";
    internal const string MaxCallDepthKey = "complexity_analyzers.max_call_depth";
    internal const string MaxMethodsPerRootKey = "complexity_analyzers.max_methods_per_root";
    internal const string MaximumComplexityKey = "complexity_analyzers.maximum_complexity";
    internal const string MaximumCyclomaticComplexityKey = "complexity_analyzers.maximum_cyclomatic_complexity";
    internal const string CyclomaticComplexityModeKey = "complexity_analyzers.cyclomatic_complexity_mode";
    internal const string MaximumNestingDepthKey = "complexity_analyzers.maximum_nesting_depth";
    internal const string MaximumMethodNlocKey = "complexity_analyzers.maximum_method_nloc";
    internal const string MaximumStatementCountKey = "complexity_analyzers.maximum_statement_count";
    internal const string MaximumTokenCountKey = "complexity_analyzers.maximum_token_count";
    internal const string MaximumParametersKey = "complexity_analyzers.maximum_parameters";

    internal static ComplexityAnalyzerOptions Read(
        AnalyzerConfigOptionsProvider optionsProvider,
        SyntaxTree syntaxTree)
    {
        _ = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        _ = syntaxTree ?? throw new ArgumentNullException(nameof(syntaxTree));

        AnalyzerConfigOptions globalOptions = optionsProvider.GlobalOptions;
        AnalyzerConfigOptions treeOptions = optionsProvider.GetOptions(syntaxTree);
        ComplexityAnalyzerOptions defaults = ComplexityAnalyzerOptions.Default;

        return new ComplexityAnalyzerOptions(
            ReadBoolean(treeOptions, globalOptions, InterproceduralAnalysisKey, defaults.InterproceduralAnalysisEnabled),
            ReadBoolean(treeOptions, globalOptions, RecursionAnalysisKey, defaults.RecursionAnalysisEnabled),
            ReadInteger(treeOptions, globalOptions, MaxCallDepthKey, defaults.MaxCallDepth, ComplexityAnalyzerOptions.MaximumMaxCallDepth),
            ReadInteger(treeOptions, globalOptions, MaxMethodsPerRootKey, defaults.MaxMethodsPerRoot, ComplexityAnalyzerOptions.MaximumMaxMethodsPerRoot),
            ReadThreshold(treeOptions, globalOptions, MaximumComplexityKey, defaults.MaximumComplexity),
            ReadOptionalPositiveInteger(treeOptions, globalOptions, MaximumCyclomaticComplexityKey, defaults.MaximumCyclomaticComplexity),
            ReadCyclomaticComplexityMode(treeOptions, globalOptions, CyclomaticComplexityModeKey, defaults.CyclomaticComplexityMode),
            ReadOptionalNonNegativeInteger(treeOptions, globalOptions, MaximumNestingDepthKey, defaults.MaximumNestingDepth),
            ReadOptionalNonNegativeInteger(treeOptions, globalOptions, MaximumMethodNlocKey, defaults.MaximumMethodNloc),
            ReadOptionalNonNegativeInteger(treeOptions, globalOptions, MaximumStatementCountKey, defaults.MaximumStatementCount),
            ReadOptionalNonNegativeInteger(treeOptions, globalOptions, MaximumTokenCountKey, defaults.MaximumTokenCount),
            ReadOptionalNonNegativeInteger(treeOptions, globalOptions, MaximumParametersKey, defaults.MaximumParameters));
    }

    private static bool ReadBoolean(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        bool defaultValue)
    {
        return TryGetValue(treeOptions, globalOptions, key, out string value)
            ? ParseBooleanOrDefault(value, defaultValue)
            : defaultValue;
    }

    private static int ReadInteger(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        int defaultValue,
        int maximumValue)
    {
        return TryGetValue(treeOptions, globalOptions, key, out string value)
            ? ParseIntegerOrDefault(value, defaultValue, maximumValue)
            : defaultValue;
    }

    private static ComplexityThreshold ReadThreshold(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        ComplexityThreshold defaultValue)
    {
        return TryGetValue(treeOptions, globalOptions, key, out string value)
            ? ComplexityThreshold.ParseOrDefault(value, defaultValue)
            : defaultValue;
    }

    private static int? ReadOptionalPositiveInteger(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        int? defaultValue)
    {
        return TryGetValue(treeOptions, globalOptions, key, out string value)
            ? ParseOptionalPositiveIntegerOrDefault(value, defaultValue)
            : defaultValue;
    }

    private static int? ReadOptionalNonNegativeInteger(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        int? defaultValue)
    {
        return TryGetValue(treeOptions, globalOptions, key, out string value)
            ? ParseOptionalNonNegativeIntegerOrDefault(value, defaultValue)
            : defaultValue;
    }

    private static CyclomaticComplexityAnalysisMode ReadCyclomaticComplexityMode(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        CyclomaticComplexityAnalysisMode defaultValue)
    {
        return TryGetValue(treeOptions, globalOptions, key, out string value)
            ? ParseCyclomaticComplexityModeOrDefault(value, defaultValue)
            : defaultValue;
    }

    private static bool TryGetValue(
        AnalyzerConfigOptions treeOptions,
        AnalyzerConfigOptions globalOptions,
        string key,
        out string value)
    {
        return treeOptions.TryGetValue(key, out value!)
            || globalOptions.TryGetValue(key, out value!);
    }

    private static bool ParseBooleanOrDefault(string value, bool defaultValue)
    {
        string trimmedValue = value.Trim();
        return trimmedValue switch
        {
            string text when string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) => true,
            string text when string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) => false,
            _ => defaultValue,
        };
    }

    private static int ParseIntegerOrDefault(string value, int defaultValue, int maximumValue)
    {
        string trimmedValue = value.Trim();
        return int.TryParse(trimmedValue, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedValue)
            && parsedValue >= 0
            && parsedValue <= maximumValue
            ? parsedValue
            : defaultValue;
    }

    private static int? ParseOptionalPositiveIntegerOrDefault(string value, int? defaultValue)
    {
        string trimmedValue = value.Trim();
        return int.TryParse(trimmedValue, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedValue)
            && parsedValue >= 1
            ? parsedValue
            : defaultValue;
    }

    private static int? ParseOptionalNonNegativeIntegerOrDefault(string value, int? defaultValue)
    {
        string trimmedValue = value.Trim();
        return int.TryParse(trimmedValue, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedValue)
            && parsedValue >= 0
            ? parsedValue
            : defaultValue;
    }

    private static CyclomaticComplexityAnalysisMode ParseCyclomaticComplexityModeOrDefault(
        string value,
        CyclomaticComplexityAnalysisMode defaultValue)
    {
        string trimmedValue = value.Trim();
        return trimmedValue switch
        {
            "standard" => CyclomaticComplexityAnalysisMode.Standard,
            "modified_mccabe" => CyclomaticComplexityAnalysisMode.ModifiedMcCabe,
            _ => defaultValue,
        };
    }
}

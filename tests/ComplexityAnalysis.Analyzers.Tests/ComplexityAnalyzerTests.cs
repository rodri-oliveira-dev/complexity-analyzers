using System;
using System.Collections.Immutable;
using System.Globalization;

using ComplexityAnalysis.Analyzers.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class ComplexityAnalyzerTests
{
    private const string EstimatedAlgorithmicComplexityId = "BIG0001";
    private const string LinearLookupInsideIterationId = "BIG1001";
    private const string MaterializationInsideIterationId = "BIG1002";
    private const string OrderingInsideIterationId = "BIG1003";
    private const string InputDependentCallInsideIterationId = "BIG1004";
    private const string ExponentialRecursiveGrowthId = "BIG1005";
    private const string MethodComplexityExceedsConfiguredThresholdId = "BIG1006";
    private const string AnalyzerExecutionProbeId = "BIG9000";

    [Fact]
    public void Analyzer_can_be_instantiated()
    {
        DiagnosticAnalyzer analyzer = new ComplexityAnalyzer();

        ComplexityAnalyzer typedAnalyzer = Assert.IsType<ComplexityAnalyzer>(analyzer);
        Assert.NotNull(typedAnalyzer);
    }

    [Fact]
    public void SupportedDiagnostics_contains_estimated_complexity_and_the_phase_one_probe()
    {
        var analyzer = new ComplexityAnalyzer();

        Assert.Equal(
            [
                EstimatedAlgorithmicComplexityId,
                LinearLookupInsideIterationId,
                MaterializationInsideIterationId,
                OrderingInsideIterationId,
                InputDependentCallInsideIterationId,
                ExponentialRecursiveGrowthId,
                MethodComplexityExceedsConfiguredThresholdId,
                AnalyzerExecutionProbeId
            ],
            analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id));
    }

    [Fact]
    public void EstimatedAlgorithmicComplexity_has_the_expected_public_descriptor_metadata()
    {
        DiagnosticDescriptor descriptor = new ComplexityAnalyzer()
            .SupportedDiagnostics
            .Single(descriptor => descriptor.Id == EstimatedAlgorithmicComplexityId);

        Assert.Equal(EstimatedAlgorithmicComplexityId, descriptor.Id);
        Assert.Equal("Estimated algorithmic complexity", descriptor.Title.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("Complexity", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.False(descriptor.IsEnabledByDefault);
    }

    [Theory]
    [InlineData(LinearLookupInsideIterationId, "Linear lookup inside iteration")]
    [InlineData(MaterializationInsideIterationId, "Materialization inside iteration")]
    [InlineData(OrderingInsideIterationId, "Ordering inside iteration")]
    [InlineData(InputDependentCallInsideIterationId, "Input-dependent method call inside iteration")]
    [InlineData(ExponentialRecursiveGrowthId, "Exponential recursive growth")]
    [InlineData(MethodComplexityExceedsConfiguredThresholdId, "Method complexity exceeds configured threshold")]
    public void Actionable_diagnostics_have_expected_public_descriptor_metadata(
        string diagnosticId,
        string expectedTitle)
    {
        DiagnosticDescriptor descriptor = new ComplexityAnalyzer()
            .SupportedDiagnostics
            .Single(descriptor => descriptor.Id == diagnosticId);

        Assert.Equal(diagnosticId, descriptor.Id);
        Assert.Equal(expectedTitle, descriptor.Title.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("Complexity", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public void AnalyzerExecutionProbe_has_the_expected_public_descriptor_metadata()
    {
        DiagnosticDescriptor descriptor = new ComplexityAnalyzer()
            .SupportedDiagnostics
            .Single(descriptor => descriptor.Id == AnalyzerExecutionProbeId);

        Assert.Equal(AnalyzerExecutionProbeId, descriptor.Id);
        Assert.Equal(DiagnosticSeverity.Info, descriptor.DefaultSeverity);
        Assert.False(descriptor.IsEnabledByDefault);
    }

    [Fact]
    public async Task Analyzer_does_not_report_estimated_complexity_when_it_is_not_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
    }

    [Fact]
    public async Task Analyzer_registers_local_functions_as_independent_executable_member_roots()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int Ordinary() => 42;

                public int M()
                {
                    int Local(int[] values)
                    {
                        foreach (var value in values)
                        {
                        }

                        return 42;
                    }

                    return 42;
                }
            }
            """,
            enableComplexity: true);

        ImmutableArray<Diagnostic> estimates =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
        ];

        Assert.Equal(3, estimates.Length);
        AssertDiagnosticText(estimates[0], "Ordinary");
        AssertDiagnosticText(estimates[1], "M");
        AssertDiagnosticText(estimates[2], "Local");
        Assert.Equal("Estimated algorithmic complexity for 'Ordinary' is O(1)", estimates[0].GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("Estimated algorithmic complexity for 'M' is O(1)", estimates[1].GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("Estimated algorithmic complexity for 'Local' is O(n)", estimates[2].GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Analyzer_uses_local_function_complexity_when_it_is_invoked_by_parent()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public void M(int[] values)
                {
                    Local(values);

                    void Local(int[] items)
                    {
                        foreach (var item in items)
                        {
                        }
                    }
                }
            }
            """,
            enableComplexity: true);

        ImmutableArray<Diagnostic> estimates =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
        ];

        Assert.Collection(
            estimates,
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "M");
                Assert.Equal("Estimated algorithmic complexity for 'M' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "Local");
                Assert.Equal("Estimated algorithmic complexity for 'Local' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            });
    }

    [Theory]
    [InlineData(
        """
        public sealed class Sample
        {
            public int M() => 42;
        }
        """,
        "Estimated algorithmic complexity for 'M' is O(1)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public void M(int[] values)
            {
                foreach (var value in values)
                {
                    var x = value + 1;
                }
            }
        }
        """,
        "Estimated algorithmic complexity for 'M' is O(n)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public void M(int[] values)
            {
                foreach (var outer in values)
                {
                    foreach (var inner in values)
                    {
                        var x = outer + inner;
                    }
                }
            }
        }
        """,
        "Estimated algorithmic complexity for 'M' is O(n\u00b2)")]
    public async Task Analyzer_reports_estimated_complexity_when_explicitly_enabled(
        string source,
        string expectedMessage)
    {
        _ = expectedMessage ?? throw new ArgumentNullException(nameof(expectedMessage));

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            source,
            enableComplexity: true);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(
            diagnostic,
            DiagnosticPropertyNames.Complexity,
            expectedMessage[(expectedMessage.IndexOf(" is ", StringComparison.Ordinal) + " is ".Length)..]);
        Assert.True(diagnostic.Location.IsInSource);

        SyntaxTree sourceTree = diagnostic.Location.SourceTree
            ?? throw new System.InvalidOperationException("Expected a source location.");
        string diagnosticText = sourceTree
            .GetText()
            .GetSubText(diagnostic.Location.SourceSpan)
            .ToString();
        Assert.Equal("M", diagnosticText);
    }

    [Fact]
    public async Task Analyzer_reports_interprocedural_estimated_complexity_when_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public void M(int[] values)
                {
                    Helper(values);
                }

                private void Helper(int[] items)
                {
                    foreach (var item in items)
                    {
                        var x = item + 1;
                    }
                }
            }
            """,
            enableComplexity: true);

        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == "M");

        Assert.Equal("Estimated algorithmic complexity for 'M' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(diagnostic, DiagnosticPropertyNames.Complexity, "O(n)");
    }

    [Fact]
    public async Task Analyzer_reports_constructor_complexity_without_changing_method_locations()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public Sample(int[] values)
                {
                    foreach (var value in values)
                    {
                    }
                }

                public int M() => 42;
            }
            """,
            enableComplexity: true);

        Diagnostic constructor = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("Sample.ctor", StringComparison.Ordinal));
        Diagnostic method = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).Contains("'M'", StringComparison.Ordinal));

        AssertDiagnosticText(constructor, "Sample");
        Assert.Equal("Estimated algorithmic complexity for 'Sample.ctor' is O(n)", constructor.GetMessage(CultureInfo.InvariantCulture));
        AssertDiagnosticText(method, "M");
        Assert.Equal("Estimated algorithmic complexity for 'M' is O(1)", method.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Analyzer_reports_accessor_event_and_expression_bodied_property_roots()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;

            public sealed class Sample
            {
                public int Count
                {
                    get
                    {
                        return 42;
                    }
                }

                public int[] Items
                {
                    set
                    {
                        foreach (var valueItem in value)
                        {
                        }
                    }
                }

                public int[] InitOnly
                {
                    init
                    {
                        foreach (var valueItem in value)
                        {
                        }
                    }
                }

                public event Action Changed
                {
                    add
                    {
                    }

                    remove
                    {
                    }
                }

                public int Answer => 42;
            }
            """,
            enableComplexity: true);

        ImmutableArray<Diagnostic> estimates =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
        ];

        Assert.Collection(
            estimates,
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "get");
                Assert.Equal("Estimated algorithmic complexity for 'Count.get' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "set");
                Assert.Equal("Estimated algorithmic complexity for 'Items.set' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "init");
                Assert.Equal("Estimated algorithmic complexity for 'InitOnly.set' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "add");
                Assert.Equal("Estimated algorithmic complexity for 'Changed.add' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "remove");
                Assert.Equal("Estimated algorithmic complexity for 'Changed.remove' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "Answer");
                Assert.Equal("Estimated algorithmic complexity for 'Answer.get' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            });
    }

    [Fact]
    public async Task Analyzer_reports_operator_and_conversion_operator_roots()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public static Sample operator !(Sample value) => value;

                public static Sample operator +(Sample left, int[] values)
                {
                    foreach (var value in values)
                    {
                    }

                    return left;
                }

                public static implicit operator Sample(int[] values)
                {
                    foreach (var value in values)
                    {
                    }

                    return null;
                }
            }
            """,
            enableComplexity: true);

        ImmutableArray<Diagnostic> estimates =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
        ];

        Assert.Collection(
            estimates,
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "operator");
                Assert.Equal("Estimated algorithmic complexity for 'operator !' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "operator");
                Assert.Equal("Estimated algorithmic complexity for 'operator +' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "implicit");
                Assert.Equal("Estimated algorithmic complexity for 'implicit operator Sample' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            });
    }

    [Fact]
    public async Task Analyzer_reports_lambdas_and_anonymous_methods_without_parent_body_contamination()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;

            public sealed class Sample
            {
                public void M()
                {
                    Func<int, int> simple = x => x + 1;
                    Action<int[]> parenthesized = (values) =>
                    {
                        foreach (var value in values)
                        {
                        }
                    };
                    Action<int[]> anonymous = delegate(int[] values)
                    {
                        foreach (var value in values)
                        {
                        }
                    };
                }
            }
            """,
            enableComplexity: true);

        ImmutableArray<Diagnostic> estimates =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
        ];

        Assert.Collection(
            estimates,
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "M");
                Assert.Equal("Estimated algorithmic complexity for 'M' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "=>");
                Assert.Equal("Estimated algorithmic complexity for 'lambda' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "=>");
                Assert.Equal("Estimated algorithmic complexity for 'lambda' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            },
            diagnostic =>
            {
                AssertDiagnosticText(diagnostic, "delegate");
                Assert.Equal("Estimated algorithmic complexity for 'anonymous method' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
            });
    }

    [Fact]
    public async Task Analyzer_does_not_double_report_actionable_diagnostics_for_nested_lambda_bodies()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class Sample
            {
                public void M()
                {
                    Action<List<int>, List<int>> action = (items, blocked) =>
                    {
                        foreach (var item in items)
                        {
                            _ = blocked.Contains(item);
                        }
                    };
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == LinearLookupInsideIterationId);

        AssertDiagnosticText(diagnostic, "blocked.Contains(item)");
    }

    [Fact]
    public async Task Analyzer_does_not_treat_captured_variables_as_lambda_input_size_parameters()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System;

            public sealed class Sample
            {
                public void M(int[] values)
                {
                    int count = values.Length;
                    Action action = () =>
                    {
                        for (var i = 0; i < count; i++)
                        {
                        }
                    };
                }
            }
            """,
            enableComplexity: true);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);

        AssertDiagnosticText(diagnostic, "M");
        Assert.Equal("Estimated algorithmic complexity for 'M' is O(1)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Analyzer_reports_chained_interprocedural_estimated_complexity_when_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public void M(int[] values)
                {
                    First(values);
                }

                private void First(int[] items)
                {
                    Second(items);
                }

                private void Second(int[] items)
                {
                    foreach (var item in items)
                    {
                        var x = item + 1;
                    }
                }
            }
            """,
            enableComplexity: true);

        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == "M");

        Assert.Equal("Estimated algorithmic complexity for 'M' is O(n)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(diagnostic, DiagnosticPropertyNames.Complexity, "O(n)");
    }

    [Fact]
    public async Task Analyzer_preserves_independent_inputs_in_interprocedural_estimated_complexity()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public void M(int[] left, int[] right)
                {
                    foreach (var value in left)
                    {
                        Search(right);
                    }
                }

                private void Search(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            enableComplexity: true);

        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == "M");

        Assert.Equal("Estimated algorithmic complexity for 'M' is O(n \u00b7 m)", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(diagnostic, DiagnosticPropertyNames.Complexity, "O(n \u00b7 m)");
    }

    [Theory]
    [InlineData(
        """
        public sealed class Sample
        {
            public int M(int n)
            {
                if (n <= 1)
                {
                    return 1;
                }

                return M(n - 1) + 1;
            }
        }
        """,
        "M",
        "Estimated algorithmic complexity for 'M' is O(n)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public int BinarySearch(int n, bool takeLeft)
            {
                if (n <= 1)
                {
                    return 0;
                }

                if (takeLeft)
                {
                    return BinarySearch(n / 2, false);
                }

                return BinarySearch(n / 2, false);
            }
        }
        """,
        "BinarySearch",
        "Estimated algorithmic complexity for 'BinarySearch' is O(log n)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public void MergeSort(int n)
            {
                if (n <= 1)
                {
                    return;
                }

                MergeSort(n / 2);
                MergeSort(n / 2);

                for (var i = 0; i < n; i++)
                {
                    var value = i + 1;
                }
            }
        }
        """,
        "MergeSort",
        "Estimated algorithmic complexity for 'MergeSort' is O(n log n)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public void FractionalMaster(int n)
            {
                if (n <= 1)
                {
                    return;
                }

                FractionalMaster(n / 2);
                FractionalMaster(n / 2);
                FractionalMaster(n / 2);

                for (var i = 0; i < n; i++)
                {
                    var value = i + 1;
                }
            }
        }
        """,
        "FractionalMaster",
        "Estimated algorithmic complexity for 'FractionalMaster' is O(n^1.585)")]
    [InlineData(
        """
        public sealed class Sample
        {
            public void UnequalSplit(double n)
            {
                if (n <= 1)
                {
                    return;
                }

                UnequalSplit(n / 3);
                UnequalSplit(n * (2.0 / 3.0));

                for (var i = 0; i < n; i++)
                {
                    var value = i + 1;
                }
            }
        }
        """,
        "UnequalSplit",
        "Estimated algorithmic complexity for 'UnequalSplit' is O(n log n)")]
    public async Task Analyzer_reports_recursive_estimated_complexity_when_enabled(
        string source,
        string methodName,
        string expectedMessage)
    {
        _ = expectedMessage ?? throw new ArgumentNullException(nameof(expectedMessage));

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            source,
            enableComplexity: true);

        Diagnostic diagnostic = diagnostics
            .Where(diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId)
            .Single(diagnostic => GetDiagnosticText(diagnostic) == methodName);

        Assert.Equal(expectedMessage, diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(
            diagnostic,
            DiagnosticPropertyNames.Complexity,
            expectedMessage[(expectedMessage.IndexOf(" is ", StringComparison.Ordinal) + " is ".Length)..]);
    }

    [Fact]
    public async Task Analyzer_does_not_report_estimated_complexity_for_interprocedural_cycles()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public void M(int[] values)
                {
                    M(values);
                }
            }
            """,
            enableComplexity: true);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
    }

    [Fact]
    public async Task Analyzer_does_not_report_estimated_complexity_for_unknown_recursion()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M(int n)
                {
                    return M(n - 1);
                }
            }
            """,
            enableComplexity: true);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
    }

    [Fact]
    public async Task Analyzer_does_not_report_estimated_complexity_for_unknown_methods()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public interface CustomCollection
            {
                bool Probe(int value);
            }

            public sealed class Sample
            {
                public bool M(CustomCollection values) => values.Probe(42);
            }
            """,
            enableComplexity: true);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == EstimatedAlgorithmicComplexityId);
    }

    [Fact]
    public async Task Big1001_reports_list_contains_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> customers, List<int> blockedCustomers)
                {
                    foreach (var customer in customers)
                    {
                        if (blockedCustomers.Contains(customer))
                        {
                        }
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == LinearLookupInsideIterationId);

        Assert.Equal(
            "List<T>.Contains performs a linear lookup with known cost O(m) inside an iteration estimated as O(n). Estimated contribution: O(n \u00b7 m).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertNestedOperationProperties(diagnostic, "List<T>.Contains", "O(m)", "O(n)", "O(n \u00b7 m)");
        AssertDiagnosticText(diagnostic, "blockedCustomers.Contains(customer)");
    }

    [Fact]
    public async Task Big1001_does_not_report_list_contains_outside_loop()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> blockedCustomers) => blockedCustomers.Contains(42);
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LinearLookupInsideIterationId);
    }

    [Fact]
    public async Task Big1001_does_not_report_hashset_contains_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> customers, HashSet<int> blockedCustomers)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LinearLookupInsideIterationId);
    }

    [Fact]
    public async Task Big1001_does_not_report_custom_contains_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class CustomCollection
            {
                public bool Contains(int value) => true;
            }

            public sealed class Sample
            {
                void M(List<int> customers, CustomCollection blockedCustomers)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LinearLookupInsideIterationId);
    }

    [Fact]
    public async Task Big1001_reports_combined_complexity_for_two_independent_dimensions()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> left, List<int> right)
                {
                    foreach (var value in left)
                    {
                        _ = right.Contains(value);
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == LinearLookupInsideIterationId);

        Assert.Contains("Estimated contribution: O(n \u00b7 m).", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        AssertNestedOperationProperties(diagnostic, "List<T>.Contains", "O(m)", "O(n)", "O(n \u00b7 m)");
    }

    [Fact]
    public async Task Big1002_reports_to_list_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        var copy = items.ToList();
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == MaterializationInsideIterationId);

        Assert.Equal(
            "Enumerable.ToList materializes the sequence with known cost O(m) inside an iteration estimated as O(n). Estimated contribution: O(n \u00b7 m).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertNestedOperationProperties(diagnostic, "Enumerable.ToList", "O(m)", "O(n)", "O(n \u00b7 m)");
        AssertDiagnosticText(diagnostic, "items.ToList()");
    }

    [Fact]
    public async Task Big1002_does_not_report_to_list_outside_loop()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> items) => items.ToList();
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MaterializationInsideIterationId);
    }

    [Fact]
    public async Task Big1002_does_not_report_custom_to_list_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class CustomCollection
            {
                public CustomCollection ToList() => this;
            }

            public sealed class Sample
            {
                void M(List<int> customers, CustomCollection items)
                {
                    foreach (var customer in customers)
                    {
                        var copy = items.ToList();
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == MaterializationInsideIterationId);
    }

    [Fact]
    public async Task Big1003_reports_orderby_consumed_inside_foreach_body()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        var sorted = items.OrderBy(item => item).ToList();
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == OrderingInsideIterationId);

        Assert.Equal(
            "Enumerable.OrderBy performs ordering with known consumed cost O(m log m) inside an iteration estimated as O(n). Estimated contribution: O(n \u00b7 m log m).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertNestedOperationProperties(diagnostic, "Enumerable.OrderBy", "O(m log m)", "O(n)", "O(n \u00b7 m log m)");
        AssertDiagnosticText(diagnostic, "items.OrderBy(item => item)");
    }

    [Fact]
    public async Task Big1003_does_not_report_deferred_orderby_without_consumption()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        var query = items.OrderBy(item => item);
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == OrderingInsideIterationId);
    }

    [Fact]
    public async Task Big1003_does_not_report_orderby_consumed_outside_loop()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(List<int> customers, IEnumerable<int> items)
                {
                    var query = items.OrderBy(item => item);
                    foreach (var customer in customers)
                    {
                        var current = customer;
                    }

                    return query.ToList();
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == OrderingInsideIterationId);
    }

    [Fact]
    public async Task Big1004_reports_linear_source_call_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int[] customers)
                {
                    foreach (var customer in customers)
                    {
                        Check(customers);
                    }
                }

                private void Check(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);

        Assert.Equal(
            "Method 'Sample.Check' has input-dependent complexity O(n) and is invoked inside an iteration estimated as O(n). Estimated contribution: O(n\u00b2).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertNestedOperationProperties(diagnostic, "Sample.Check", "O(n)", "O(n)", "O(n\u00b2)");
        AssertDiagnosticText(diagnostic, "Check(customers)");
    }

    [Fact]
    public async Task Big1004_reports_independent_source_dimension_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int[] customers, int[] blocked)
                {
                    foreach (var customer in customers)
                    {
                        CheckAgainstBlacklist(customer, blocked);
                    }
                }

                private void CheckAgainstBlacklist(int customer, int[] blocked)
                {
                    foreach (var value in blocked)
                    {
                        var x = value + customer;
                    }
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);

        Assert.Equal(
            "Method 'Sample.CheckAgainstBlacklist' has input-dependent complexity O(m) and is invoked inside an iteration estimated as O(n). Estimated contribution: O(n \u00b7 m).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertNestedOperationProperties(diagnostic, "Sample.CheckAgainstBlacklist", "O(m)", "O(n)", "O(n \u00b7 m)");
        AssertDiagnosticText(diagnostic, "CheckAgainstBlacklist(customer, blocked)");
    }

    [Fact]
    public async Task Big1004_reports_solved_recursive_source_call_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int[] customers, int limit)
                {
                    foreach (var customer in customers)
                    {
                        CountDown(limit);
                    }
                }

                private int CountDown(int n)
                {
                    if (n <= 1)
                    {
                        return 1;
                    }

                    return CountDown(n - 1) + 1;
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);

        Assert.Equal(
            "Method 'Sample.CountDown' has input-dependent complexity O(m) and is invoked inside an iteration estimated as O(n). Estimated contribution: O(n \u00b7 m).",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertNestedOperationProperties(diagnostic, "Sample.CountDown", "O(m)", "O(n)", "O(n \u00b7 m)");
        AssertDiagnosticText(diagnostic, "CountDown(limit)");
    }

    [Fact]
    public async Task Big1004_does_not_report_constant_source_call_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int[] customers)
                {
                    foreach (var customer in customers)
                    {
                        Check(customer);
                    }
                }

                private int Check(int value) => value + 1;
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
    }

    [Fact]
    public async Task Big1004_does_not_report_source_call_outside_loop()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    Check(values);
                }

                private void Check(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
    }

    [Fact]
    public async Task Big1004_does_not_report_unknown_source_call_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int[] customers, int[] values)
                {
                    foreach (var customer in customers)
                    {
                        Check(values);
                    }
                }

                private void Check(int[] values)
                {
                    System.Console.WriteLine(values.Length);
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
    }

    [Fact]
    public async Task Big1004_does_not_report_virtual_unsafe_dispatch_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public class Worker
            {
                public virtual void Check(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }

            public sealed class Sample
            {
                void M(int[] customers, int[] values, Worker worker)
                {
                    foreach (var customer in customers)
                    {
                        worker.Check(values);
                    }
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
    }

    [Fact]
    public async Task Big1004_does_not_report_cycle_boundary_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int[] customers, int[] values)
                {
                    foreach (var customer in customers)
                    {
                        Check(values);
                    }
                }

                private void Check(int[] values)
                {
                    Check(values);
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
    }

    [Fact]
    public async Task Big1004_does_not_duplicate_known_list_contains_inside_foreach()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> customers, List<int> blockedCustomers)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                    }
                }
            }
            """);

        Assert.Equal(1, diagnostics.Count(diagnostic => diagnostic.Id == LinearLookupInsideIterationId));
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == InputDependentCallInsideIterationId);
    }

    [Fact]
    public async Task Actionable_diagnostics_coexist_without_duplicate_reports()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(List<int> customers, List<int> blockedCustomers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                        var sorted = items.OrderBy(item => item).ToList();
                    }
                }
            }
            """);

        Assert.Equal(1, diagnostics.Count(diagnostic => diagnostic.Id == LinearLookupInsideIterationId));
        Assert.Equal(1, diagnostics.Count(diagnostic => diagnostic.Id == MaterializationInsideIterationId));
        Assert.Equal(1, diagnostics.Count(diagnostic => diagnostic.Id == OrderingInsideIterationId));
        Assert.Equal(0, diagnostics.Count(diagnostic => diagnostic.Id == InputDependentCallInsideIterationId));
    }

    [Fact]
    public async Task Big1005_reports_fibonacci_like_supported_recursion()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                int Fibonacci(int n)
                {
                    if (n <= 1)
                    {
                        return n;
                    }

                    return Fibonacci(n - 1) + Fibonacci(n - 2);
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);

        Assert.Equal(
            "Recursive method 'Sample.Fibonacci' exhibits exponential growth with estimated complexity O(1.618^n)",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(diagnostic, DiagnosticPropertyNames.Complexity, "O(1.618^n)");
        AssertProperty(diagnostic, DiagnosticPropertyNames.RecurrenceClass, "exponential");
        AssertDiagnosticText(diagnostic, "Fibonacci");
    }

    [Fact]
    public async Task Big1005_reports_two_decrement_recursive_calls()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void Branch(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    Branch(n - 1);
                    Branch(n - 1);
                }
            }
            """);

        Diagnostic diagnostic = Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);

        Assert.Equal(
            "Recursive method 'Sample.Branch' exhibits exponential growth with estimated complexity O(2^n)",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        AssertProperty(diagnostic, DiagnosticPropertyNames.Complexity, "O(2^n)");
        AssertProperty(diagnostic, DiagnosticPropertyNames.RecurrenceClass, "exponential");
        AssertDiagnosticText(diagnostic, "Branch");
    }

    [Fact]
    public async Task Big1005_does_not_report_non_exponential_recursion()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    M(n - 1);
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);
    }

    [Fact]
    public async Task Big1005_does_not_report_unknown_recursion()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void M(int n)
                {
                    M(n - 1);
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);
    }

    [Fact]
    public async Task Big1005_does_not_report_mutual_recursion()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                void A(int n)
                {
                    if (n <= 1)
                    {
                        return;
                    }

                    B(n - 1);
                }

                void B(int n)
                {
                    A(n - 1);
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ExponentialRecursiveGrowthId);
    }

    [Fact]
    public async Task Analyzer_diagnostics_are_deterministic_for_repeated_source()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                public int Constant() => 42;

                void M(List<int> customers, List<int> blockedCustomers, IEnumerable<int> items)
                {
                    foreach (var customer in customers)
                    {
                        _ = blockedCustomers.Contains(customer);
                        var sorted = items.OrderBy(item => item).ToList();
                    }
                }
            }
            """;
        ImmutableArray<string>? expected = null;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
                source,
                enableProbe: true,
                enableComplexity: true);
            ImmutableArray<string> actual =
            [
                .. diagnostics
                .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .Select(FormatDeterministicDiagnostic)
            ];

            expected ??= actual;

            Assert.Equal(expected, actual);
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(EstimatedAlgorithmicComplexityId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(LinearLookupInsideIterationId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(MaterializationInsideIterationId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(OrderingInsideIterationId + "|", StringComparison.Ordinal));
            Assert.DoesNotContain(actual, diagnostic => diagnostic.StartsWith(InputDependentCallInsideIterationId + "|", StringComparison.Ordinal));
            Assert.Contains(actual, diagnostic => diagnostic.StartsWith(AnalyzerExecutionProbeId + "|", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Analyzer_does_not_report_the_probe_when_it_is_not_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
    }

    [Fact]
    public async Task Analyzer_reports_exactly_one_probe_per_compilation_when_explicitly_enabled()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """,
            enableProbe: true);

        Diagnostic diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        AssertProperty(diagnostic, DiagnosticPropertyNames.DiagnosticRole, "execution-probe");
    }

    [Fact]
    public async Task Analyzer_reports_the_probe_at_a_source_location_when_source_is_available()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int M() => 42;
            }
            """,
            enableProbe: true);

        Diagnostic diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);

        Assert.True(diagnostic.Location.IsInSource);
    }

    [Fact]
    public async Task Analyzer_reports_only_one_probe_for_code_with_multiple_methods()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public int First() => 1;
                public int Second() => 2;
                public int Third() => First() + Second();
            }
            """,
            enableProbe: true);

        _ = Assert.Single(diagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
    }

    [Fact]
    public async Task Generated_code_does_not_change_probe_emission_behavior()
    {
        ImmutableArray<SyntaxTree> syntaxTrees =
        [
            Parse(
                """
                public sealed class UserCode
                {
                    public int M() => 42;
                }
                """),
            Parse(
                """
                // <auto-generated/>
                public sealed class GeneratedCode
                {
                    public int First() => 1;
                    public int Second() => 2;
                }
                """,
                "Generated.g.cs")
        ];

        ImmutableArray<Diagnostic> disabledDiagnostics = await GetAnalyzerDiagnosticsAsync(syntaxTrees);
        ImmutableArray<Diagnostic> enabledDiagnostics = await GetAnalyzerDiagnosticsAsync(syntaxTrees, enableProbe: true);

        Assert.DoesNotContain(disabledDiagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
        _ = Assert.Single(enabledDiagnostics, diagnostic => diagnostic.Id == AnalyzerExecutionProbeId);
    }

    [Fact]
    public async Task Analyzer_runs_on_a_valid_csharp_compilation_without_throwing()
    {
        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(
            """
            public sealed class Sample
            {
                public string M() => nameof(Sample);
            }
            """);

        Assert.Empty(diagnostics);
    }

    private static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        bool enableProbe = false,
        bool enableComplexity = false)
    {
        return GetAnalyzerDiagnosticsAsync([Parse(source)], enableProbe, enableComplexity);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        ImmutableArray<SyntaxTree> syntaxTrees,
        bool enableProbe = false,
        bool enableComplexity = false)
    {
        CSharpCompilation compilation = CreateCompilation(syntaxTrees, enableProbe, enableComplexity);
        var analyzer = new ComplexityAnalyzer();
        ImmutableArray<DiagnosticAnalyzer> analyzers = [analyzer];
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(
        ImmutableArray<SyntaxTree> syntaxTrees,
        bool enableProbe,
        bool enableComplexity)
    {
        ImmutableDictionary<string, ReportDiagnostic>.Builder specificDiagnosticOptions =
            ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();

        if (enableProbe)
        {
            specificDiagnosticOptions.Add(AnalyzerExecutionProbeId, ReportDiagnostic.Info);
        }

        if (enableComplexity)
        {
            specificDiagnosticOptions.Add(EstimatedAlgorithmicComplexityId, ReportDiagnostic.Info);
        }

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            specificDiagnosticOptions: specificDiagnosticOptions.ToImmutable());

        return CSharpCompilation.Create(
            assemblyName: "AnalyzerInfrastructureTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: compilationOptions);
    }

    private static SyntaxTree Parse(string source, string path = "Sample.cs")
    {
        return CSharpSyntaxTree.ParseText(source, path: path);
    }

    private static void AssertDiagnosticText(Diagnostic diagnostic, string expectedText)
    {
        Assert.Equal(expectedText, GetDiagnosticText(diagnostic));
    }

    private static void AssertNestedOperationProperties(
        Diagnostic diagnostic,
        string operation,
        string operationComplexity,
        string iterationComplexity,
        string combinedComplexity)
    {
        AssertProperty(diagnostic, DiagnosticPropertyNames.Operation, operation);
        AssertProperty(diagnostic, DiagnosticPropertyNames.OperationComplexity, operationComplexity);
        AssertProperty(diagnostic, DiagnosticPropertyNames.IterationComplexity, iterationComplexity);
        AssertProperty(diagnostic, DiagnosticPropertyNames.CombinedComplexity, combinedComplexity);
    }

    private static void AssertProperty(
        Diagnostic diagnostic,
        string key,
        string expectedValue)
    {
        Assert.True(diagnostic.Properties.TryGetValue(key, out string? actualValue));
        Assert.Equal(expectedValue, actualValue);
    }

    private static string GetDiagnosticText(Diagnostic diagnostic)
    {
        SyntaxTree sourceTree = diagnostic.Location.SourceTree
            ?? throw new System.InvalidOperationException("Expected a source location.");
        return sourceTree
            .GetText()
            .GetSubText(diagnostic.Location.SourceSpan)
            .ToString();
    }

    private static string FormatDeterministicDiagnostic(Diagnostic diagnostic)
    {
        SyntaxTree sourceTree = diagnostic.Location.SourceTree
            ?? throw new System.InvalidOperationException("Expected a source location.");
        string diagnosticText = sourceTree
            .GetText()
            .GetSubText(diagnostic.Location.SourceSpan)
            .ToString();

        return string.Join(
            "|",
            diagnostic.Id,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            diagnostic.Location.SourceSpan.Start.ToString(CultureInfo.InvariantCulture),
            diagnostic.Location.SourceSpan.Length.ToString(CultureInfo.InvariantCulture),
            diagnosticText);
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } = CreateTrustedPlatformReferences();

    private static ImmutableArray<MetadataReference> CreateTrustedPlatformReferences()
    {
        string trustedPlatformAssemblies =
            (string?)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? string.Empty;

        return
        [
            .. trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }
}

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.Interprocedural;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class MethodComplexityExtractorTests
{
    [Fact]
    public void Phase_three_characterization_matrix_covers_core_extraction_contracts()
    {
        (string Scenario, string Source, string Expected)[] cases =
        [
            (
                "constant",
                """
                public sealed class Sample
                {
                    int M() => 1;
                }
                """,
                "O(1)"),
            (
                "foreach-input",
                """
                public sealed class Sample
                {
                    void M(int[] input)
                    {
                        foreach (var item in input)
                        {
                            var x = item + 1;
                        }
                    }
                }
                """,
                "O(n)"),
            (
                "nested-same-input",
                """
                public sealed class Sample
                {
                    void M(int[] input)
                    {
                        foreach (var outer in input)
                        {
                            foreach (var inner in input)
                            {
                                var x = outer + inner;
                            }
                        }
                    }
                }
                """,
                "O(n\u00b2)"),
            (
                "nested-independent-inputs",
                """
                public sealed class Sample
                {
                    void M(int[] left, int[] right)
                    {
                        foreach (var l in left)
                        {
                            foreach (var r in right)
                            {
                                var x = l + r;
                            }
                        }
                    }
                }
                """,
                "O(n \u00b7 m)"),
            (
                "logarithmic-for",
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 1; i < count; i *= 2)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
                "O(log n)"),
            (
                "linear-with-nested-logarithmic",
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 0; i < count; i++)
                        {
                            for (var j = 1; j < count; j *= 2)
                            {
                                var x = i + j;
                            }
                        }
                    }
                }
                """,
                "O(n log n)"),
            (
                "branch-worst-case",
                """
                public sealed class Sample
                {
                    void M(bool enabled, int[] input)
                    {
                        if (enabled)
                        {
                            foreach (var item in input)
                            {
                                var x = item + 1;
                            }
                        }
                        else
                        {
                            foreach (var outer in input)
                            {
                                foreach (var inner in input)
                                {
                                    var x = outer + inner;
                                }
                            }
                        }
                    }
                }
                """,
                "O(n\u00b2)"),
            (
                "source-invocation",
                """
                public sealed class Sample
                {
                    void M()
                    {
                        Visit();
                    }

                    void Visit()
                    {
                    }
                }
                """,
                "O(1)"),
            (
                "unknown-while-bound",
                """
                public sealed class Sample
                {
                    void M(bool condition)
                    {
                        while (condition)
                        {
                            var x = 1;
                        }
                    }
                }
                """,
                "Unknown"),
            (
                "custom-property",
                """
                public sealed class Holder
                {
                    public int Count => 1;
                }

                public sealed class Sample
                {
                    int M(Holder holder) => holder.Count;
                }
                """,
                "Unknown"),
            (
                "custom-indexer",
                """
                public sealed class Indexed
                {
                    public int this[int index] => index;
                }

                public sealed class Sample
                {
                    int M(Indexed indexed) => indexed[0];
                }
                """,
                "Unknown")
        ];

        foreach ((string scenario, string source, string expected) in cases)
        {
            AssertMethodComplexity(source, expected, scenario);
        }
    }

    [Fact]
    public void Empty_block_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Expression_bodied_literal_method_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M() => 42;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Expression_bodied_arithmetic_method_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int a, int b) => a + b;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Return_literal_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M()
                {
                    return 42;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Primitive_comparison_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                bool M(int a, int b) => a < b;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Local_declaration_and_assignment_are_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M()
                {
                    int value = 1;
                    value = 2;
                    return value;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Uninvoked_local_function_mutation_does_not_invalidate_parent_loop_bound()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    int limit = values.Length;

                    void Reset()
                    {
                        limit = 0;
                    }

                    for (var i = 0; i < limit; i++)
                    {
                        var x = i;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Increment_and_decrement_are_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M()
                {
                    int value = 1;
                    value++;
                    --value;
                    return value;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Sequential_constant_operations_remain_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int a, int b)
                {
                    int value = a + b;
                    value = value * 2;
                    value++;
                    return value;
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Array_length_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int[] values) => values.Length;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void String_length_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(string text) => text.Length;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Array_element_access_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int[] values) => values[0];
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Custom_property_is_not_assumed_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Holder
            {
                public int Length => 1;
            }

            public sealed class Sample
            {
                int M(Holder holder) => holder.Length;
            }
            """,
            "Unknown");
    }

    [Theory]
    [InlineData(
        "list-contains",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            bool M(List<int> values) => values.Contains(42);
        }
        """,
        "O(n)")]
    [InlineData(
        "hashset-contains",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            bool M(HashSet<int> values) => values.Contains(42);
        }
        """,
        "O(1)")]
    [InlineData(
        "dictionary-contains-value",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            bool M(Dictionary<int, string> values) => values.ContainsValue("needle");
        }
        """,
        "O(n)")]
    [InlineData(
        "linq-any",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            bool M(IEnumerable<int> values) => values.Any(value => value > 0);
        }
        """,
        "O(n)")]
    [InlineData(
        "linq-to-list",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            List<int> M(IEnumerable<int> values) => values.ToList();
        }
        """,
        "O(n)")]
    [InlineData(
        "linq-where-to-list",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            List<int> M(IEnumerable<int> values) => values.Where(value => value > 0).ToList();
        }
        """,
        "O(n)")]
    [InlineData(
        "linq-orderby-to-list",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            List<int> M(IEnumerable<int> values) => values.OrderBy(value => value).ToList();
        }
        """,
        "O(n log n)")]
    [InlineData(
        "foreach-where",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            void M(IEnumerable<int> values)
            {
                foreach (var value in values.Where(value => value > 0))
                {
                    var x = value + 1;
                }
            }
        }
        """,
        "O(n)")]
    [InlineData(
        "unknown-custom-method",
        """
        public sealed class CustomCollection
        {
            public bool Probe(int value) => true;
        }

        public sealed class Sample
        {
            bool M(CustomCollection values) => values.Probe(42);
        }
        """,
        "O(1)")]
    public void Known_invocations_integrate_with_method_extraction(
        string scenario,
        string source,
        string expected)
    {
        AssertMethodComplexity(source, expected, scenario);
    }

    [Fact]
    public void Known_invocation_substitutes_receiver_dimension_instead_of_first_parameter()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> left, List<int> right) => right.Contains(42);
            }
            """,
            "O(m)");
    }

    [Fact]
    public void Known_invocation_inside_loop_composes_with_loop_dimension()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(List<int> outer, List<int> inner)
                {
                    foreach (var value in outer)
                    {
                        inner.Contains(value);
                    }
                }
            }
            """,
            "O(n \u00b7 m)");
    }

    [Fact]
    public void Custom_collection_contains_resolves_as_safe_source_call()
    {
        AssertMethodComplexity(
            """
            public sealed class CustomCollection
            {
                public bool Contains(int value) => true;
            }

            public sealed class Sample
            {
                bool M(CustomCollection values) => values.Contains(42);
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Direct_source_call_propagates_callee_complexity()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    Helper(items);
                }

                private void Helper(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Two_level_source_chain_propagates_callee_complexity()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    First(items);
                }

                private void First(int[] values)
                {
                    Second(values);
                }

                private void Second(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Three_level_source_chain_propagates_callee_complexity()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    A(items);
                }

                private void A(int[] values)
                {
                    B(values);
                }

                private void B(int[] values)
                {
                    C(values);
                }

                private void C(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Source_callee_in_different_syntax_tree_uses_callee_semantic_model()
    {
        AssertMethodComplexity(
            [
                """
                public sealed class Sample
                {
                    void M(int[] items)
                    {
                        Helpers.Helper(items);
                    }
                }
                """,
                """
                public static class Helpers
                {
                    public static void Helper(int[] values)
                    {
                        foreach (var value in values)
                        {
                            var x = value + 1;
                        }
                    }
                }
                """
            ],
            "O(n)");
    }

    [Fact]
    public void Same_source_callee_twice_uses_one_cached_template()
    {
        AssertMethodComplexityAndCacheCount(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    Helper(items);
                    Helper(items);
                }

                private void Helper(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)",
            expectedCacheCount: 1);
    }

    [Fact]
    public void Same_source_callee_with_different_inputs_substitutes_per_call_site()
    {
        AssertMethodComplexityAndCacheCount(
            """
            public sealed class Sample
            {
                void M(int[] left, int[] right)
                {
                    Helper(left);
                    Helper(right);
                }

                private void Helper(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n + m)",
            expectedCacheCount: 1);
    }

    [Fact]
    public void Source_call_inside_loop_composes_multiplicatively()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    foreach (var item in items)
                    {
                        Check(items);
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
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Source_call_inside_loop_preserves_independent_input_dimension()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] left, int[] right)
                {
                    foreach (var x in left)
                    {
                        Search(right);
                    }
                }

                private void Search(int[] values)
                {
                    foreach (var value in values)
                    {
                        var y = value + 1;
                    }
                }
            }
            """,
            "O(n \u00b7 m)");
    }

    [Fact]
    public void Source_call_includes_known_argument_evaluation_cost()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(IEnumerable<int> values)
                {
                    Helper(values.ToList());
                }

                private void Helper(List<int> values)
                {
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Source_call_with_unknown_argument_evaluation_remains_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                    Helper(System.Console.ReadLine());
                }

                private void Helper(string value)
                {
                }
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Source_call_preserves_positional_slots_for_non_dimension_parameters()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    Helper(true, items);
                }

                private void Helper(bool enabled, int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Explicit_this_receiver_on_source_call_is_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    this.Scan(items);
                }

                private void Scan(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Explicit_base_receiver_on_source_call_is_constant()
    {
        AssertMethodComplexity(
            """
            public class BaseSample
            {
                protected void Scan(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }

            public sealed class Sample : BaseSample
            {
                void M(int[] items)
                {
                    base.Scan(items);
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Source_extension_receiver_substitutes_into_callee_template()
    {
        AssertMethodComplexity(
            """
            public static class Extensions
            {
                public static void Scan(this int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }

            public sealed class Sample
            {
                void M(int[] items)
                {
                    items.Scan();
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Known_bcl_mapping_keeps_precedence_over_source_analysis_cache()
    {
        AssertMethodComplexityAndCacheCount(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> values) => values.Contains(42);
            }
            """,
            "O(n)",
            expectedCacheCount: 0);
    }

    [Fact]
    public void External_unresolved_call_remains_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                    System.Console.WriteLine("value");
                }
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Unsafe_virtual_source_target_remains_unknown()
    {
        AssertMethodComplexity(
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
                void M(Worker worker, int[] items)
                {
                    worker.Check(items);
                }
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Unsafe_interface_source_target_remains_unknown()
    {
        AssertMethodComplexity(
            """
            public interface IWorker
            {
                void Check(int[] values);
            }

            public sealed class Worker : IWorker
            {
                public void Check(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }

            public sealed class Sample
            {
                void M(IWorker worker, int[] items)
                {
                    worker.Check(items);
                }
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Constant_argument_reduces_source_callee_complexity_to_constant()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                    Helper(10);
                }

                private void Helper(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Direct_recursion_stops_at_cycle_boundary()
    {
        AssertMethodComplexityAndCacheCount(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    M(items);
                }
            }
            """,
            "Unknown",
            expectedCacheCount: 0);
    }

    [Fact]
    public void Two_method_cycle_stops_at_active_path_boundary()
    {
        AssertMethodComplexityAndCacheCount(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    A(items);
                }

                private void A(int[] values)
                {
                    B(values);
                }

                private void B(int[] values)
                {
                    A(values);
                }
            }
            """,
            "Unknown",
            expectedCacheCount: 0);
    }

    [Fact]
    public void Three_method_cycle_stops_at_active_path_boundary()
    {
        AssertMethodComplexityAndCacheCount(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    A(items);
                }

                private void A(int[] values)
                {
                    B(values);
                }

                private void B(int[] values)
                {
                    C(values);
                }

                private void C(int[] values)
                {
                    A(values);
                }
            }
            """,
            "Unknown",
            expectedCacheCount: 0);
    }

    [Fact]
    public void Depth_limit_counts_root_as_depth_zero_and_allows_fifth_callee()
    {
        AssertMethodComplexityAndCacheCount(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    A1(items);
                }

                private void A1(int[] values) => A2(values);
                private void A2(int[] values) => A3(values);
                private void A3(int[] values) => A4(values);
                private void A4(int[] values) => A5(values);

                private void A5(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)",
            expectedCacheCount: 5);
    }

    [Fact]
    public void Depth_limit_plus_one_stops_at_boundary_without_caching_contextual_unknown()
    {
        AssertMethodComplexityAndCacheCount(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    A1(items);
                }

                private void A1(int[] values) => A2(values);
                private void A2(int[] values) => A3(values);
                private void A3(int[] values) => A4(values);
                private void A4(int[] values) => A5(values);
                private void A5(int[] values) => A6(values);

                private void A6(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "Unknown",
            expectedCacheCount: 0);
    }

    [Fact]
    public void Method_budget_stops_expansion_for_current_root_only()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M1(int[] items)
                {
                    BudgetEater(items);
                    Target(items);
                }

                void M2(int[] items)
                {
                    Target(items);
                }

                private void BudgetEater(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }

                private void Target(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            new AnalysisBudget(maximumCallDepth: 5, maximumMethodsPerRootAnalysis: 1),
            CancellationToken.None);

        ComplexityExpression first = AnalyzeMethod(facts, "M1", context, CancellationToken.None);
        ComplexityExpression second = AnalyzeMethod(facts, "M2", context, CancellationToken.None);

        Assert.Equal("Unknown", first.ToBigONotation());
        Assert.Equal("O(n)", second.ToBigONotation());
        Assert.Equal(2, context.TemplateCache.Count);
    }

    [Fact]
    public void Budget_boundary_does_not_poison_cache_for_later_root()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M1(int[] items)
                {
                    BudgetEater(items);
                    Target(items);
                }

                void M2(int[] items)
                {
                    Target(items);
                }

                private void BudgetEater(int[] values)
                {
                }

                private void Target(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            new AnalysisBudget(maximumCallDepth: 5, maximumMethodsPerRootAnalysis: 1),
            CancellationToken.None);

        Assert.Equal("Unknown", AnalyzeMethod(facts, "M1", context, CancellationToken.None).ToBigONotation());
        Assert.Equal("O(n)", AnalyzeMethod(facts, "M2", context, CancellationToken.None).ToBigONotation());
    }

    [Fact]
    public void Abandoned_cancelled_cache_reservation_does_not_poison_later_analysis()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    Helper(items);
                }

                private void Helper(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);
        IMethodSymbol helper = GetMethodSymbol(facts, "Helper");
        using var cancellationTokenSource = new CancellationTokenSource();

        Assert.True(context.TemplateCache.TryReserveAnalysis(
            helper,
            CancellationToken.None,
            out InterproceduralAnalysisResult? completed));
        Assert.Null(completed);

        cancellationTokenSource.Cancel();
        _ = Assert.Throws<OperationCanceledException>(() =>
            context.TemplateCache.StoreCompleted(
                helper,
                InterproceduralAnalysisResult.Unknown("Cancelled analysis."),
                cancellationTokenSource.Token));
        Assert.True(context.TemplateCache.AbandonAnalysis(helper, CancellationToken.None));

        ComplexityExpression result = AnalyzeMethod(facts, "M", context, CancellationToken.None);

        Assert.Equal("O(n)", result.ToBigONotation());
        Assert.True(context.TemplateCache.TryGetCompleted(helper, CancellationToken.None, out InterproceduralAnalysisResult cached));
        Assert.Equal(InterproceduralAnalysisResultKind.Known, cached.Kind);
    }

    [Fact]
    public async Task Concurrent_roots_analyzing_same_callee_are_deterministic()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M1(int[] items)
                {
                    Shared(items);
                }

                void M2(int[] items)
                {
                    Shared(items);
                }

                private void Shared(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        string[] results = await RunConcurrentAnalysesWithTimeout(
            () => AnalyzeMethod(facts, "M1", context, CancellationToken.None).ToBigONotation(),
            () => AnalyzeMethod(facts, "M2", context, CancellationToken.None).ToBigONotation());

        Assert.Equal(["O(n)", "O(n)"], results);
        Assert.Equal(1, context.TemplateCache.Count);
    }

    [Fact]
    public async Task Concurrent_roots_analyzing_different_callees_are_deterministic()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M1(int[] items)
                {
                    Left(items);
                }

                void M2(int[] items)
                {
                    Right(items);
                }

                private void Left(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }

                private void Right(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        string[] results = await RunConcurrentAnalysesWithTimeout(
            () => AnalyzeMethod(facts, "M1", context, CancellationToken.None).ToBigONotation(),
            () => AnalyzeMethod(facts, "M2", context, CancellationToken.None).ToBigONotation());

        Assert.Equal(["O(n)", "O(n)"], results);
        Assert.Equal(2, context.TemplateCache.Count);
    }

    [Fact]
    public async Task Concurrent_roots_analyzing_overlapping_graph_are_deterministic()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M1(int[] items)
                {
                    Left(items);
                }

                void M2(int[] items)
                {
                    Right(items);
                }

                private void Left(int[] values)
                {
                    Shared(values);
                }

                private void Right(int[] values)
                {
                    Shared(values);
                }

                private void Shared(int[] values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        string[] results = await RunConcurrentAnalysesWithTimeout(
            () => AnalyzeMethod(facts, "M1", context, CancellationToken.None).ToBigONotation(),
            () => AnalyzeMethod(facts, "M2", context, CancellationToken.None).ToBigONotation());

        Assert.Equal(["O(n)", "O(n)"], results);
        Assert.Equal(3, context.TemplateCache.Count);
    }

    [Fact]
    public async Task Concurrent_mutual_cycle_does_not_deadlock_on_cache_entries()
    {
        CompilationFacts facts = CreateCompilationFacts(
            """
            public sealed class Sample
            {
                void M1(int[] items)
                {
                    A(items);
                }

                void M2(int[] items)
                {
                    B(items);
                }

                private void A(int[] values)
                {
                    B(values);
                }

                private void B(int[] values)
                {
                    A(values);
                }
            }
            """);
        InterproceduralAnalysisContext context = InterproceduralAnalysisContext.Create(
            facts.Compilation,
            CancellationToken.None);

        string[] results = await RunConcurrentAnalysesWithTimeout(
            () => AnalyzeMethod(facts, "M1", context, CancellationToken.None).ToBigONotation(),
            () => AnalyzeMethod(facts, "M2", context, CancellationToken.None).ToBigONotation());

        Assert.Equal(["Unknown", "Unknown"], results);
        Assert.Equal(0, context.TemplateCache.Count);
    }

    [Fact]
    public void Linq_count_on_list_receiver_uses_known_constant_count()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(List<int> values) => values.Count();
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Linq_count_on_generic_enumerable_receiver_enumerates()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(IEnumerable<int> values) => values.Count();
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Linq_count_on_icollection_receiver_uses_known_constant_count()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(ICollection<int> values) => values.Count();
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Linq_where_without_consumption_only_creates_pipeline()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(IEnumerable<int> values)
                {
                    var query = values.Where(value => value > 0);
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Linq_where_consumed_by_to_list_enumerates_once()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> values) => values.Where(value => value > 0).ToList();
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Linq_select_consumed_by_to_array_enumerates_once()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int[] M(IEnumerable<int> values) => values.Select(value => value + 1).ToArray();
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Linq_orderby_without_consumption_only_creates_pipeline()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(IEnumerable<int> values)
                {
                    var query = values.OrderBy(value => value);
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Linq_orderby_consumed_by_to_list_counts_sorting()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> values) => values.OrderBy(value => value).ToList();
            }
            """,
            "O(n log n)");
    }

    [Fact]
    public void Linq_any_without_predicate_is_constant()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                bool M(IEnumerable<int> values) => values.Any();
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Linq_any_with_predicate_is_linear_worst_case()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                bool M(IEnumerable<int> values) => values.Any(value => value > 0);
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Linq_contains_on_enumerable_is_linear()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                bool M(IEnumerable<int> values) => values.Contains(1);
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Linq_pipeline_in_foreach_is_counted_when_enumerated()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(IEnumerable<int> values)
                {
                    foreach (var value in values.Where(value => value > 0))
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Linq_ordering_pipeline_in_foreach_counts_sorting_when_enumerated()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                void M(IEnumerable<int> values)
                {
                    foreach (var value in values.OrderBy(value => value))
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n log n)");
    }

    [Fact]
    public void Custom_extension_named_where_resolves_as_safe_source_call()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            namespace MyCompany
            {
                public static class QueryExtensions
                {
                    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, System.Func<T, bool> predicate) => source;
                }

                public sealed class Sample
                {
                    IEnumerable<int> M(IEnumerable<int> values) => values.Where(value => value > 0);
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Custom_extension_named_any_resolves_as_safe_source_call()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            namespace MyCompany
            {
                public static class QueryExtensions
                {
                    public static bool Any<T>(this IEnumerable<T> source) => true;
                }

                public sealed class Sample
                {
                    bool M(IEnumerable<int> values) => values.Any();
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Chained_linq_pipeline_consumed_once_remains_linear()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int[] M(IEnumerable<int> values) => values
                    .Where(value => value > 0)
                    .Select(value => value + 1)
                    .ToArray();
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Chained_ordering_pipeline_consumed_once_keeps_sorting_complexity()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> values) => values
                    .OrderBy(value => value)
                    .ThenBy(value => value)
                    .Where(value => value > 0)
                    .ToList();
            }
            """,
            "O(n log n)");
    }

    [Fact]
    public void Select_many_with_known_inner_receiver_preserves_nested_size()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> outer, int[] inner) => outer.SelectMany(_ => inner).ToList();
            }
            """,
            "O(n \u00b7 m)");
    }

    [Fact]
    public void Consumed_linq_pipeline_with_source_predicate_is_linear()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                List<int> M(IEnumerable<int> values) => values.Where(value => IsPositive(value)).ToList();

                bool IsPositive(int value) => value > 0;
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Custom_indexer_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Indexed
            {
                public int this[int index] => index;
            }

            public sealed class Sample
            {
                int M(Indexed indexed) => indexed[0];
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Object_creation_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                object M() => new object();
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Array_creation_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int[] M(int length) => new int[length];
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Foreach_over_array_input_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    foreach (var item in items)
                    {
                        var x = item + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Foreach_over_string_input_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(string text)
                {
                    foreach (var ch in text)
                    {
                        var x = ch;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Foreach_over_collection_input_is_linear()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M(IReadOnlyCollection<int> values)
                {
                    foreach (var value in values)
                    {
                        var x = value + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Foreach_over_unknown_origin_is_unknown()
    {
        AssertMethodComplexity(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                void M()
                {
                    foreach (var value in GetValues())
                    {
                        var x = value + 1;
                    }
                }

                IEnumerable<int> GetValues() => null;
            }
            """,
            "Unknown");
    }

    [Fact]
    public void For_from_zero_to_integral_bound_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_from_zero_to_length_bound_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] values)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        var x = values[i];
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_from_bound_down_to_zero_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = count; i > 0; i--)
                    {
                        var x = i - 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_with_constant_additive_step_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 0; i <= count; i += 2)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void For_with_multiplicative_step_by_two_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 1; i < count; i *= 2)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void For_with_multiplicative_step_by_three_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 1; i < count; i *= 3)
                    {
                        var x = i + 1;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void For_with_divisive_step_by_two_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = count; i > 1; i /= 2)
                    {
                        var x = i - 1;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void Constant_bound_for_is_constant_with_constant_body()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                    var x = 0;
                    for (var i = 0; i < 10; i++)
                    {
                        x++;
                    }
                }
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Nested_loops_over_same_input_are_quadratic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] items)
                {
                    foreach (var outer in items)
                    {
                        foreach (var inner in items)
                        {
                            var x = outer + inner;
                        }
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Nested_loops_over_independent_inputs_preserve_product()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int[] left, int[] right)
                {
                    foreach (var l in left)
                    {
                        foreach (var r in right)
                        {
                            var x = l + r;
                        }
                    }
                }
            }
            """,
            "O(n \u00b7 m)");
    }

    [Fact]
    public void Repeated_extraction_preserves_expression_notation_and_canonical_variables()
    {
        MethodFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void M(int[] left, int[] right)
                {
                    foreach (var l in left)
                    {
                        foreach (var r in right)
                        {
                            var x = l + r;
                        }
                    }
                }
            }
            """);
        ComplexityExpression? expectedExpression = null;
        string? expectedNotation = null;
        string[]? expectedVariables = null;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            MethodAnalysisContext context = MethodAnalysisContext.Create(
                facts.MethodDeclaration,
                facts.SemanticModel,
                CancellationToken.None);
            ComplexityExpression expression = MethodComplexityExtractor.AnalyzeMethod(
                facts.MethodDeclaration,
                facts.SemanticModel,
                CancellationToken.None);
            string[] variables =
            [
                .. context.MethodSymbol.Parameters
                .Select(parameter => context.TryGetInputSizeVariable(parameter, out ComplexityVariable variable)
                    ? parameter.Name + ":" + variable.Name
                    : parameter.Name + ":<none>")
            ];

            expectedExpression ??= expression;
            expectedNotation ??= expression.ToBigONotation();
            expectedVariables ??= variables;

            Assert.Equal(expectedExpression, expression);
            Assert.Equal(expectedNotation, expression.ToBigONotation());
            Assert.Equal(expectedVariables, variables);
        }

        string finalNotation = expectedNotation ?? throw new InvalidOperationException("Expected a baseline notation.");
        string[] finalVariables = expectedVariables ?? throw new InvalidOperationException("Expected baseline variables.");

        Assert.Equal("O(n \u00b7 m)", finalNotation);
        Assert.Equal(["left:n", "right:m"], finalVariables);
    }

    [Fact]
    public void While_with_increment_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 0;
                    while (i < count)
                    {
                        i++;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void While_with_constant_add_assignment_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 0;
                    while (i < count)
                    {
                        i += 2;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void While_with_multiplicative_step_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 1;
                    while (i < count)
                    {
                        i *= 2;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void While_with_divisive_step_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = count;
                    while (i > 1)
                    {
                        i /= 2;
                    }
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void While_without_provable_bound_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool condition)
                {
                    while (condition)
                    {
                        var x = 1;
                    }
                }
            }
            """,
            "Unknown");
    }

    [Theory]
    [InlineData("factor-one")]
    [InlineData("variable-factor")]
    [InlineData("condition-without-control-variable")]
    [InlineData("multiple-control-mutations")]
    public void Unsupported_while_patterns_are_unknown(string scenario)
    {
        string source = scenario switch
        {
            "factor-one" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 1;
                        while (i < count)
                        {
                            i *= 1;
                        }
                    }
                }
                """,
            "variable-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count, int factor)
                    {
                        var i = 1;
                        while (i < count)
                        {
                            i *= factor;
                        }
                    }
                }
                """,
            "condition-without-control-variable" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 1;
                        while (count > 1)
                        {
                            i *= 2;
                        }
                    }
                }
                """,
            "multiple-control-mutations" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 0;
                        while (i < count)
                        {
                            i++;
                            i *= 2;
                        }
                    }
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        AssertMethodComplexity(source, "Unknown");
    }

    [Fact]
    public void Do_while_with_increment_is_linear()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 0;
                    do
                    {
                        i++;
                    }
                    while (i < count);
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Do_while_with_multiplicative_step_is_logarithmic()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 1;
                    do
                    {
                        i *= 2;
                    }
                    while (i < count);
                }
            }
            """,
            "O(log n)");
    }

    [Fact]
    public void Do_while_without_provable_bound_is_unknown()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool condition)
                {
                    do
                    {
                        var x = 1;
                    }
                    while (condition);
                }
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Nested_linear_outer_and_logarithmic_inner_compose_to_n_log_n()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        for (var j = 1; j < count; j *= 2)
                        {
                            var x = i + j;
                        }
                    }
                }
            }
            """,
            "O(n log n)");
    }

    [Fact]
    public void Nested_logarithmic_loops_compose_to_squared_log()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    for (var i = 1; i < count; i *= 2)
                    {
                        for (var j = 1; j < count; j *= 2)
                        {
                            var x = i + j;
                        }
                    }
                }
            }
            """,
            "O(log^2 n)");
    }

    [Fact]
    public void Simple_if_without_else_uses_true_branch_as_worst_case()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void If_else_uses_worst_case_branch()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        var x = 1;
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Else_if_chain_uses_worst_case_alternative()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool fast, bool slow, int[] items)
                {
                    if (fast)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                    else if (slow)
                    {
                        foreach (var outer in items)
                        {
                            foreach (var inner in items)
                            {
                                var x = outer + inner;
                            }
                        }
                    }
                    else
                    {
                        var x = 1;
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Branching_selects_quadratic_over_linear_branch()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                    else
                    {
                        foreach (var outer in items)
                        {
                            foreach (var inner in items)
                            {
                                var x = outer + inner;
                            }
                        }
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Branching_selects_n_log_n_over_linear_branch()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            for (var i = 1; i < items.Length; i *= 2)
                            {
                                var x = item + i;
                            }
                        }
                    }
                }
            }
            """,
            "O(n log n)");
    }

    [Fact]
    public void Switch_uses_worst_case_across_cases()
    {
        AssertMethodComplexity(
            """
            public enum Mode
            {
                Fast,
                Slow
            }

            public sealed class Sample
            {
                void M(Mode mode, int[] items)
                {
                    switch (mode)
                    {
                        case Mode.Fast:
                            var y = 1;
                            break;
                        case Mode.Slow:
                            foreach (var item in items)
                            {
                                var x = item + 1;
                            }
                            break;
                    }
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Switch_default_participates_in_worst_case()
    {
        AssertMethodComplexity(
            """
            public enum Mode
            {
                Fast,
                Slow
            }

            public sealed class Sample
            {
                void M(Mode mode, int[] items)
                {
                    switch (mode)
                    {
                        case Mode.Fast:
                            foreach (var item in items)
                            {
                                var x = item + 1;
                            }
                            break;
                        default:
                            foreach (var outer in items)
                            {
                                foreach (var inner in items)
                                {
                                    var x = outer + inner;
                                }
                            }
                            break;
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Nested_branching_uses_worst_case_inner_branch()
    {
        AssertMethodComplexity(
            """
            public enum Mode
            {
                Fast,
                Slow
            }

            public sealed class Sample
            {
                void M(bool enabled, Mode mode, int[] items)
                {
                    if (enabled)
                    {
                        switch (mode)
                        {
                            case Mode.Fast:
                                foreach (var item in items)
                                {
                                    var x = item + 1;
                                }
                                break;
                            default:
                                foreach (var outer in items)
                                {
                                    foreach (var inner in items)
                                    {
                                        var x = outer + inner;
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        var x = 1;
                    }
                }
            }
            """,
            "O(n\u00b2)");
    }

    [Fact]
    public void Branch_containing_source_invocation_uses_branch_composition()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(bool enabled, int[] items)
                {
                    if (enabled)
                    {
                        Visit();
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            var x = item + 1;
                        }
                    }
                }

                void Visit()
                {
                }
            }
            """,
            "O(n)");
    }

    [Fact]
    public void Condition_containing_source_invocation_is_known()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M()
                {
                    if (IsEnabled())
                    {
                        var x = 1;
                    }
                }

                bool IsEnabled() => true;
            }
            """,
            "O(1)");
    }

    [Theory]
    [InlineData("custom-property-getter")]
    [InlineData("custom-indexer")]
    [InlineData("external-invocation")]
    [InlineData("while-without-progression")]
    [InlineData("conditional-while-progression")]
    [InlineData("conditional-do-while-progression")]
    [InlineData("complex-control-mutation")]
    [InlineData("bound-dependent-on-call")]
    public void False_positive_safety_patterns_remain_unknown(string scenario)
    {
        string source = scenario switch
        {
            "custom-property-getter" =>
                """
                public sealed class Holder
                {
                    public int Count => 1;
                }

                public sealed class Sample
                {
                    int M(Holder holder) => holder.Count;
                }
                """,
            "custom-indexer" =>
                """
                public sealed class Indexed
                {
                    public int this[int index] => index;
                }

                public sealed class Sample
                {
                    int M(Indexed indexed) => indexed[0];
                }
                """,
            "external-invocation" =>
                """
                public sealed class Sample
                {
                    void M()
                    {
                        System.Console.WriteLine("value");
                    }
                }
                """,
            "while-without-progression" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 0;
                        while (i < count)
                        {
                            var x = i;
                        }
                    }
                }
                """,
            "conditional-while-progression" =>
                """
                public sealed class Sample
                {
                    void M(bool enabled, int count)
                    {
                        var i = 0;
                        while (i < count)
                        {
                            if (enabled)
                            {
                                i++;
                            }
                        }
                    }
                }
                """,
            "conditional-do-while-progression" =>
                """
                public sealed class Sample
                {
                    void M(bool enabled, int count)
                    {
                        var i = 0;
                        do
                        {
                            if (enabled)
                            {
                                i++;
                            }
                        }
                        while (i < count);
                    }
                }
                """,
            "complex-control-mutation" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var i = 0;
                        while (i < count)
                        {
                            i += GetStep();
                        }
                    }

                    int GetStep() => 1;
                }
                """,
            "bound-dependent-on-call" =>
                """
                public sealed class Sample
                {
                    void M()
                    {
                        var limit = GetLimit();
                        var i = 0;
                        while (i < limit)
                        {
                            i++;
                        }
                    }

                    int GetLimit() => 10;
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        AssertMethodComplexity(source, "Unknown");
    }

    [Fact]
    public void Generated_code_marker_does_not_change_extraction_layer_contract()
    {
        AssertMethodComplexity(
            """
            // <auto-generated/>
            public sealed class Sample
            {
                int M() => 1;
            }
            """,
            "O(1)");
    }

    [Fact]
    public void Switch_expression_remains_out_of_scope()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                int M(int value) => value switch
                {
                    0 => 1,
                    _ => 2
                };
            }
            """,
            "Unknown");
    }

    [Fact]
    public void Switch_pattern_labels_remain_out_of_scope()
    {
        AssertMethodComplexity(
            """
            public sealed class Sample
            {
                void M(object value)
                {
                    switch (value)
                    {
                        case int number when IsPositive(number):
                            var x = number + 1;
                            break;
                        default:
                            var y = 1;
                            break;
                    }
                }

                bool IsPositive(int value) => value > 0;
            }
            """,
            "Unknown");
    }

    [Theory]
    [InlineData("missing-condition")]
    [InlineData("wrong-increment-variable")]
    [InlineData("inconsistent-progression")]
    [InlineData("invocation-bound")]
    [InlineData("invalid-multiplicative-factor")]
    [InlineData("invalid-divisive-factor")]
    [InlineData("variable-multiplicative-factor")]
    [InlineData("unrelated-condition")]
    public void Unsupported_for_patterns_are_unknown(string scenario)
    {
        string source = scenario switch
        {
            "missing-condition" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 0; ; i++)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "wrong-increment-variable" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        var j = 0;
                        for (var i = 0; i < count; j++)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "inconsistent-progression" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = count; i > 0; i++)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "invocation-bound" =>
                """
                public sealed class Sample
                {
                    void M()
                    {
                        for (var i = 0; i < GetLimit(); i++)
                        {
                            var x = i + 1;
                        }
                    }

                    int GetLimit() => 10;
                }
                """,
            "invalid-multiplicative-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 1; i < count; i *= 1)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "invalid-divisive-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = count; i > 1; i /= 1)
                        {
                            var x = i - 1;
                        }
                    }
                }
                """,
            "variable-multiplicative-factor" =>
                """
                public sealed class Sample
                {
                    void M(int count, int factor)
                    {
                        for (var i = 1; i < count; i *= factor)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            "unrelated-condition" =>
                """
                public sealed class Sample
                {
                    void M(int count)
                    {
                        for (var i = 1; count > 1; i *= 2)
                        {
                            var x = i + 1;
                        }
                    }
                }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

        AssertMethodComplexity(source, "Unknown");
    }

    [Fact]
    public void Already_cancelled_token_is_respected_by_method_extractor()
    {
        MethodFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                int M() => 42;
            }
            """);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() =>
            MethodComplexityExtractor.AnalyzeMethod(
                facts.MethodDeclaration,
                facts.SemanticModel,
                cancellationTokenSource.Token));
    }

    [Fact]
    public void Cancellation_requested_after_context_creation_is_respected_by_block_extraction()
    {
        MethodFacts facts = CreateFacts(
            """
            public sealed class Sample
            {
                void M(int count)
                {
                    var i = 0;
                    while (i < count)
                    {
                        i++;
                    }
                }
            }
            """);
        using var cancellationTokenSource = new CancellationTokenSource();
        MethodAnalysisContext context = MethodAnalysisContext.Create(
            facts.MethodDeclaration,
            facts.SemanticModel,
            cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();
        var extractor = new MethodComplexityExtractor();

        _ = Assert.Throws<OperationCanceledException>(() =>
            extractor.AnalyzeBlock(
                facts.MethodDeclaration.Body!,
                context));
    }

    private static void AssertMethodComplexity(string source, string expected, string? scenario = null)
    {
        string actual = AnalyzeMethod(source).ToBigONotation();

        Assert.True(
            StringComparer.Ordinal.Equals(expected, actual),
            (scenario is null ? string.Empty : scenario + ": ")
            + "expected "
            + expected
            + " but got "
            + actual);
    }

    private static void AssertMethodComplexity(string[] sources, string expected)
    {
        string actual = AnalyzeMethod(sources, out _).ToBigONotation();

        Assert.True(
            StringComparer.Ordinal.Equals(expected, actual),
            "expected " + expected + " but got " + actual);
    }

    private static void AssertMethodComplexityAndCacheCount(
        string source,
        string expected,
        int expectedCacheCount)
    {
        string actual = AnalyzeMethod([source], out InterproceduralAnalysisContext interproceduralContext)
            .ToBigONotation();

        Assert.True(
            StringComparer.Ordinal.Equals(expected, actual),
            "expected " + expected + " but got " + actual);
        Assert.Equal(expectedCacheCount, interproceduralContext.TemplateCache.Count);
    }

    private static ComplexityExpression AnalyzeMethod(string source)
    {
        return AnalyzeMethod([source], out _);
    }

    private static ComplexityExpression AnalyzeMethod(
        string[] sources,
        out InterproceduralAnalysisContext interproceduralContext)
    {
        CompilationFacts compilationFacts = CreateCompilationFacts(sources);
        MethodFacts facts = GetMethodFacts(compilationFacts, "M");
        interproceduralContext = InterproceduralAnalysisContext.Create(
            facts.SemanticModel.Compilation,
            CancellationToken.None);

        return MethodComplexityExtractor.AnalyzeMethod(
            facts.MethodDeclaration,
            facts.SemanticModel,
            interproceduralContext,
            CancellationToken.None);
    }

    private static ComplexityExpression AnalyzeMethod(
        CompilationFacts facts,
        string methodName,
        InterproceduralAnalysisContext interproceduralContext,
        CancellationToken cancellationToken)
    {
        MethodFacts methodFacts = GetMethodFacts(facts, methodName);
        return MethodComplexityExtractor.AnalyzeMethod(
            methodFacts.MethodDeclaration,
            methodFacts.SemanticModel,
            interproceduralContext,
            cancellationToken);
    }

    private static MethodFacts CreateFacts(string source)
    {
        return CreateFactsFromSources("M", source);
    }

    private static MethodFacts CreateFactsFromSources(string methodName, params string[] sources)
    {
        return GetMethodFacts(CreateCompilationFacts(sources), methodName);
    }

    private static CompilationFacts CreateCompilationFacts(params string[] sources)
    {
        SyntaxTree[] syntaxTrees =
        [
            .. sources.Select(source => CSharpSyntaxTree.ParseText(source))
        ];
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "MethodComplexityExtractorTests",
            syntaxTrees: syntaxTrees,
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return new CompilationFacts(compilation, [.. syntaxTrees]);
    }

    private static MethodFacts GetMethodFacts(
        CompilationFacts facts,
        string methodName)
    {
        MethodDeclarationSyntax methodDeclaration = facts.SyntaxTrees
            .SelectMany(syntaxTree => syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Single(method => StringComparer.Ordinal.Equals(method.Identifier.ValueText, methodName));
        SemanticModel semanticModel = facts.Compilation.GetSemanticModel(methodDeclaration.SyntaxTree);

        return new MethodFacts(semanticModel, methodDeclaration);
    }

    private static IMethodSymbol GetMethodSymbol(
        CompilationFacts facts,
        string methodName)
    {
        MethodFacts methodFacts = GetMethodFacts(facts, methodName);
        return methodFacts.SemanticModel.GetDeclaredSymbol(
            methodFacts.MethodDeclaration,
            CancellationToken.None)
            ?? throw new InvalidOperationException("Expected method declaration to resolve to a symbol.");
    }

    private static async Task<string[]> RunConcurrentAnalysesWithTimeout(
        params Func<string>[] analyses)
    {
        Task<string>[] tasks =
        [
            .. analyses.Select(analysis => Task.Run(analysis))
        ];
        Task<string[]> analysisTask = Task.WhenAll(tasks);
        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        Task completedTask = await Task.WhenAny(analysisTask, timeoutTask);

        Assert.Same(analysisTask, completedTask);
        return await analysisTask;
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
            .. trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record MethodFacts(
        SemanticModel SemanticModel,
        MethodDeclarationSyntax MethodDeclaration);

    private sealed record CompilationFacts(
        CSharpCompilation Compilation,
        ImmutableArray<SyntaxTree> SyntaxTrees);
}

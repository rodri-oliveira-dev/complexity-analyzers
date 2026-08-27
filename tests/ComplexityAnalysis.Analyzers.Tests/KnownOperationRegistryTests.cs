using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;
using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class KnownOperationRegistryTests
{
    [Fact]
    public void Resolver_returns_exact_known_match()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            namespace Fixtures
            {
                public sealed class KnownCollection
                {
                    public bool Probe(int value) => true;
                }

                public sealed class Sample
                {
                    void M(KnownCollection collection)
                    {
                        collection.Probe(1);
                    }
                }
            }
            """);
        KnownOperationMapping expected = CreateMapping(
            KnownCollectionProbeIntIdentity(),
            ComplexityFactory.Linear(ComplexityVariable.N));
        KnownOperationResolver resolver = CreateResolver(expected);

        Assert.True(resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out KnownOperationMapping actual));
        Assert.Equal(expected, actual);
        Assert.Equal(KnownOperationExecutionKind.Immediate, actual.ExecutionKind);
        Assert.Equal(KnownOperationProvenanceKind.Conservative, actual.Provenance.Kind);
        Assert.True(actual.Metadata.IsLookupOperation);
    }

    [Fact]
    public void Resolver_returns_no_mapping_for_unknown_operation()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            namespace Fixtures
            {
                public sealed class KnownCollection
                {
                    public bool Probe(int value) => true;
                }

                public sealed class Sample
                {
                    void M(KnownCollection collection)
                    {
                        collection.Probe(1);
                    }
                }
            }
            """);
        KnownOperationResolver resolver = new(KnownOperationRegistry.Empty);

        Assert.False(resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out _));
    }

    [Fact]
    public void Resolver_distinguishes_overloads_by_parameter_shape()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            namespace Fixtures
            {
                public sealed class KnownCollection
                {
                    public bool Probe(int value) => true;
                    public bool Probe(string value) => true;
                }

                public sealed class Sample
                {
                    void M(KnownCollection collection)
                    {
                        collection.Probe("value");
                    }
                }
            }
            """);
        KnownOperationMapping intMapping = CreateMapping(
            KnownCollectionProbeIntIdentity(),
            ComplexityFactory.Linear(ComplexityVariable.N));
        KnownOperationMapping stringMapping = CreateMapping(
            KnownCollectionProbeStringIdentity(),
            ComplexityFactory.Constant());
        KnownOperationResolver resolver = CreateResolver(intMapping, stringMapping);

        Assert.True(resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out KnownOperationMapping actual));
        Assert.Equal(stringMapping, actual);
        Assert.Equal("O(1)", actual.Complexity.ToBigONotation());
    }

    [Fact]
    public void Resolver_does_not_match_custom_method_with_same_name()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            namespace Fixtures
            {
                public sealed class KnownCollection
                {
                    public bool Probe(int value) => true;
                }

                public sealed class CustomCollection
                {
                    public bool Probe(int value) => true;
                }

                public sealed class Sample
                {
                    void M(CustomCollection collection)
                    {
                        collection.Probe(1);
                    }
                }
            }
            """);
        KnownOperationResolver resolver = CreateResolver(
            CreateMapping(KnownCollectionProbeIntIdentity(), ComplexityFactory.Linear(ComplexityVariable.N)));

        Assert.False(resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out _));
    }

    [Fact]
    public void Resolver_matches_reduced_extension_method_definition()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            using System.Collections.Generic;

            namespace Fixtures
            {
                public static class KnownExtensions
                {
                    public static bool ExtensionProbe<T>(this IEnumerable<T> source, T value) => true;
                }

                public sealed class Sample
                {
                    void M(IEnumerable<int> values)
                    {
                        values.ExtensionProbe(1);
                    }
                }
            }
            """);
        KnownOperationMapping expected = CreateMapping(
            ExtensionProbeIdentity(),
            ComplexityFactory.Linear(ComplexityVariable.N));
        KnownOperationResolver resolver = CreateResolver(expected);

        Assert.NotNull(facts.MethodSymbol.ReducedFrom);
        Assert.True(resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out KnownOperationMapping actual));
        Assert.Equal(expected, actual);
        Assert.Equal(KnownOperationReceiverKind.ReducedExtension, actual.Identity.ReceiverKind);
    }

    [Fact]
    public void Registry_orders_mappings_deterministically()
    {
        KnownOperationMapping first = CreateMapping(
            KnownCollectionProbeIntIdentity(),
            ComplexityFactory.Linear(ComplexityVariable.N));
        KnownOperationMapping second = CreateMapping(
            KnownCollectionProbeStringIdentity(),
            ComplexityFactory.Constant());

        KnownOperationRegistry forward = KnownOperationRegistry.Create([first, second]);
        KnownOperationRegistry reversed = KnownOperationRegistry.Create([second, first]);

        string[] forwardKeys = [.. forward.Mappings.Select(mapping => mapping.Identity.SortKey)];
        string[] reversedKeys = [.. reversed.Mappings.Select(mapping => mapping.Identity.SortKey)];

        Assert.Equal(forwardKeys.OrderBy(key => key, StringComparer.Ordinal), forwardKeys);
        Assert.Equal(forwardKeys, reversedKeys);
    }

    [Fact]
    public void Registry_supports_concurrent_read_resolution()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            namespace Fixtures
            {
                public sealed class KnownCollection
                {
                    public bool Probe(int value) => true;
                }

                public sealed class Sample
                {
                    void M(KnownCollection collection)
                    {
                        collection.Probe(1);
                    }
                }
            }
            """);
        KnownOperationMapping expected = CreateMapping(
            KnownCollectionProbeIntIdentity(),
            ComplexityFactory.Linear(ComplexityVariable.N));
        KnownOperationResolver resolver = CreateResolver(expected);
        ConcurrentQueue<KnownOperationMapping> results = new();
        ConcurrentQueue<string> failures = new();

        ParallelLoopResult loopResult = Parallel.For(
            0,
            256,
            _ =>
            {
                if (resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out KnownOperationMapping actual))
                {
                    results.Enqueue(actual);
                }
                else
                {
                    failures.Enqueue("Expected known operation mapping.");
                }
            });

        Assert.True(loopResult.IsCompleted);
        Assert.Empty(failures);
        Assert.Equal(256, results.Count);
        Assert.All(results, actual => Assert.Equal(expected, actual));
    }

    [Fact]
    public void Unknown_lookup_does_not_become_linear_complexity()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            namespace Fixtures
            {
                public sealed class KnownCollection
                {
                    public bool Probe(int value) => true;
                }

                public sealed class Sample
                {
                    void M(KnownCollection collection)
                    {
                        collection.Probe(1);
                    }
                }
            }
            """);
        KnownOperationResolver resolver = new(KnownOperationRegistry.Empty);

        ComplexityExpression callerResult = resolver.TryResolve(
            facts.MethodSymbol,
            CancellationToken.None,
            out KnownOperationMapping mapping)
            ? mapping.Complexity
            : ComplexityFactory.Unknown();

        Assert.Equal("Unknown", callerResult.ToBigONotation());
        Assert.NotEqual("O(n)", callerResult.ToBigONotation());
    }

    [Theory]
    [InlineData(
        "list-count",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            int M(List<int> values) => values.Count;
        }
        """,
        "O(1)",
        "WorstCase",
        false,
        false,
        false)]
    [InlineData(
        "list-indexer",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            int M(List<int> values) => values[0];
        }
        """,
        "O(1)",
        "WorstCase",
        false,
        false,
        false)]
    [InlineData(
        "array-length",
        """
        public sealed class Sample
        {
            int M(int[] values) => values.Length;
        }
        """,
        "O(1)",
        "WorstCase",
        false,
        false,
        false)]
    [InlineData(
        "string-length",
        """
        public sealed class Sample
        {
            int M(string text) => text.Length;
        }
        """,
        "O(1)",
        "WorstCase",
        false,
        false,
        false)]
    public void Bcl_property_mappings_resolve_semantically(
        string operationFamily,
        string source,
        string expectedComplexity,
        string expectedCase,
        bool expectedEnumeratesReceiver,
        bool expectedOrders,
        bool expectedLookupOperation)
    {
        KnownOperationMapping mapping = ResolveBclProperty(source);

        AssertBclMapping(
            mapping,
            operationFamily,
            expectedComplexity,
            ParseComplexityCase(expectedCase),
            expectedEnumeratesReceiver,
            expectedOrders,
            expectedLookupOperation);
    }

    [Theory]
    [InlineData(
        "list-contains",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            bool M(List<int> values) => values.Contains(1);
        }
        """,
        "O(n)",
        "WorstCase",
        true,
        false,
        true)]
    [InlineData(
        "list-indexof",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            int M(List<int> values) => values.IndexOf(1);
        }
        """,
        "O(n)",
        "WorstCase",
        true,
        false,
        true)]
    [InlineData(
        "list-indexof",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            int M(List<int> values) => values.IndexOf(1, 0);
        }
        """,
        "O(n)",
        "WorstCase",
        true,
        false,
        true)]
    [InlineData(
        "list-indexof",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            int M(List<int> values) => values.IndexOf(1, 0, 1);
        }
        """,
        "O(n)",
        "WorstCase",
        true,
        false,
        true)]
    [InlineData(
        "list-sort",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            void M(List<int> values) => values.Sort();
        }
        """,
        "O(n log n)",
        "WorstCase",
        true,
        true,
        false)]
    [InlineData(
        "list-sort",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            void M(List<int> values, IComparer<int> comparer) => values.Sort(comparer);
        }
        """,
        "O(n log n)",
        "WorstCase",
        true,
        true,
        false)]
    [InlineData(
        "list-sort",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            void M(List<int> values) => values.Sort(static (left, right) => 0);
        }
        """,
        "O(n log n)",
        "WorstCase",
        true,
        true,
        false)]
    [InlineData(
        "list-sort",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            void M(List<int> values, IComparer<int> comparer) => values.Sort(0, values.Count, comparer);
        }
        """,
        "O(n log n)",
        "WorstCase",
        true,
        true,
        false)]
    [InlineData(
        "dictionary-contains-key",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            bool M(Dictionary<int, string> values) => values.ContainsKey(1);
        }
        """,
        "O(1)",
        "Average",
        false,
        false,
        true)]
    [InlineData(
        "dictionary-contains-value",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            bool M(Dictionary<int, string> values) => values.ContainsValue("a");
        }
        """,
        "O(n)",
        "WorstCase",
        true,
        false,
        true)]
    [InlineData(
        "hashset-contains",
        """
        using System.Collections.Generic;

        public sealed class Sample
        {
            bool M(HashSet<int> values) => values.Contains(1);
        }
        """,
        "O(1)",
        "Average",
        false,
        false,
        true)]
    public void Bcl_method_mappings_resolve_semantically(
        string operationFamily,
        string source,
        string expectedComplexity,
        string expectedCase,
        bool expectedEnumeratesReceiver,
        bool expectedOrders,
        bool expectedLookupOperation)
    {
        KnownOperationMapping mapping = ResolveBclInvocation(source);

        AssertBclMapping(
            mapping,
            operationFamily,
            expectedComplexity,
            ParseComplexityCase(expectedCase),
            expectedEnumeratesReceiver,
            expectedOrders,
            expectedLookupOperation);
    }

    [Fact]
    public void Bcl_registry_distinguishes_dictionary_key_and_value_lookup()
    {
        KnownOperationMapping containsKey = ResolveBclInvocation(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(Dictionary<int, string> values) => values.ContainsKey(1);
            }
            """);
        KnownOperationMapping containsValue = ResolveBclInvocation(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(Dictionary<int, string> values) => values.ContainsValue("a");
            }
            """);

        Assert.Equal("dictionary-contains-key", containsKey.Metadata.OperationFamily);
        Assert.Equal("dictionary-contains-value", containsValue.Metadata.OperationFamily);
        Assert.Equal("O(1)", containsKey.Complexity.ToBigONotation());
        Assert.Equal("O(n)", containsValue.Complexity.ToBigONotation());
    }

    [Fact]
    public void Bcl_registry_distinguishes_hashset_contains_from_list_contains()
    {
        KnownOperationMapping listContains = ResolveBclInvocation(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> values) => values.Contains(1);
            }
            """);
        KnownOperationMapping hashSetContains = ResolveBclInvocation(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(HashSet<int> values) => values.Contains(1);
            }
            """);

        Assert.Equal("list-contains", listContains.Metadata.OperationFamily);
        Assert.Equal("hashset-contains", hashSetContains.Metadata.OperationFamily);
        Assert.Equal("O(n)", listContains.Complexity.ToBigONotation());
        Assert.Equal("O(1)", hashSetContains.Complexity.ToBigONotation());
        Assert.Equal(KnownOperationComplexityCase.Average, hashSetContains.ComplexityCase);
    }

    [Fact]
    public void Bcl_registry_does_not_match_custom_homonymous_list_contains()
    {
        InvocationFacts facts = CreateInvocationFacts(
            """
            namespace MyCompany
            {
                public sealed class List<T>
                {
                    public bool Contains(T value) => true;
                }

                public sealed class Sample
                {
                    bool M(List<int> values) => values.Contains(1);
                }
            }
            """);
        KnownOperationResolver resolver = new(KnownOperationRegistry.Bcl);

        Assert.False(resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out _));
    }

    [Fact]
    public void Bcl_registry_contains_no_linq_mappings()
    {
        Assert.DoesNotContain(
            KnownOperationRegistry.Bcl.Mappings,
            mapping => StringComparer.Ordinal.Equals(
                "System.Linq",
                mapping.Identity.ContainingType.NamespaceName));
    }

    [Theory]
    [InlineData(
        "linq-where",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            IEnumerable<int> M(IEnumerable<int> values) => values.Where(value => value > 0);
        }
        """,
        "O(n)",
        "Deferred",
        true,
        false,
        false)]
    [InlineData(
        "linq-select",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            IEnumerable<int> M(IEnumerable<int> values) => values.Select(value => value + 1);
        }
        """,
        "O(n)",
        "Deferred",
        true,
        false,
        false)]
    [InlineData(
        "linq-orderby",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            IOrderedEnumerable<int> M(IEnumerable<int> values) => values.OrderBy(value => value);
        }
        """,
        "O(n log n)",
        "Deferred",
        true,
        false,
        true)]
    [InlineData(
        "linq-distinct",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            IEnumerable<int> M(IEnumerable<int> values) => values.Distinct();
        }
        """,
        "O(n)",
        "Deferred",
        true,
        false,
        false)]
    [InlineData(
        "linq-groupby",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            IEnumerable<IGrouping<int, int>> M(IEnumerable<int> values) => values.GroupBy(value => value);
        }
        """,
        "O(n)",
        "Deferred",
        true,
        false,
        false)]
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
        "O(n)",
        "Immediate",
        true,
        true,
        false)]
    [InlineData(
        "linq-any",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            bool M(IEnumerable<int> values) => values.Any();
        }
        """,
        "O(1)",
        "Immediate",
        false,
        false,
        false)]
    [InlineData(
        "linq-count",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            int M(IEnumerable<int> values) => values.Count();
        }
        """,
        "O(n)",
        "Immediate",
        true,
        false,
        false)]
    [InlineData(
        "linq-contains",
        """
        using System.Collections.Generic;
        using System.Linq;

        public sealed class Sample
        {
            bool M(IEnumerable<int> values) => values.Contains(1);
        }
        """,
        "O(n)",
        "Immediate",
        true,
        false,
        false)]
    public void Linq_mappings_resolve_semantically(
        string operationFamily,
        string source,
        string expectedComplexity,
        string expectedExecutionKind,
        bool expectedEnumeratesReceiver,
        bool expectedMaterializes,
        bool expectedOrders)
    {
        KnownOperationMapping mapping = ResolveDefaultInvocation(source);

        Assert.Equal(operationFamily, mapping.Metadata.OperationFamily);
        Assert.Equal(expectedComplexity, mapping.Complexity.ToBigONotation());
        Assert.Equal(ParseExecutionKind(expectedExecutionKind), mapping.ExecutionKind);
        Assert.Equal(expectedEnumeratesReceiver, mapping.Metadata.EnumeratesReceiver);
        Assert.Equal(expectedMaterializes, mapping.Metadata.Materializes);
        Assert.Equal(expectedOrders, mapping.Metadata.Orders);
        Assert.Equal(KnownOperationProvenanceKind.OfficialDocumentation, mapping.Provenance.Kind);
        Assert.Contains("learn.microsoft.com", mapping.Provenance.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Linq_registry_does_not_match_custom_homonymous_extensions()
    {
        InvocationFacts facts = CreateInvocationFacts(
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
            """);
        KnownOperationResolver resolver = new(KnownOperationRegistry.Default);

        Assert.False(resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out _));
    }

    [Fact]
    public void Linq_mappings_have_verified_provenance()
    {
        Assert.All(
            KnownOperationRegistry.Default.Mappings.Where(mapping => StringComparer.Ordinal.Equals(
                "System.Linq",
                mapping.Identity.ContainingType.NamespaceName)),
            mapping =>
            {
                Assert.Equal(KnownOperationProvenanceKind.OfficialDocumentation, mapping.Provenance.Kind);
                Assert.Contains("learn.microsoft.com", mapping.Provenance.Note, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Bcl_mappings_have_verified_or_explicitly_conservative_provenance()
    {
        Assert.All(
            KnownOperationRegistry.Bcl.Mappings,
            mapping =>
            {
                Assert.Contains(
                    "src/ComplexityAnalysis.Roslyn/BCL/BCLComplexityMappings.cs",
                    mapping.Provenance.Note,
                    StringComparison.Ordinal);
                Assert.True(
                    mapping.Provenance.Note.Contains("learn.microsoft.com/dotnet/api/", StringComparison.Ordinal)
                    || mapping.Provenance.Note.Contains("dotnet/runtime", StringComparison.Ordinal),
                    "Expected official documentation, runtime source, or explicit conservative provenance for "
                    + mapping.Identity.SortKey);
                if (mapping.Provenance.Kind == KnownOperationProvenanceKind.Conservative)
                {
                    Assert.Contains("conservative", mapping.Provenance.Note, StringComparison.OrdinalIgnoreCase);
                }
            });
    }

    private static KnownOperationResolver CreateResolver(params KnownOperationMapping[] mappings)
    {
        return new KnownOperationResolver(KnownOperationRegistry.Create(mappings));
    }

    private static KnownOperationMapping ResolveBclInvocation(string source)
    {
        InvocationFacts facts = CreateInvocationFacts(source);
        KnownOperationResolver resolver = new(KnownOperationRegistry.Bcl);

        return resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out KnownOperationMapping mapping)
            ? mapping
            : throw new InvalidOperationException("Expected BCL invocation mapping for " + facts.MethodSymbol);
    }

    private static KnownOperationMapping ResolveDefaultInvocation(string source)
    {
        InvocationFacts facts = CreateInvocationFacts(source);
        KnownOperationResolver resolver = new(KnownOperationRegistry.Default);

        return resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out KnownOperationMapping mapping)
            ? mapping
            : throw new InvalidOperationException("Expected known invocation mapping for " + facts.MethodSymbol);
    }

    private static KnownOperationMapping ResolveBclProperty(string source)
    {
        PropertyFacts facts = CreatePropertyFacts(source);
        KnownOperationResolver resolver = new(KnownOperationRegistry.Bcl);

        return resolver.TryResolve(facts.PropertySymbol, CancellationToken.None, out KnownOperationMapping mapping)
            ? mapping
            : throw new InvalidOperationException("Expected BCL property mapping for " + facts.PropertySymbol);
    }

    private static void AssertBclMapping(
        KnownOperationMapping mapping,
        string operationFamily,
        string expectedComplexity,
        KnownOperationComplexityCase expectedCase,
        bool expectedEnumeratesReceiver,
        bool expectedOrders,
        bool expectedLookupOperation)
    {
        Assert.Equal(operationFamily, mapping.Metadata.OperationFamily);
        Assert.Equal(expectedComplexity, mapping.Complexity.ToBigONotation());
        Assert.Equal(KnownOperationExecutionKind.Immediate, mapping.ExecutionKind);
        Assert.Equal(expectedCase, mapping.ComplexityCase);
        Assert.Equal(expectedEnumeratesReceiver, mapping.Metadata.EnumeratesReceiver);
        Assert.False(mapping.Metadata.Materializes);
        Assert.Equal(expectedOrders, mapping.Metadata.Orders);
        Assert.Equal(expectedLookupOperation, mapping.Metadata.IsLookupOperation);
    }

    private static KnownOperationComplexityCase ParseComplexityCase(string value)
    {
        return Enum.TryParse(value, ignoreCase: false, out KnownOperationComplexityCase complexityCase)
            ? complexityCase
            : throw new ArgumentException("Unknown complexity case.", nameof(value));
    }

    private static KnownOperationExecutionKind ParseExecutionKind(string value)
    {
        return Enum.TryParse(value, ignoreCase: false, out KnownOperationExecutionKind executionKind)
            ? executionKind
            : throw new ArgumentException("Unknown execution kind.", nameof(value));
    }

    private static KnownOperationMapping CreateMapping(
        KnownOperationIdentity identity,
        ComplexityExpression complexity)
    {
        return new KnownOperationMapping(
            identity,
            complexity,
            KnownOperationExecutionKind.Immediate,
            KnownOperationProvenance.Conservative("test-fixture"),
            new KnownOperationMetadata(
                "fixture-lookup",
                enumeratesReceiver: true,
                materializes: false,
                orders: false,
                isLookupOperation: true));
    }

    private static KnownOperationIdentity KnownCollectionProbeIntIdentity()
    {
        return KnownOperationIdentity.Create(
            KnownOperationReceiverKind.Instance,
            KnownOperationTypeIdentity.Named("Fixtures", "KnownCollection"),
            "Probe",
            methodArity: 0,
            [KnownOperationTypeIdentity.Named("System", "Int32")]);
    }

    private static KnownOperationIdentity KnownCollectionProbeStringIdentity()
    {
        return KnownOperationIdentity.Create(
            KnownOperationReceiverKind.Instance,
            KnownOperationTypeIdentity.Named("Fixtures", "KnownCollection"),
            "Probe",
            methodArity: 0,
            [KnownOperationTypeIdentity.Named("System", "String")]);
    }

    private static KnownOperationIdentity ExtensionProbeIdentity()
    {
        return KnownOperationIdentity.Create(
            KnownOperationReceiverKind.ReducedExtension,
            KnownOperationTypeIdentity.Named("Fixtures", "KnownExtensions"),
            "ExtensionProbe",
            methodArity: 1,
            [
                KnownOperationTypeIdentity.Named("System.Collections.Generic", "IEnumerable`1"),
                KnownOperationTypeIdentity.TypeParameter(0)
            ]);
    }

    private static InvocationFacts CreateInvocationFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "KnownOperationRegistryTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        InvocationExpressionSyntax invocation = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single();
        IMethodSymbol methodSymbol = semanticModel.GetSymbolInfo(invocation, CancellationToken.None).Symbol as IMethodSymbol
            ?? throw new InvalidOperationException("Expected invocation to resolve to a method symbol.");

        return new InvocationFacts(methodSymbol);
    }

    private static PropertyFacts CreatePropertyFacts(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "KnownOperationRegistryTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        ExpressionSyntax propertyAccess = FindPropertyAccess(syntaxTree);
        IPropertySymbol propertySymbol = semanticModel.GetSymbolInfo(propertyAccess, CancellationToken.None).Symbol as IPropertySymbol
            ?? throw new InvalidOperationException("Expected property access to resolve to a property symbol.");

        return new PropertyFacts(propertySymbol);
    }

    private static ExpressionSyntax FindPropertyAccess(SyntaxTree syntaxTree)
    {
        SyntaxNode root = syntaxTree.GetRoot();
        ElementAccessExpressionSyntax? elementAccess = root
            .DescendantNodes()
            .OfType<ElementAccessExpressionSyntax>()
            .SingleOrDefault();

        return elementAccess is not null
            ? elementAccess
            : root
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Single(memberAccess => memberAccess.Name.Identifier.ValueText is "Count" or "Length");
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

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private sealed record InvocationFacts(IMethodSymbol MethodSymbol);

    private sealed record PropertyFacts(IPropertySymbol PropertySymbol);
}

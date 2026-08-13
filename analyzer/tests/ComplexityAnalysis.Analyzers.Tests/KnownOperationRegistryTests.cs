using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
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

        _ = Parallel.For(
            0,
            256,
            _ =>
            {
                if (!resolver.TryResolve(facts.MethodSymbol, CancellationToken.None, out KnownOperationMapping actual))
                {
                    throw new InvalidOperationException("Expected known operation mapping.");
                }

                if (!expected.Equals(actual))
                {
                    throw new InvalidOperationException("Concurrent read returned an unexpected mapping.");
                }
            });
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

    private static KnownOperationResolver CreateResolver(params KnownOperationMapping[] mappings)
    {
        return new KnownOperationResolver(KnownOperationRegistry.Create(mappings));
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

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } =
        [
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location)
        ];

    private sealed record InvocationFacts(IMethodSymbol MethodSymbol);
}

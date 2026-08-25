using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal static class LinqMappings
{
    private const string LinqDocumentationOrigin = "verified: ";

    internal static ImmutableArray<KnownOperationMapping> Create()
    {
        ImmutableArray<KnownOperationMapping>.Builder mappings =
            ImmutableArray.CreateBuilder<KnownOperationMapping>();

        AddDeferredMappings(mappings);
        AddImmediateMappings(mappings);

        return mappings.ToImmutable();
    }

    private static void AddDeferredMappings(ImmutableArray<KnownOperationMapping>.Builder mappings)
    {
        AddDeferredLinear(mappings, "Where", methodArity: 1, Parameters(Enumerable(), Func2()), "linq-where");
        AddDeferredLinear(mappings, "Where", methodArity: 1, Parameters(Enumerable(), Func3()), "linq-where");
        AddDeferredLinear(mappings, "Select", methodArity: 2, Parameters(Enumerable(), Func2()), "linq-select");
        AddDeferredLinear(mappings, "Select", methodArity: 2, Parameters(Enumerable(), Func3()), "linq-select");
        AddDeferred(
            mappings,
            "SelectMany",
            methodArity: 2,
            Parameters(Enumerable(), Func2()),
            "linq-select-many",
            ComplexityFactory.Unknown(),
            Provenance("learn.microsoft.com/dotnet/api/system.linq.enumerable.selectmany"));
        AddDeferred(
            mappings,
            "SelectMany",
            methodArity: 3,
            Parameters(Enumerable(), Func2(), Func3()),
            "linq-select-many",
            ComplexityFactory.Unknown(),
            Provenance("learn.microsoft.com/dotnet/api/system.linq.enumerable.selectmany"));
        AddDeferredOrdering(mappings, "OrderBy", Parameters(Enumerable(), Func2()), "linq-orderby");
        AddDeferredOrdering(mappings, "OrderBy", Parameters(Enumerable(), Func2(), Comparer()), "linq-orderby");
        AddDeferredOrdering(mappings, "OrderByDescending", Parameters(Enumerable(), Func2()), "linq-orderby-descending");
        AddDeferredOrdering(mappings, "OrderByDescending", Parameters(Enumerable(), Func2(), Comparer()), "linq-orderby-descending");
        AddDeferredOrdering(mappings, "ThenBy", Parameters(OrderedEnumerable(), Func2()), "linq-thenby");
        AddDeferredOrdering(mappings, "ThenBy", Parameters(OrderedEnumerable(), Func2(), Comparer()), "linq-thenby");
        AddDeferredOrdering(mappings, "ThenByDescending", Parameters(OrderedEnumerable(), Func2()), "linq-thenby-descending");
        AddDeferredOrdering(mappings, "ThenByDescending", Parameters(OrderedEnumerable(), Func2(), Comparer()), "linq-thenby-descending");
        AddDeferredLinear(mappings, "Distinct", methodArity: 1, Parameters(Enumerable()), "linq-distinct");
        AddDeferredLinear(mappings, "Distinct", methodArity: 1, Parameters(Enumerable(), EqualityComparer()), "linq-distinct");
        AddDeferredLinear(mappings, "GroupBy", methodArity: 2, Parameters(Enumerable(), Func2()), "linq-groupby");
        AddDeferredLinear(mappings, "GroupBy", methodArity: 2, Parameters(Enumerable(), Func2(), EqualityComparer()), "linq-groupby");
        AddDeferredLinear(mappings, "GroupBy", methodArity: 3, Parameters(Enumerable(), Func2(), Func2()), "linq-groupby");
        AddDeferredLinear(mappings, "GroupBy", methodArity: 3, Parameters(Enumerable(), Func2(), Func2(), EqualityComparer()), "linq-groupby");
    }

    private static void AddImmediateMappings(ImmutableArray<KnownOperationMapping>.Builder mappings)
    {
        AddImmediate(mappings, "Any", methodArity: 1, Parameters(Enumerable()), "linq-any", ComplexityFactory.Constant(), materializes: false, orders: false);
        AddImmediateLinear(mappings, "Any", methodArity: 1, Parameters(Enumerable(), Func2()), "linq-any");
        AddImmediateLinear(mappings, "All", methodArity: 1, Parameters(Enumerable(), Func2()), "linq-all");
        AddImmediateLinear(mappings, "Contains", methodArity: 1, Parameters(Enumerable(), KnownOperationTypeIdentity.TypeParameter(0)), "linq-contains");
        AddImmediateLinear(mappings, "Contains", methodArity: 1, Parameters(Enumerable(), KnownOperationTypeIdentity.TypeParameter(0), EqualityComparer()), "linq-contains");
        AddImmediateLinear(mappings, "Count", methodArity: 1, Parameters(Enumerable()), "linq-count");
        AddImmediateLinear(mappings, "Count", methodArity: 1, Parameters(Enumerable(), Func2()), "linq-count");
        AddImmediateLinear(mappings, "LongCount", methodArity: 1, Parameters(Enumerable()), "linq-long-count");
        AddImmediateLinear(mappings, "LongCount", methodArity: 1, Parameters(Enumerable(), Func2()), "linq-long-count");
        AddImmediateLinear(mappings, "ToList", methodArity: 1, Parameters(Enumerable()), "linq-to-list", materializes: true);
        AddImmediateLinear(mappings, "ToArray", methodArity: 1, Parameters(Enumerable()), "linq-to-array", materializes: true);
        AddImmediateLinear(mappings, "ToDictionary", methodArity: 2, Parameters(Enumerable()), "linq-to-dictionary", materializes: true);
        AddImmediateLinear(mappings, "ToDictionary", methodArity: 2, Parameters(Enumerable(), EqualityComparer()), "linq-to-dictionary", materializes: true);
        AddImmediateLinear(mappings, "ToDictionary", methodArity: 2, Parameters(Enumerable(), Func2()), "linq-to-dictionary", materializes: true);
        AddImmediateLinear(mappings, "ToDictionary", methodArity: 2, Parameters(Enumerable(), Func2(), EqualityComparer()), "linq-to-dictionary", materializes: true);
        AddImmediateLinear(mappings, "ToDictionary", methodArity: 3, Parameters(Enumerable(), Func2(), Func2()), "linq-to-dictionary", materializes: true);
        AddImmediateLinear(mappings, "ToDictionary", methodArity: 3, Parameters(Enumerable(), Func2(), Func2(), EqualityComparer()), "linq-to-dictionary", materializes: true);
        AddImmediateLinear(mappings, "ToHashSet", methodArity: 1, Parameters(Enumerable()), "linq-to-hash-set", materializes: true);
        AddImmediateLinear(mappings, "ToHashSet", methodArity: 1, Parameters(Enumerable(), EqualityComparer()), "linq-to-hash-set", materializes: true);
        AddImmediateLinear(mappings, "Sum", methodArity: 0, Parameters(Enumerable()), "linq-sum");
        AddImmediateLinear(mappings, "Sum", methodArity: 1, Parameters(Enumerable(), Func2()), "linq-sum");
        AddImmediateLinear(mappings, "Min", methodArity: 0, Parameters(Enumerable()), "linq-min");
        AddImmediateLinear(mappings, "Min", methodArity: 1, Parameters(Enumerable()), "linq-min");
        AddImmediateLinear(mappings, "Min", methodArity: 2, Parameters(Enumerable(), Func2()), "linq-min");
        AddImmediateLinear(mappings, "Max", methodArity: 0, Parameters(Enumerable()), "linq-max");
        AddImmediateLinear(mappings, "Max", methodArity: 1, Parameters(Enumerable()), "linq-max");
        AddImmediateLinear(mappings, "Max", methodArity: 2, Parameters(Enumerable(), Func2()), "linq-max");
        AddImmediateLinear(mappings, "Aggregate", methodArity: 1, Parameters(Enumerable(), Func3()), "linq-aggregate");
        AddImmediateLinear(mappings, "Aggregate", methodArity: 2, Parameters(Enumerable(), KnownOperationTypeIdentity.TypeParameter(1), Func3()), "linq-aggregate");
        AddImmediateLinear(mappings, "Aggregate", methodArity: 3, Parameters(Enumerable(), KnownOperationTypeIdentity.TypeParameter(1), Func3(), Func2()), "linq-aggregate");
    }

    private static void AddDeferredLinear(
        ImmutableArray<KnownOperationMapping>.Builder mappings,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        string operationFamily)
    {
        AddDeferred(
            mappings,
            methodMetadataName,
            methodArity,
            parameterTypes,
            operationFamily,
            ComplexityFactory.Linear(ComplexityVariable.N),
            Provenance("learn.microsoft.com/dotnet/standard/linq/deferred-execution-lazy-evaluation; learn.microsoft.com/dotnet/api/system.linq.enumerable." + DocumentationSlug(methodMetadataName)));
    }

    private static void AddDeferredOrdering(
        ImmutableArray<KnownOperationMapping>.Builder mappings,
        string methodMetadataName,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        string operationFamily)
    {
        AddDeferred(
            mappings,
            methodMetadataName,
            methodArity: 2,
            parameterTypes,
            operationFamily,
            ComplexityFactory.NLogN(ComplexityVariable.N),
            Provenance("learn.microsoft.com/dotnet/api/system.linq.enumerable.orderby; learn.microsoft.com/dotnet/standard/linq/deferred-execution-lazy-evaluation"));
    }

    private static void AddDeferred(
        ImmutableArray<KnownOperationMapping>.Builder mappings,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        string operationFamily,
        ComplexityExpression complexity,
        KnownOperationProvenance provenance)
    {
        AddExtensionMappings(
            mappings,
            methodMetadataName,
            methodArity,
            parameterTypes,
            complexity,
            KnownOperationExecutionKind.Deferred,
            provenance,
            new KnownOperationMetadata(
                operationFamily,
                enumeratesReceiver: true,
                materializes: false,
                orders: operationFamily.Contains("orderby") || operationFamily.Contains("thenby"),
                isLookupOperation: false));
    }

    private static void AddImmediateLinear(
        ImmutableArray<KnownOperationMapping>.Builder mappings,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        string operationFamily,
        bool materializes = false)
    {
        AddImmediate(
            mappings,
            methodMetadataName,
            methodArity,
            parameterTypes,
            operationFamily,
            ComplexityFactory.Linear(ComplexityVariable.N),
            materializes,
            orders: false);
    }

    private static void AddImmediate(
        ImmutableArray<KnownOperationMapping>.Builder mappings,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        string operationFamily,
        ComplexityExpression complexity,
        bool materializes,
        bool orders)
    {
        AddExtensionMappings(
            mappings,
            methodMetadataName,
            methodArity,
            parameterTypes,
            complexity,
            KnownOperationExecutionKind.Immediate,
            Provenance("learn.microsoft.com/dotnet/api/system.linq.enumerable." + DocumentationSlug(methodMetadataName) + "; dotnet/runtime System.Linq sources"),
            new KnownOperationMetadata(
                operationFamily,
                enumeratesReceiver: complexity is not ConstantComplexity,
                materializes,
                orders,
                isLookupOperation: operationFamily == "linq-contains"));
    }

    private static void AddExtensionMappings(
        ImmutableArray<KnownOperationMapping>.Builder mappings,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        ComplexityExpression complexity,
        KnownOperationExecutionKind executionKind,
        KnownOperationProvenance provenance,
        KnownOperationMetadata metadata)
    {
        mappings.Add(new KnownOperationMapping(
            ExtensionMethod(KnownOperationReceiverKind.ReducedExtension, methodMetadataName, methodArity, parameterTypes),
            complexity,
            executionKind,
            provenance,
            metadata));
        mappings.Add(new KnownOperationMapping(
            ExtensionMethod(KnownOperationReceiverKind.Static, methodMetadataName, methodArity, parameterTypes),
            complexity,
            executionKind,
            provenance,
            metadata));
    }

    private static KnownOperationIdentity ExtensionMethod(
        KnownOperationReceiverKind receiverKind,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes)
    {
        return KnownOperationIdentity.Create(
            receiverKind,
            Named("System.Linq", "Enumerable"),
            methodMetadataName,
            methodArity,
            parameterTypes);
    }

    private static KnownOperationTypeIdentity Enumerable()
    {
        return Named("System.Collections.Generic", "IEnumerable`1");
    }

    private static KnownOperationTypeIdentity OrderedEnumerable()
    {
        return Named("System.Linq", "IOrderedEnumerable`1");
    }

    private static KnownOperationTypeIdentity Func2()
    {
        return Named("System", "Func`2");
    }

    private static KnownOperationTypeIdentity Func3()
    {
        return Named("System", "Func`3");
    }

    private static KnownOperationTypeIdentity Comparer()
    {
        return Named("System.Collections.Generic", "IComparer`1");
    }

    private static KnownOperationTypeIdentity EqualityComparer()
    {
        return Named("System.Collections.Generic", "IEqualityComparer`1");
    }

    private static KnownOperationTypeIdentity Named(string namespaceName, string metadataName)
    {
        return KnownOperationTypeIdentity.Named(namespaceName, metadataName);
    }

    private static ImmutableArray<KnownOperationTypeIdentity> Parameters(params KnownOperationTypeIdentity[] parameterTypes)
    {
        return ImmutableArray.Create(parameterTypes);
    }

    private static KnownOperationProvenance Provenance(string sourceIdentifier)
    {
        return KnownOperationProvenance.OfficialDocumentation(LinqDocumentationOrigin + sourceIdentifier);
    }

    private static string DocumentationSlug(string methodMetadataName)
    {
        return methodMetadataName.ToLowerInvariant();
    }
}

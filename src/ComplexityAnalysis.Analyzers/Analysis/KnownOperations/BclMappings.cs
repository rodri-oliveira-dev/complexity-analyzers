using System.Collections.Immutable;

using ComplexityAnalysis.Analyzers.Model;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal static class BclMappings
{
    private const string LegacyBclRegistryOrigin =
        "inspired by src/ComplexityAnalysis.Roslyn/BCL/BCLComplexityMappings.cs; verified: ";

    internal static ImmutableArray<KnownOperationMapping> Create()
    {
        ImmutableArray<KnownOperationMapping>.Builder mappings =
            ImmutableArray.CreateBuilder<KnownOperationMapping>();

        AddListMappings(mappings);
        AddDictionaryMappings(mappings);
        AddHashSetMappings(mappings);
        AddArrayMappings(mappings);
        AddStringMappings(mappings);

        return mappings.ToImmutable();
    }

    private static void AddListMappings(ImmutableArray<KnownOperationMapping>.Builder mappings)
    {
        KnownOperationTypeIdentity list = Named("System.Collections.Generic", "List`1");
        KnownOperationTypeIdentity typeParameter = KnownOperationTypeIdentity.TypeParameter(0);
        KnownOperationTypeIdentity int32 = Named("System", "Int32");
        KnownOperationTypeIdentity comparer = Named("System.Collections.Generic", "IComparer`1");
        KnownOperationTypeIdentity comparison = Named("System", "Comparison`1");

        mappings.Add(ConstantProperty(
            list,
            "get_Count",
            Parameters(),
            "list-count",
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.count")));
        mappings.Add(ConstantProperty(
            list,
            "get_Item",
            Parameters(int32),
            "list-indexer",
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.item")));
        mappings.Add(LinearLookup(
            list,
            "Contains",
            Parameters(typeParameter),
            "list-contains",
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.contains")));
        mappings.Add(LinearLookup(
            list,
            "IndexOf",
            Parameters(typeParameter),
            "list-indexof",
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.indexof")));
        mappings.Add(LinearLookup(
            list,
            "IndexOf",
            Parameters(typeParameter, int32),
            "list-indexof",
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.indexof")));
        mappings.Add(LinearLookup(
            list,
            "IndexOf",
            Parameters(typeParameter, int32, int32),
            "list-indexof",
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.indexof")));
        mappings.Add(Sort(
            list,
            Parameters(),
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.sort")));
        mappings.Add(Sort(
            list,
            Parameters(comparer),
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.sort")));
        mappings.Add(Sort(
            list,
            Parameters(comparison),
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.sort")));
        mappings.Add(Sort(
            list,
            Parameters(int32, int32, comparer),
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.list-1.sort")));
    }

    private static void AddDictionaryMappings(ImmutableArray<KnownOperationMapping>.Builder mappings)
    {
        KnownOperationTypeIdentity dictionary = Named("System.Collections.Generic", "Dictionary`2");
        KnownOperationTypeIdentity keyTypeParameter = KnownOperationTypeIdentity.TypeParameter(0);
        KnownOperationTypeIdentity valueTypeParameter = KnownOperationTypeIdentity.TypeParameter(1);

        mappings.Add(new KnownOperationMapping(
            InstanceMethod(dictionary, "ContainsKey", Parameters(keyTypeParameter)),
            ComplexityFactory.Constant(),
            KnownOperationExecutionKind.Immediate,
            Documentation(
                "learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.containskey; approaches O(1), recorded average-case"),
            LookupMetadata("dictionary-contains-key", enumeratesReceiver: false),
            KnownOperationComplexityCase.Average));
        mappings.Add(LinearLookup(
            dictionary,
            "ContainsValue",
            Parameters(valueTypeParameter),
            "dictionary-contains-value",
            Documentation("learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.containsvalue")));
    }

    private static void AddHashSetMappings(ImmutableArray<KnownOperationMapping>.Builder mappings)
    {
        KnownOperationTypeIdentity hashSet = Named("System.Collections.Generic", "HashSet`1");
        KnownOperationTypeIdentity typeParameter = KnownOperationTypeIdentity.TypeParameter(0);

        mappings.Add(new KnownOperationMapping(
            InstanceMethod(hashSet, "Contains", Parameters(typeParameter)),
            ComplexityFactory.Constant(),
            KnownOperationExecutionKind.Immediate,
            Documentation(
                "learn.microsoft.com/dotnet/api/system.collections.generic.hashset-1.contains; O(1) lookup recorded average-case"),
            LookupMetadata("hashset-contains", enumeratesReceiver: false),
            KnownOperationComplexityCase.Average));
    }

    private static void AddArrayMappings(ImmutableArray<KnownOperationMapping>.Builder mappings)
    {
        mappings.Add(ConstantProperty(
            Named("System", "Array"),
            "get_Length",
            Parameters(),
            "array-length",
            Conservative(
                "learn.microsoft.com/dotnet/api/system.array.length documents fixed total element count; conservative O(1) for intrinsic array length")));
    }

    private static void AddStringMappings(ImmutableArray<KnownOperationMapping>.Builder mappings)
    {
        mappings.Add(ConstantProperty(
            Named("System", "String"),
            "get_Length",
            Parameters(),
            "string-length",
            RuntimeSource(
                "raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Private.CoreLib/src/System/String.cs: String.Length returns _stringLength")));
    }

    private static KnownOperationMapping ConstantProperty(
        KnownOperationTypeIdentity containingType,
        string accessorMetadataName,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        string operationFamily,
        KnownOperationProvenance provenance)
    {
        return new KnownOperationMapping(
            InstanceMethod(containingType, accessorMetadataName, parameterTypes),
            ComplexityFactory.Constant(),
            KnownOperationExecutionKind.Immediate,
            provenance,
            new KnownOperationMetadata(
                operationFamily,
                enumeratesReceiver: false,
                materializes: false,
                orders: false,
                isLookupOperation: false));
    }

    private static KnownOperationMapping LinearLookup(
        KnownOperationTypeIdentity containingType,
        string methodMetadataName,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        string operationFamily,
        KnownOperationProvenance provenance)
    {
        return new KnownOperationMapping(
            InstanceMethod(containingType, methodMetadataName, parameterTypes),
            ComplexityFactory.Linear(ComplexityVariable.N),
            KnownOperationExecutionKind.Immediate,
            provenance,
            LookupMetadata(operationFamily, enumeratesReceiver: true));
    }

    private static KnownOperationMapping Sort(
        KnownOperationTypeIdentity containingType,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes,
        KnownOperationProvenance provenance)
    {
        return new KnownOperationMapping(
            InstanceMethod(containingType, "Sort", parameterTypes),
            ComplexityFactory.NLogN(ComplexityVariable.N),
            KnownOperationExecutionKind.Immediate,
            provenance,
            new KnownOperationMetadata(
                "list-sort",
                enumeratesReceiver: true,
                materializes: false,
                orders: true,
                isLookupOperation: false));
    }

    private static KnownOperationIdentity InstanceMethod(
        KnownOperationTypeIdentity containingType,
        string methodMetadataName,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes)
    {
        return KnownOperationIdentity.Create(
            KnownOperationReceiverKind.Instance,
            containingType,
            methodMetadataName,
            methodArity: 0,
            parameterTypes);
    }

    private static KnownOperationMetadata LookupMetadata(string operationFamily, bool enumeratesReceiver)
    {
        return new KnownOperationMetadata(
            operationFamily,
            enumeratesReceiver,
            materializes: false,
            orders: false,
            isLookupOperation: true);
    }

    private static KnownOperationTypeIdentity Named(string namespaceName, string metadataName)
    {
        return KnownOperationTypeIdentity.Named(namespaceName, metadataName);
    }

    private static ImmutableArray<KnownOperationTypeIdentity> Parameters(params KnownOperationTypeIdentity[] parameterTypes)
    {
        return ImmutableArray.Create(parameterTypes);
    }

    private static KnownOperationProvenance Documentation(string sourceIdentifier)
    {
        return KnownOperationProvenance.OfficialDocumentation(
            LegacyBclRegistryOrigin + sourceIdentifier);
    }

    private static KnownOperationProvenance RuntimeSource(string sourceIdentifier)
    {
        return KnownOperationProvenance.RuntimeSource(
            LegacyBclRegistryOrigin + sourceIdentifier);
    }

    private static KnownOperationProvenance Conservative(string sourceIdentifier)
    {
        return KnownOperationProvenance.Conservative(
            LegacyBclRegistryOrigin + sourceIdentifier);
    }
}

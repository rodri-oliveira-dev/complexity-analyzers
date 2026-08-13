using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal sealed class KnownOperationRegistry
{
    private readonly ImmutableDictionary<KnownOperationIdentity, KnownOperationMapping> mappingsByIdentity;

    private KnownOperationRegistry(ImmutableArray<KnownOperationMapping> mappings)
    {
        Mappings = mappings;
        mappingsByIdentity = mappings.ToImmutableDictionary(
            mapping => mapping.Identity,
            mapping => mapping);
    }

    internal ImmutableArray<KnownOperationMapping> Mappings
    {
        get;
    }

    internal static KnownOperationRegistry Empty
    {
        get;
    } = Create([]);

    internal static KnownOperationRegistry Create(IEnumerable<KnownOperationMapping> mappings)
    {
        if (mappings is null)
        {
            throw new ArgumentNullException(nameof(mappings));
        }

        ImmutableArray<KnownOperationMapping> orderedMappings = mappings
            .Select(mapping => mapping ?? throw new ArgumentException("Mappings must not contain null values.", nameof(mappings)))
            .OrderBy(mapping => mapping.Identity)
            .ToImmutableArray();

        HashSet<KnownOperationIdentity> identities = [];
        foreach (KnownOperationMapping mapping in orderedMappings)
        {
            if (!identities.Add(mapping.Identity))
            {
                throw new ArgumentException("Known operation mappings must have unique identities.", nameof(mappings));
            }
        }

        return new KnownOperationRegistry(orderedMappings);
    }

    internal bool TryGetMapping(KnownOperationIdentity identity, out KnownOperationMapping mapping)
    {
        _ = identity ?? throw new ArgumentNullException(nameof(identity));

        return mappingsByIdentity.TryGetValue(identity, out mapping!);
    }
}

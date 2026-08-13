using System;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal sealed class KnownOperationTypeIdentity : IEquatable<KnownOperationTypeIdentity>, IComparable<KnownOperationTypeIdentity>
{
    private KnownOperationTypeIdentity(
        KnownOperationTypeIdentityKind kind,
        string namespaceName,
        string metadataName,
        int ordinal,
        int arrayRank,
        KnownOperationTypeIdentity? elementType)
    {
        Kind = kind;
        NamespaceName = namespaceName;
        MetadataName = metadataName;
        Ordinal = ordinal;
        ArrayRank = arrayRank;
        ElementType = elementType;
        SortKey = CreateSortKey(kind, namespaceName, metadataName, ordinal, arrayRank, elementType);
    }

    internal KnownOperationTypeIdentityKind Kind
    {
        get;
    }

    internal string NamespaceName
    {
        get;
    }

    internal string MetadataName
    {
        get;
    }

    internal int Ordinal
    {
        get;
    }

    internal int ArrayRank
    {
        get;
    }

    internal KnownOperationTypeIdentity? ElementType
    {
        get;
    }

    internal string SortKey
    {
        get;
    }

    internal static KnownOperationTypeIdentity Named(string namespaceName, string metadataName)
    {
        ValidateText(namespaceName, nameof(namespaceName), allowEmpty: true);
        ValidateText(metadataName, nameof(metadataName), allowEmpty: false);

        return new KnownOperationTypeIdentity(
            KnownOperationTypeIdentityKind.Named,
            namespaceName,
            metadataName,
            ordinal: -1,
            arrayRank: 0,
            elementType: null);
    }

    internal static KnownOperationTypeIdentity TypeParameter(int ordinal)
    {
        return ordinal < 0
            ? throw new ArgumentOutOfRangeException(nameof(ordinal), "Type parameter ordinal must be non-negative.")
            : new KnownOperationTypeIdentity(
                KnownOperationTypeIdentityKind.TypeParameter,
                namespaceName: string.Empty,
                metadataName: string.Empty,
                ordinal,
                arrayRank: 0,
                elementType: null);
    }

    internal static KnownOperationTypeIdentity Array(KnownOperationTypeIdentity elementType, int rank)
    {
        _ = elementType ?? throw new ArgumentNullException(nameof(elementType));

        return rank <= 0
            ? throw new ArgumentOutOfRangeException(nameof(rank), "Array rank must be positive.")
            : new KnownOperationTypeIdentity(
                KnownOperationTypeIdentityKind.Array,
                namespaceName: string.Empty,
                metadataName: string.Empty,
                ordinal: -1,
                arrayRank: rank,
                elementType);
    }

    public int CompareTo(KnownOperationTypeIdentity? other)
    {
        return other is null
            ? 1
            : StringComparer.Ordinal.Compare(SortKey, other.SortKey);
    }

    public bool Equals(KnownOperationTypeIdentity? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(SortKey, other.SortKey);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as KnownOperationTypeIdentity);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(SortKey);
    }

    public override string ToString()
    {
        return SortKey;
    }

    private static string CreateSortKey(
        KnownOperationTypeIdentityKind kind,
        string namespaceName,
        string metadataName,
        int ordinal,
        int arrayRank,
        KnownOperationTypeIdentity? elementType)
    {
        return kind switch
        {
            KnownOperationTypeIdentityKind.Named => "named:" + namespaceName + "." + metadataName,
            KnownOperationTypeIdentityKind.TypeParameter => "type-parameter:" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            KnownOperationTypeIdentityKind.Array => "array:" + arrayRank.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + elementType!.SortKey,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown type identity kind."),
        };
    }

    private static void ValidateText(string value, string parameterName, bool allowEmpty)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (!allowEmpty && value.Length == 0)
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }
    }
}

internal enum KnownOperationTypeIdentityKind
{
    Named,
    TypeParameter,
    Array,
}

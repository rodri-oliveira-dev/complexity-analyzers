using System;
using System.Collections.Immutable;
using System.Linq;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal sealed class KnownOperationIdentity : IEquatable<KnownOperationIdentity>, IComparable<KnownOperationIdentity>
{
    private KnownOperationIdentity(
        KnownOperationReceiverKind receiverKind,
        KnownOperationTypeIdentity containingType,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes)
    {
        ReceiverKind = receiverKind;
        ContainingType = containingType;
        MethodMetadataName = methodMetadataName;
        MethodArity = methodArity;
        ParameterTypes = parameterTypes;
        SortKey = CreateSortKey(receiverKind, containingType, methodMetadataName, methodArity, parameterTypes);
    }

    internal KnownOperationReceiverKind ReceiverKind
    {
        get;
    }

    internal KnownOperationTypeIdentity ContainingType
    {
        get;
    }

    internal string MethodMetadataName
    {
        get;
    }

    internal int MethodArity
    {
        get;
    }

    internal ImmutableArray<KnownOperationTypeIdentity> ParameterTypes
    {
        get;
    }

    internal string SortKey
    {
        get;
    }

    internal static KnownOperationIdentity Create(
        KnownOperationReceiverKind receiverKind,
        KnownOperationTypeIdentity containingType,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes)
    {
        _ = containingType ?? throw new ArgumentNullException(nameof(containingType));

        if (methodMetadataName is null)
        {
            throw new ArgumentNullException(nameof(methodMetadataName));
        }

        if (methodMetadataName.Length == 0)
        {
            throw new ArgumentException("Method metadata name must not be empty.", nameof(methodMetadataName));
        }

        if (methodArity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(methodArity), "Method arity must be non-negative.");
        }

        if (parameterTypes.IsDefault)
        {
            throw new ArgumentException("Parameter type collection must be initialized.", nameof(parameterTypes));
        }

        foreach (KnownOperationTypeIdentity parameterType in parameterTypes)
        {
            _ = parameterType ?? throw new ArgumentException("Parameter type collection must not contain null values.", nameof(parameterTypes));
        }

        return new KnownOperationIdentity(
            receiverKind,
            containingType,
            methodMetadataName,
            methodArity,
            parameterTypes);
    }

    public int CompareTo(KnownOperationIdentity? other)
    {
        return other is null
            ? 1
            : StringComparer.Ordinal.Compare(SortKey, other.SortKey);
    }

    public bool Equals(KnownOperationIdentity? other)
    {
        return other is not null && StringComparer.Ordinal.Equals(SortKey, other.SortKey);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as KnownOperationIdentity);
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
        KnownOperationReceiverKind receiverKind,
        KnownOperationTypeIdentity containingType,
        string methodMetadataName,
        int methodArity,
        ImmutableArray<KnownOperationTypeIdentity> parameterTypes)
    {
        string parameters = string.Join(
            ",",
            parameterTypes.Select(parameterType => parameterType.SortKey));

        return receiverKind.ToString()
            + "|"
            + containingType.SortKey
            + "|"
            + methodMetadataName
            + "`"
            + methodArity.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "("
            + parameters
            + ")";
    }
}

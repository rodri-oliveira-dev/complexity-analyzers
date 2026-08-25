using System;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal sealed class KnownOperationMetadata : IEquatable<KnownOperationMetadata>
{
    internal KnownOperationMetadata(
        string operationFamily,
        bool enumeratesReceiver,
        bool materializes,
        bool orders,
        bool isLookupOperation)
    {
        if (operationFamily is null)
        {
            throw new ArgumentNullException(nameof(operationFamily));
        }

        if (operationFamily.Length == 0)
        {
            throw new ArgumentException("Operation family must not be empty.", nameof(operationFamily));
        }

        OperationFamily = operationFamily;
        EnumeratesReceiver = enumeratesReceiver;
        Materializes = materializes;
        Orders = orders;
        IsLookupOperation = isLookupOperation;
    }

    internal string OperationFamily
    {
        get;
    }

    internal bool EnumeratesReceiver
    {
        get;
    }

    internal bool Materializes
    {
        get;
    }

    internal bool Orders
    {
        get;
    }

    internal bool IsLookupOperation
    {
        get;
    }

    public bool Equals(KnownOperationMetadata? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(OperationFamily, other.OperationFamily)
            && EnumeratesReceiver == other.EnumeratesReceiver
            && Materializes == other.Materializes
            && Orders == other.Orders
            && IsLookupOperation == other.IsLookupOperation;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as KnownOperationMetadata);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(OperationFamily);
            hash = (hash * 397) ^ EnumeratesReceiver.GetHashCode();
            hash = (hash * 397) ^ Materializes.GetHashCode();
            hash = (hash * 397) ^ Orders.GetHashCode();
            hash = (hash * 397) ^ IsLookupOperation.GetHashCode();
            return hash;
        }
    }
}

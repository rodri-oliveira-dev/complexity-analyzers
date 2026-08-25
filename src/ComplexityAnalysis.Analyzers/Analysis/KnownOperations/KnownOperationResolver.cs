using System;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

internal sealed class KnownOperationResolver
{
    private readonly KnownOperationRegistry registry;

    internal KnownOperationResolver(KnownOperationRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    internal bool TryResolve(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken,
        out KnownOperationMapping mapping)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        KnownOperationIdentity identity = CreateIdentity(methodSymbol, cancellationToken);
        return registry.TryGetMapping(identity, out mapping);
    }

    internal bool TryResolve(
        IPropertySymbol propertySymbol,
        CancellationToken cancellationToken,
        out KnownOperationMapping mapping)
    {
        _ = propertySymbol ?? throw new ArgumentNullException(nameof(propertySymbol));

        cancellationToken.ThrowIfCancellationRequested();

        if (propertySymbol.GetMethod is null)
        {
            mapping = null!;
            return false;
        }

        return registry.TryGetMapping(CreateIdentity(propertySymbol, cancellationToken), out mapping);
    }

    internal static KnownOperationIdentity CreateIdentity(
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        KnownOperationReceiverKind receiverKind = GetReceiverKind(methodSymbol);
        IMethodSymbol definitionSymbol = GetDefinitionSymbol(methodSymbol, receiverKind);

        cancellationToken.ThrowIfCancellationRequested();

        INamedTypeSymbol containingType = definitionSymbol.ContainingType?.OriginalDefinition
            ?? throw new ArgumentException("Method symbol must have a containing type.", nameof(methodSymbol));

        ImmutableArray<KnownOperationTypeIdentity> parameterTypes = CreateParameterTypes(
            definitionSymbol.Parameters,
            cancellationToken);

        return KnownOperationIdentity.Create(
            receiverKind,
            CreateNamedTypeIdentity(containingType),
            definitionSymbol.MetadataName,
            definitionSymbol.Arity,
            parameterTypes);
    }

    internal static KnownOperationIdentity CreateIdentity(
        IPropertySymbol propertySymbol,
        CancellationToken cancellationToken)
    {
        _ = propertySymbol ?? throw new ArgumentNullException(nameof(propertySymbol));

        cancellationToken.ThrowIfCancellationRequested();

        IMethodSymbol accessor = propertySymbol.GetMethod
            ?? throw new ArgumentException("Property symbol must have a getter.", nameof(propertySymbol));

        return CreateIdentity(accessor, cancellationToken);
    }

    private static KnownOperationReceiverKind GetReceiverKind(IMethodSymbol methodSymbol)
    {
        return methodSymbol.ReducedFrom is not null
            ? KnownOperationReceiverKind.ReducedExtension
            : methodSymbol.IsStatic
                ? KnownOperationReceiverKind.Static
                : KnownOperationReceiverKind.Instance;
    }

    private static IMethodSymbol GetDefinitionSymbol(
        IMethodSymbol methodSymbol,
        KnownOperationReceiverKind receiverKind)
    {
        IMethodSymbol symbol = receiverKind == KnownOperationReceiverKind.ReducedExtension
            ? methodSymbol.ReducedFrom!
            : methodSymbol;

        IMethodSymbol originalDefinition = symbol.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(symbol, originalDefinition)
            ? symbol
            : originalDefinition;
    }

    private static ImmutableArray<KnownOperationTypeIdentity> CreateParameterTypes(
        ImmutableArray<IParameterSymbol> parameters,
        CancellationToken cancellationToken)
    {
        ImmutableArray<KnownOperationTypeIdentity>.Builder parameterTypes =
            ImmutableArray.CreateBuilder<KnownOperationTypeIdentity>(parameters.Length);

        foreach (IParameterSymbol parameter in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            parameterTypes.Add(CreateTypeIdentity(parameter.Type));
        }

        return parameterTypes.ToImmutable();
    }

    private static KnownOperationTypeIdentity CreateTypeIdentity(ITypeSymbol type)
    {
        return type switch
        {
            IArrayTypeSymbol arrayType => KnownOperationTypeIdentity.Array(
                CreateTypeIdentity(arrayType.ElementType),
                arrayType.Rank),
            ITypeParameterSymbol typeParameter => KnownOperationTypeIdentity.TypeParameter(typeParameter.Ordinal),
            INamedTypeSymbol namedType => CreateNamedTypeIdentity(namedType.OriginalDefinition),
            _ => KnownOperationTypeIdentity.Named(
                GetNamespaceName(type.ContainingNamespace),
                type.MetadataName),
        };
    }

    private static KnownOperationTypeIdentity CreateNamedTypeIdentity(INamedTypeSymbol type)
    {
        return KnownOperationTypeIdentity.Named(
            GetNamespaceName(type.ContainingNamespace),
            type.MetadataName);
    }

    private static string GetNamespaceName(INamespaceSymbol? namespaceSymbol)
    {
        return namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace
            ? string.Empty
            : namespaceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }
}

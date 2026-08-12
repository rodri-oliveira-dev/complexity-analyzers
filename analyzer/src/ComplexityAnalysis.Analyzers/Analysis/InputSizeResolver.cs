using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

using ComplexityAnalysis.Analyzers.Model;

using Microsoft.CodeAnalysis;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class InputSizeResolver
{
    private readonly CancellationToken cancellationToken;

    internal InputSizeResolver(SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        this.cancellationToken = cancellationToken;
    }

    internal ImmutableDictionary<ISymbol, ComplexityVariable> ResolveParameterVariables(IMethodSymbol methodSymbol)
    {
        _ = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));

        cancellationToken.ThrowIfCancellationRequested();

        ImmutableDictionary<ISymbol, ComplexityVariable>.Builder variables =
            ImmutableDictionary.CreateBuilder<ISymbol, ComplexityVariable>(SymbolEqualityComparer.Default);

        int variableIndex = 0;
        foreach (IParameterSymbol parameter in methodSymbol.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsEligibleInputSizeParameter(parameter))
            {
                continue;
            }

            variables[parameter] = CreateCanonicalVariable(variableIndex);
            variableIndex++;
        }

        return variables.ToImmutable();
    }

    internal bool IsEligibleInputSizeParameter(IParameterSymbol parameter)
    {
        _ = parameter ?? throw new ArgumentNullException(nameof(parameter));

        cancellationToken.ThrowIfCancellationRequested();

        ITypeSymbol type = parameter.Type;

        return !IsCancellationToken(type)
            && type.TypeKind != TypeKind.Enum
            && type.TypeKind != TypeKind.Delegate
            && (type.TypeKind == TypeKind.Array
            || type.SpecialType == SpecialType.System_String
            || IsIntegralBoundType(type)
            || IsCollectionOrEnumerable(type));
    }

    private static ComplexityVariable CreateCanonicalVariable(int variableIndex)
    {
        string name = variableIndex switch
        {
            0 => "n",
            1 => "m",
            2 => "k",
            3 => "p",
            _ => "v" + (variableIndex + 1).ToString(CultureInfo.InvariantCulture),
        };

        return new ComplexityVariable(name);
    }

    private static bool IsIntegralBoundType(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64;
    }

    private bool IsCollectionOrEnumerable(ITypeSymbol type)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (type is INamedTypeSymbol namedType && IsKnownCollectionOrEnumerableInterface(namedType))
        {
            return true;
        }

        foreach (INamedTypeSymbol @interface in type.AllInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsKnownCollectionOrEnumerableInterface(@interface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKnownCollectionOrEnumerableInterface(INamedTypeSymbol type)
    {
        INamedTypeSymbol definition = type.OriginalDefinition;

        return HasMetadataName(definition, "System.Collections", "IEnumerable")
            || HasMetadataName(definition, "System.Collections", "ICollection")
            || HasMetadataName(definition, "System.Collections.Generic", "IEnumerable`1")
            || HasMetadataName(definition, "System.Collections.Generic", "ICollection`1")
            || HasMetadataName(definition, "System.Collections.Generic", "IReadOnlyCollection`1")
            || HasMetadataName(definition, "System.Collections.Generic", "IList`1")
            || HasMetadataName(definition, "System.Collections.Generic", "IReadOnlyList`1");
    }

    private static bool IsCancellationToken(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
            && HasMetadataName(namedType, "System.Threading", "CancellationToken");
    }

    private static bool HasMetadataName(INamedTypeSymbol type, string namespaceName, string metadataName)
    {
        return StringComparer.Ordinal.Equals(type.MetadataName, metadataName)
            && StringComparer.Ordinal.Equals(GetNamespaceName(type.ContainingNamespace), namespaceName);
    }

    private static string GetNamespaceName(INamespaceSymbol namespaceSymbol)
    {
        return namespaceSymbol.IsGlobalNamespace
            ? string.Empty
            : namespaceSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }
}

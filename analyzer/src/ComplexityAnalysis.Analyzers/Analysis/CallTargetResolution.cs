using System;

using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Analyzers.Analysis;

internal sealed class CallTargetResolution
{
    private CallTargetResolution(
        CallTargetResolutionKind kind,
        IMethodSymbol? targetMethodSymbol,
        IMethodSymbol? sourceMethodDefinition,
        MethodDeclarationSyntax? sourceMethodDeclaration,
        KnownOperationMapping? knownOperationMapping)
    {
        Kind = kind;
        TargetMethodSymbol = targetMethodSymbol;
        SourceMethodDefinition = sourceMethodDefinition;
        SourceMethodDeclaration = sourceMethodDeclaration;
        KnownOperationMapping = knownOperationMapping;
    }

    internal CallTargetResolutionKind Kind
    {
        get;
    }

    internal IMethodSymbol? TargetMethodSymbol
    {
        get;
    }

    internal IMethodSymbol? SourceMethodDefinition
    {
        get;
    }

    internal MethodDeclarationSyntax? SourceMethodDeclaration
    {
        get;
    }

    internal KnownOperationMapping? KnownOperationMapping
    {
        get;
    }

    internal static CallTargetResolution KnownOperation(
        IMethodSymbol targetMethodSymbol,
        KnownOperationMapping mapping)
    {
        _ = targetMethodSymbol ?? throw new ArgumentNullException(nameof(targetMethodSymbol));
        _ = mapping ?? throw new ArgumentNullException(nameof(mapping));

        return new CallTargetResolution(
            CallTargetResolutionKind.KnownOperation,
            targetMethodSymbol,
            null,
            null,
            mapping);
    }

    internal static CallTargetResolution SourceMethod(
        IMethodSymbol targetMethodSymbol,
        IMethodSymbol sourceMethodDefinition,
        MethodDeclarationSyntax sourceMethodDeclaration)
    {
        _ = targetMethodSymbol ?? throw new ArgumentNullException(nameof(targetMethodSymbol));
        _ = sourceMethodDefinition ?? throw new ArgumentNullException(nameof(sourceMethodDefinition));
        _ = sourceMethodDeclaration ?? throw new ArgumentNullException(nameof(sourceMethodDeclaration));

        return new CallTargetResolution(
            CallTargetResolutionKind.SourceMethod,
            targetMethodSymbol,
            sourceMethodDefinition,
            sourceMethodDeclaration,
            null);
    }

    internal static CallTargetResolution Unsupported()
    {
        return new CallTargetResolution(
            CallTargetResolutionKind.Unsupported,
            null,
            null,
            null,
            null);
    }
}

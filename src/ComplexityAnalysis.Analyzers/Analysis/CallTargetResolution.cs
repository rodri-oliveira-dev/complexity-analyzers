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
        ExecutableMember? sourceMember,
        KnownOperationMapping? knownOperationMapping)
    {
        Kind = kind;
        TargetMethodSymbol = targetMethodSymbol;
        SourceMethodDefinition = sourceMethodDefinition;
        SourceMember = sourceMember;
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
        => SourceMember?.Declaration as MethodDeclarationSyntax;

    internal ExecutableMember? SourceMember
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
        ExecutableMember sourceMember)
    {
        _ = targetMethodSymbol ?? throw new ArgumentNullException(nameof(targetMethodSymbol));
        _ = sourceMethodDefinition ?? throw new ArgumentNullException(nameof(sourceMethodDefinition));
        _ = sourceMember ?? throw new ArgumentNullException(nameof(sourceMember));

        return new CallTargetResolution(
            CallTargetResolutionKind.SourceMethod,
            targetMethodSymbol,
            sourceMethodDefinition,
            sourceMember,
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

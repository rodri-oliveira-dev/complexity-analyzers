using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Analyzers.Analysis.KnownOperations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace ComplexityAnalysis.Analyzers.Tests;

public sealed class SourceMethodResolverTests
{
    [Fact]
    public void Static_source_method_resolves_as_source_method()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public static class Helpers
            {
                public static void Visit()
                {
                }
            }

            public sealed class Sample
            {
                void M()
                {
                    Helpers.Visit();
                }
            }
            """);
        IMethodSymbol expected = GetDeclaredMethodSymbol(facts, method => method.IsStatic);

        AssertSourceMethod(facts, expected);
    }

    [Fact]
    public void Private_source_method_resolves_as_source_method()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public sealed class Sample
            {
                void M()
                {
                    Visit(1);
                }

                private void Visit(int value)
                {
                }
            }
            """);
        IMethodSymbol expected = GetDeclaredMethodSymbol(
            facts,
            method => method.DeclaredAccessibility == Accessibility.Private
                && method.Parameters.Length == 1
                && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32);

        AssertSourceMethod(facts, expected);
    }

    [Fact]
    public void Ordinary_nonvirtual_source_method_resolves_as_source_method()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public class Worker
            {
                public void Visit()
                {
                }
            }

            public sealed class Sample
            {
                void M(Worker worker)
                {
                    worker.Visit();
                }
            }
            """);
        IMethodSymbol expected = GetDeclaredMethodSymbol(
            facts,
            method => method.DeclaredAccessibility == Accessibility.Public
                && method.Parameters.Length == 0
                && !method.IsVirtual
                && !method.IsOverride);

        AssertSourceMethod(facts, expected);
    }

    [Fact]
    public void Sealed_runtime_target_resolves_as_source_method()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public abstract class WorkerBase
            {
                public abstract void Visit();
            }

            public sealed class Worker : WorkerBase
            {
                public override void Visit()
                {
                }
            }

            public sealed class Sample
            {
                void M(Worker worker)
                {
                    worker.Visit();
                }
            }
            """);
        IMethodSymbol expected = GetDeclaredMethodSymbol(
            facts,
            method => method.IsOverride);

        AssertSourceMethod(facts, expected);
    }

    [Fact]
    public void Overloaded_source_methods_are_distinguished_by_resolved_symbol()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public sealed class Worker
            {
                public void Visit(int value)
                {
                }

                public void Visit(string value)
                {
                }
            }

            public sealed class Sample
            {
                void M(Worker worker)
                {
                    worker.Visit("value");
                }
            }
            """);
        IMethodSymbol expected = GetDeclaredMethodSymbol(
            facts,
            method => method.Parameters.Length == 1
                && method.Parameters[0].Type.SpecialType == SpecialType.System_String);

        AssertSourceMethod(facts, expected);
    }

    [Fact]
    public void Generic_source_method_uses_original_definition_for_source_identity()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public static class Helpers
            {
                public static T Echo<T>(T value) => value;
            }

            public sealed class Sample
            {
                int M(int value) => Helpers.Echo(value);
            }
            """);
        IMethodSymbol expected = GetDeclaredMethodSymbol(
            facts,
            method => method.IsGenericMethod && method.TypeParameters.Length == 1);

        AssertSourceMethod(facts, expected);
        IMethodSymbol targetMethodSymbol = facts.Resolution.TargetMethodSymbol
            ?? throw new InvalidOperationException("Expected target method symbol.");
        Assert.Equal(SpecialType.System_Int32, targetMethodSymbol.TypeArguments.Single().SpecialType);
    }

    [Fact]
    public void Bcl_known_operation_keeps_precedence_over_source_method_resolution()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            using System.Collections.Generic;

            public sealed class Sample
            {
                bool M(List<int> values) => values.Contains(1);
            }
            """);

        AssertKnownOperation(facts);
    }

    [Fact]
    public void Linq_known_operation_keeps_precedence_over_source_method_resolution()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Sample
            {
                int M(IEnumerable<int> values) => values.Count();
            }
            """);

        AssertKnownOperation(facts);
    }

    [Fact]
    public void External_unknown_method_resolves_as_unsupported()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public sealed class Sample
            {
                void M()
                {
                    System.Console.WriteLine("value");
                }
            }
            """);

        AssertUnsupported(facts.Resolution);
    }

    [Fact]
    public void Custom_source_method_with_bcl_like_name_is_not_known_operation()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public sealed class CustomCollection
            {
                public bool Contains(int value) => true;
            }

            public sealed class Sample
            {
                bool M(CustomCollection values) => values.Contains(1);
            }
            """);
        IMethodSymbol expected = GetDeclaredMethodSymbol(
            facts,
            method => method.DeclaredAccessibility == Accessibility.Public
                && method.Parameters.Length == 1
                && method.ReturnType.SpecialType == SpecialType.System_Boolean);

        AssertSourceMethod(facts, expected);
    }

    [Fact]
    public void Interface_dispatch_resolves_as_unsupported()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public interface IWorker
            {
                void Visit();
            }

            public sealed class Worker : IWorker
            {
                public void Visit()
                {
                }
            }

            public sealed class Sample
            {
                void M(IWorker worker)
                {
                    worker.Visit();
                }
            }
            """);

        AssertUnsupported(facts.Resolution);
    }

    [Fact]
    public void Virtual_dispatch_resolves_as_unsupported()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public class Worker
            {
                public virtual void Visit()
                {
                }
            }

            public sealed class Sample
            {
                void M(Worker worker)
                {
                    worker.Visit();
                }
            }
            """);

        AssertUnsupported(facts.Resolution);
    }

    [Fact]
    public void Abstract_target_resolves_as_unsupported()
    {
        ResolutionFacts facts = ResolveSingleInvocation(
            """
            public abstract class Worker
            {
                public abstract void Visit();
            }

            public sealed class Sample
            {
                void M(Worker worker)
                {
                    worker.Visit();
                }
            }
            """);

        AssertUnsupported(facts.Resolution);
    }

    private static void AssertSourceMethod(
        ResolutionFacts facts,
        IMethodSymbol expected)
    {
        Assert.Equal(CallTargetResolutionKind.SourceMethod, facts.Resolution.Kind);
        Assert.NotNull(facts.Resolution.TargetMethodSymbol);
        Assert.NotNull(facts.Resolution.SourceMethodDefinition);
        Assert.NotNull(facts.Resolution.SourceMethodDeclaration);
        Assert.Null(facts.Resolution.KnownOperationMapping);
        Assert.True(SymbolEqualityComparer.Default.Equals(
            expected.OriginalDefinition,
            facts.Resolution.SourceMethodDefinition));

        MethodDeclarationSyntax sourceMethodDeclaration = facts.Resolution.SourceMethodDeclaration
            ?? throw new InvalidOperationException("Expected source method declaration.");
        IMethodSymbol declarationSymbol = facts.SemanticModel.GetDeclaredSymbol(
            sourceMethodDeclaration,
            CancellationToken.None)
            ?? throw new InvalidOperationException("Expected source declaration to bind to a method symbol.");
        Assert.True(SymbolEqualityComparer.Default.Equals(
            expected.OriginalDefinition,
            declarationSymbol.OriginalDefinition));
    }

    private static void AssertKnownOperation(ResolutionFacts facts)
    {
        Assert.Equal(CallTargetResolutionKind.KnownOperation, facts.Resolution.Kind);
        Assert.NotNull(facts.Resolution.TargetMethodSymbol);
        Assert.NotNull(facts.Resolution.KnownOperationMapping);
        Assert.Null(facts.Resolution.SourceMethodDefinition);
        Assert.Null(facts.Resolution.SourceMethodDeclaration);

        IMethodSymbol targetMethodSymbol = facts.Resolution.TargetMethodSymbol
            ?? throw new InvalidOperationException("Expected target method symbol.");
        KnownOperationResolver resolver = new(KnownOperationRegistry.Default);
        Assert.True(resolver.TryResolve(
            targetMethodSymbol,
            CancellationToken.None,
            out KnownOperationMapping expected));
        Assert.Equal(expected, facts.Resolution.KnownOperationMapping);
    }

    private static void AssertUnsupported(CallTargetResolution resolution)
    {
        Assert.Equal(CallTargetResolutionKind.Unsupported, resolution.Kind);
        Assert.Null(resolution.TargetMethodSymbol);
        Assert.Null(resolution.SourceMethodDefinition);
        Assert.Null(resolution.SourceMethodDeclaration);
        Assert.Null(resolution.KnownOperationMapping);
    }

    private static IMethodSymbol GetDeclaredMethodSymbol(
        ResolutionFacts facts,
        Func<IMethodSymbol, bool> predicate)
    {
        return facts.SyntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(method => facts.SemanticModel.GetDeclaredSymbol(method, CancellationToken.None))
            .Where(method => method is not null)
            .Cast<IMethodSymbol>()
            .Single(predicate);
    }

    private static ResolutionFacts ResolveSingleInvocation(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "SourceMethodResolverTests",
            syntaxTrees: [syntaxTree],
            references: BasicReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics(CancellationToken.None)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];
        Assert.Empty(errors);

        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        InvocationExpressionSyntax invocation = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single();
        CallTargetResolution resolution = new SourceMethodResolver().Resolve(
            invocation,
            semanticModel,
            CancellationToken.None);

        return new ResolutionFacts(
            syntaxTree,
            semanticModel,
            invocation,
            resolution);
    }

    private static ImmutableArray<MetadataReference> BasicReferences
    {
        get;
    } = CreateTrustedPlatformReferences();

    private static ImmutableArray<MetadataReference> CreateTrustedPlatformReferences()
    {
        string trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? string.Empty;

        return
        [
            .. trustedPlatformAssemblies.Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    private sealed record ResolutionFacts(
        SyntaxTree SyntaxTree,
        SemanticModel SemanticModel,
        InvocationExpressionSyntax Invocation,
        CallTargetResolution Resolution);
}

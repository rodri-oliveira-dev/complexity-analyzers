using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using ComplexityAnalysis.Analyzers.Analysis;
using ComplexityAnalysis.Tool.Project;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ComplexityAnalysis.Tool.Duplicates;

internal static class CSharpDuplicateTokenNormalizer
{
    internal static IReadOnlyList<NormalizedToken> Normalize(
        ExecutableMember member,
        SemanticModel semanticModel,
        string projectPath,
        string filePath,
        int streamId,
        CancellationToken cancellationToken)
    {
        _ = member ?? throw new ArgumentNullException(nameof(member));
        _ = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));

        Dictionary<string, string> parameterNames = CreateParameterMap(member);
        Dictionary<string, string> localNames = [];
        List<NormalizedToken> tokens = [];

        foreach (SyntaxToken token in ExecutableMemberSyntax.DescendantTokensInOwnBody(member))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (token.IsMissing)
            {
                continue;
            }

            string value = NormalizeToken(token, semanticModel, parameterNames, localNames, cancellationToken);
            FileLinePositionSpan lineSpan = token.SyntaxTree!.GetLineSpan(token.Span, cancellationToken);
            tokens.Add(new NormalizedToken(
                value,
                projectPath,
                filePath,
                streamId,
                tokens.Count,
                token.SpanStart,
                token.Span.End,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1,
                lineSpan.EndLinePosition.Line + 1,
                lineSpan.EndLinePosition.Character + 1));
        }

        return tokens;
    }

    private static Dictionary<string, string> CreateParameterMap(ExecutableMember member)
    {
        List<ParameterSyntax> parameters =
        [
            .. member.Declaration
                .DescendantNodes(descendIntoChildren: node => node == member.Declaration || node is not LocalFunctionStatementSyntax and not AnonymousFunctionExpressionSyntax)
                .OfType<ParameterSyntax>()
        ];
        Dictionary<string, string> names = new(StringComparer.Ordinal);
        int index = 0;
        foreach (ParameterSyntax parameter in parameters)
        {
            string name = parameter.Identifier.ValueText;
            if (name.Length > 0 && !names.ContainsKey(name))
            {
                names.Add(name, "parameter:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                index++;
            }
        }

        return names;
    }

    private static string NormalizeToken(
        SyntaxToken token,
        SemanticModel semanticModel,
        Dictionary<string, string> parameterNames,
        Dictionary<string, string> localNames,
        CancellationToken cancellationToken)
    {
        SyntaxKind kind = token.Kind();
        if (kind == SyntaxKind.IdentifierToken)
        {
            return NormalizeIdentifier(token, semanticModel, parameterNames, localNames, cancellationToken);
        }

        if (kind is SyntaxKind.NumericLiteralToken)
        {
            return "literal:number";
        }

        if (kind is SyntaxKind.StringLiteralToken or SyntaxKind.Utf8StringLiteralToken or SyntaxKind.InterpolatedStringTextToken)
        {
            return "literal:string";
        }

        if (kind is SyntaxKind.CharacterLiteralToken)
        {
            return "literal:char";
        }

        if (kind is SyntaxKind.TrueKeyword)
        {
            return "literal:bool:true";
        }

        if (kind is SyntaxKind.FalseKeyword)
        {
            return "literal:bool:false";
        }

        if (kind is SyntaxKind.NullKeyword)
        {
            return "literal:null";
        }

        return "token:" + kind;
    }

    private static string NormalizeIdentifier(
        SyntaxToken token,
        SemanticModel semanticModel,
        Dictionary<string, string> parameterNames,
        Dictionary<string, string> localNames,
        CancellationToken cancellationToken)
    {
        string name = token.ValueText;
        if (parameterNames.TryGetValue(name, out string? parameterName))
        {
            return parameterName;
        }

        if (IsLocalDeclaration(token) || IsPatternVariable(token))
        {
            if (!localNames.TryGetValue(name, out string? localName))
            {
                localName = "local:" + localNames.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                localNames.Add(name, localName);
            }

            return localName;
        }

        if (localNames.TryGetValue(name, out string? existingLocalName))
        {
            return existingLocalName;
        }

        SymbolInfo symbolInfo = token.Parent is SyntaxNode parent
            ? semanticModel.GetSymbolInfo(parent, cancellationToken)
            : default;
        return symbolInfo.Symbol switch
        {
            ILocalSymbol => "local:ref",
            IParameterSymbol => "parameter:ref",
            IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol => "member:" + IdentifierRole(token),
            INamedTypeSymbol or ITypeParameterSymbol => "type:" + IdentifierRole(token),
            INamespaceSymbol => "namespace",
            _ => "identifier:" + IdentifierRole(token),
        };
    }

    private static string IdentifierRole(SyntaxToken token)
    {
        return token.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name.DescendantTokens().Contains(token) => "memberAccessName",
            GenericNameSyntax => "genericName",
            NameSyntax => "name",
            TypeSyntax => "typeName",
            LabeledStatementSyntax => "label",
            GotoStatementSyntax => "label",
            _ => "value",
        };
    }

    private static bool IsLocalDeclaration(SyntaxToken token)
    {
        return token.Parent is VariableDeclaratorSyntax declarator
            && declarator.Identifier == token
            && declarator.Parent?.Parent is LocalDeclarationStatementSyntax or ForStatementSyntax or UsingStatementSyntax;
    }

    private static bool IsPatternVariable(SyntaxToken token)
    {
        return token.Parent is SingleVariableDesignationSyntax designation
            && designation.Identifier == token;
    }
}

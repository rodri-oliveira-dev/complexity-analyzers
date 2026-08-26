# Issue #9 SDD - Executable Member Coverage

## Specification

Issue #9 expands the executable-member abstraction introduced by #8 beyond
ordinary `MethodDeclarationSyntax`. The goal is not to register every possible
syntax node. The goal is to support additional executable C# constructs only
when the analyzer can define semantic identity, body ownership, semantic model,
stable diagnostic location, nested-body boundaries, Big-O applicability, and
safe dispatch behavior.

Unsupported or unproven cases remain `Unknown` or produce no diagnostic.

## Support Matrix

| Member kind | Supported? | Symbol strategy | Body strategy | Diagnostic location | Interprocedural | Recursion |
| --- | --- | --- | --- | --- | --- | --- |
| Ordinary methods | Supported | `GetDeclaredSymbol(MethodDeclarationSyntax)` `IMethodSymbol` | block or expression body | method identifier | Supported by existing safe source-method dispatch | Supported direct recursion |
| Constructors | Supported as roots | `GetDeclaredSymbol(ConstructorDeclarationSyntax)` `IMethodSymbol` | block or expression body | constructor identifier | Deferred as callees; object creation dispatch is not expanded in this issue | Not applicable |
| Static constructors | Supported as roots | `GetDeclaredSymbol(ConstructorDeclarationSyntax)` `IMethodSymbol` | block body | type identifier in static constructor declaration | Not applicable as callees | Not applicable |
| Destructors/finalizers | Intentionally unsupported | Not created | Not analyzed | n/a | n/a | n/a |
| Property getters | Supported as roots | `GetDeclaredSymbol(AccessorDeclarationSyntax)` `IMethodSymbol` | block or expression body | accessor keyword | Deferred as property-access callees | Supported if Roslyn exposes direct accessor recursion safely |
| Property setters | Supported as roots | `GetDeclaredSymbol(AccessorDeclarationSyntax)` `IMethodSymbol` | block or expression body | accessor keyword | Deferred as property-access callees | Supported if direct recursion is explicit and solvable |
| Init accessors | Supported as roots | `GetDeclaredSymbol(AccessorDeclarationSyntax)` `IMethodSymbol` | block or expression body | accessor keyword | Deferred as property-access callees | Supported if direct recursion is explicit and solvable |
| Event add | Supported as roots | `GetDeclaredSymbol(AccessorDeclarationSyntax)` `IMethodSymbol` | block or expression body | accessor keyword | Deferred as event-accessor callees | Supported if direct recursion is explicit and solvable |
| Event remove | Supported as roots | `GetDeclaredSymbol(AccessorDeclarationSyntax)` `IMethodSymbol` | block or expression body | accessor keyword | Deferred as event-accessor callees | Supported if direct recursion is explicit and solvable |
| Operators | Supported as roots | `GetDeclaredSymbol(OperatorDeclarationSyntax)` `IMethodSymbol` | block or expression body | operator keyword | Deferred as operator-call callees | Supported if direct recursion is explicit and solvable |
| Conversion operators | Supported as roots | `GetDeclaredSymbol(ConversionOperatorDeclarationSyntax)` `IMethodSymbol` | block or expression body | implicit/explicit keyword | Deferred as conversion-call callees | Supported if direct recursion is explicit and solvable |
| Local functions | Supported | `GetDeclaredSymbol(LocalFunctionStatementSyntax)` `IMethodSymbol` | block or expression body | local function identifier | Supported for direct local-function invocations through the existing source-call pipeline | Supported direct recursion |
| Simple lambdas | Supported as roots | `IAnonymousFunctionOperation.Symbol` `IMethodSymbol` | block or expression body | lambda arrow token | Deferred as delegate invocation callees | Deferred |
| Parenthesized lambdas | Supported as roots | `IAnonymousFunctionOperation.Symbol` `IMethodSymbol` | block or expression body | lambda arrow token | Deferred as delegate invocation callees | Deferred |
| Anonymous methods | Supported as roots | `IAnonymousFunctionOperation.Symbol` `IMethodSymbol` | block body | `delegate` keyword | Deferred as delegate invocation callees | Deferred |
| Expression-bodied properties/accessors | Supported when accessor syntax exists or property has an expression body | accessor symbol for accessor syntax; property getter symbol for expression-bodied property | expression body | accessor keyword or property identifier | Deferred as property-access callees | Deferred for property expression bodies |
| Expression-bodied constructors/operators/method-like declarations | Supported for the corresponding member kind | declared `IMethodSymbol` | expression body | declaration keyword/operator/member identifier | Same as member kind | Same as member kind |

## Discovery

#8 is present in `main` via merged PR #49, commit `4d24aeb`, and ordinary
methods already flow through `ExecutableMember`. The abstraction currently
stores declaration, `IMethodSymbol`, body, diagnostic location, display name, and
syntax tree. It does not own semantic models, options, caches, root state, or
cancellation, which remains the correct separation for #9.

The current pipeline has two boundary gaps:

- `ActionableComplexityDiagnosticAnalyzer` scans `member.Declaration`
  descendants, which can cross into nested executable bodies.
- `MethodComplexityExtractor` treats local-function declarations as unknown
  parent statements instead of declaration-only executable members.

## Design

Executable-member creation remains concentrated in `ExecutableMember`. The
shared analysis pipeline consumes the normalized member and does not switch over
all Roslyn syntax kinds.

Registration uses one syntax-node callback with the supported root syntax kinds.
Each callback tries to create an `ExecutableMember`; unsupported shapes return
false and produce no diagnostic.

Executable body boundaries are enforced by a reusable syntax helper. When a
member is analyzed, traversal visits that member's own body but skips nested
local functions, lambdas, and anonymous methods unless that nested member is the
registered root currently being analyzed.

Interprocedural dispatch remains conservative. Ordinary source methods keep the
existing safe dispatch rules. Local functions can be followed because a direct
local-function invocation resolves to the local-function `IMethodSymbol` and has
no virtual runtime dispatch ambiguity. Constructors, accessors, operators,
conversions, lambdas, and anonymous methods are roots only in this issue; call
forms that require property access, object creation, operator binding, or
delegate invocation expansion remain deferred.

Direct recursion remains supported only where the existing recurrence extractor
can use the member symbol and body without special cases. Lambda and anonymous
method recursion is deferred because stable self-reference and delegate dispatch
need a separate design.

## Validation Plan

- Focused tests for `ExecutableMember`.
- Analyzer tests for constructors, accessors, operators, conversions, local
  functions, lambdas, anonymous methods, expression-bodied forms, diagnostics
  locations, and double-reporting prevention.
- Regression tests proving ordinary methods keep locations, estimates,
  interprocedural behavior, recursion, and single `BIG9000` probe behavior.
- Release restore/build/test.
- Local pack and package contract/consumer/host compatibility tests.
- Performance budget and performance harness validation.

# Issue #8 SDD - Executable Member Analysis Abstraction

## Specification

### Problem

The current C# analysis pipeline is centered on `MethodDeclarationSyntax`. That is sufficient for ordinary methods, but it mixes the identity of the executable member being analyzed with ordinary-method-specific Roslyn syntax. This makes future coverage for constructors, accessors, operators, conversion operators, local functions, lambdas, anonymous methods, and expression-bodied forms harder to add without duplicating symbol, body, display, location, and semantic-model plumbing.

This issue is an architecture refactor only. It must preserve the public behavior characterized by issues #31 through #35 and must not expand the currently documented supported executable-member set.

### Current Responsibilities

- Register ordinary method declarations as the analyzer entry point.
- Resolve the analyzed member to an `IMethodSymbol`.
- Read tree-specific analyzer options.
- Extract block-bodied and expression-bodied ordinary method bodies.
- Use the method identifier as the diagnostic display name and diagnostic location for method-level diagnostics.
- Create a `MethodAnalysisContext` for semantic analysis, input-size variables, options, interprocedural state, local loop facts, and cancellation.
- Resolve safe source-method callees for interprocedural analysis.
- Cache source-method templates and direct-recursion results by method symbol and effective options.
- Detect and solve direct recursion for supported ordinary methods.
- Report existing diagnostics without changing IDs, severities, enablement, messages, or properties.

### Desired Responsibilities

- Represent "who is being analyzed" with a reusable internal executable-member abstraction.
- Keep "how analysis is executed" in existing analysis contexts, options, budgets, caches, semantic models, and cancellation-aware traversal components.
- Let metric analyzers consume pre-resolved member identity, body, display, and location information without independently resolving the same ordinary-method metadata.
- Preserve block-bodied and expression-bodied ordinary method analysis through the abstraction.
- Leave future member-kind registration and coverage to issue #9.

### Invariants

- No intentional change to Big-O estimates.
- No intentional change to known operation handling, LINQ handling, interprocedural analysis, recursion analysis, recurrence solving, budgets, diagnostics, configuration, package contract, or performance contract.
- `Unknown` remains the conservative result for unsupported or unproven cases.
- Generated code remains excluded.
- Concurrent analyzer execution remains enabled and safe.
- Cancellation remains propagated through non-trivial traversal and semantic-resolution paths.
- Analyzer hot paths do not add filesystem I/O, network I/O, process execution, telemetry, or broad mandatory scans.
- The analyzer project remains `netstandard2.0`, Roslyn 4.8-compatible, package-only under `analyzers/dotnet/cs/`, and free of `Microsoft.CodeAnalysis.Workspaces`.

### Non-Goals

- Do not enable constructors, accessors, operators, conversion operators, local functions, lambdas, or anonymous methods as new analysis entry points.
- Do not add diagnostics, configuration keys, package metadata, dependencies, or public severity/default changes.
- Do not change diagnostic messages, diagnostic properties, or release tracking.
- Do not introduce a single God context that owns syntax, symbols, semantic models, compilation, options, caches, diagnostics, solver services, and cancellation.
- Do not port code from `complexity-hints` or create any dependency on inherited projects.

### Compatibility

The refactor keeps the public analyzer contract unchanged. Existing package/consumer and host-compatibility tests remain the compatibility evidence. Since no dependency, target framework, or package layout change is planned, package validation is used as regression evidence rather than because the package contract is intentionally changed.

### Performance

The abstraction must be cheap to create for the current syntax-node callback. It should store already-needed Roslyn facts and avoid repeated symbol/body/location resolution. It must not expand analysis scope or add compilation-wide scans. Existing per-compilation caches remain owned by `InterproceduralAnalysisContext`, and existing per-root budgets remain owned by `InterproceduralRootAnalysisState`.

### Migration Strategy

1. Discover each current `MethodDeclarationSyntax` dependency and classify its semantic role.
2. Introduce a small executable-member identity/body abstraction for ordinary methods.
3. Add an ordinary-method factory that performs symbol resolution once and returns an unsupported result conservatively when metadata cannot be resolved.
4. Migrate the ordinary-method analyzer entry point, method complexity extraction, actionable diagnostics, interprocedural callee resolution, and direct-recursion extraction to consume the abstraction.
5. Keep compatibility wrappers for tests and internal callers when useful, but make the primary pipeline use the abstraction.
6. Update internal architecture documentation if the affected pipeline docs describe the old method-only shape.

### Test Strategy

- Add focused tests for the new abstraction covering block-bodied and expression-bodied ordinary methods.
- Add tests proving unsupported member kinds are not accidentally represented by the ordinary-method factory in this issue.
- Keep or update regression coverage for existing Big-O, known operation, interprocedural, recursion, budget, generated-code, package, and compatibility contracts.
- Run broad Release restore/build/test plus focused characterization, performance-budget, synthetic performance, package contract, consumer contract, and host compatibility suites as proportional evidence.

## Discovery

### Dependency Matrix

| Current component | `MethodDeclarationSyntax` dependency | Required abstraction |
| --- | --- | --- |
| `ComplexityAnalyzer.InitializeCompilationAnalysis` | `SyntaxKind.MethodDeclaration` is the current Roslyn entry point. | Keep registration method-only for this issue; create an executable member from the callback node before analysis. |
| `ComplexityAnalyzer.AnalyzeMethodDeclaration` | Casts `context.Node`, reads `SyntaxTree`, passes declaration to analysis, reports identifier location/name. | Ordinary-method factory, member `SyntaxTree`, member `DiagnosticLocation`, member `DisplayName`. |
| `ActionableComplexityDiagnosticAnalyzer.AnalyzeMethod` | Resolves `IMethodSymbol`, creates root state/context, scans declaration descendants. | Pre-resolved member `Symbol` and `Declaration`; semantic/options context remains separate. |
| `ActionableComplexityDiagnosticAnalyzer.AnalyzeRecursiveMethod` | Uses method identifier location for `BIG1005`. | Member `DiagnosticLocation` and member `Symbol` for display. |
| `MethodAnalysisContext.Create(MethodDeclarationSyntax, ...)` | Resolves declared method symbol. | Compatibility wrapper over executable member creation; context keeps semantic/options/caches/cancellation. |
| `MethodComplexityExtractor.AnalyzeMethod` | Resolves symbol, creates root state, chooses block body or expression body. | Member `Symbol` plus executable body abstraction. |
| `MethodComplexityExtractor.AnalyzeSourceMethod` | Receives source method declaration and symbol separately. | Receive one source executable member; keep root state/options/cache outside member. |
| `MethodComplexityExtractor.TrySolveDirectRecurrence` | Passes declaration into recurrence extraction. | Member passed into recurrence extraction. |
| `SourceMethodResolver` | Converts `DeclaringSyntaxReferences` to `MethodDeclarationSyntax`. | Current source support remains ordinary-method-only, but returns an executable member for successful source targets. |
| `CallTargetResolution` | Carries nullable source method declaration. | Carry nullable source executable member; keep compatibility accessor for tests/ordinary-method callers. |
| `InterproceduralInvocationAnalyzer` | Uses callee declaration syntax tree for options/semantic model, passes declaration to extractor. | Use source member `SyntaxTree`, `Symbol`, and body. |
| `RecurrenceExtractor` | Resolves symbol and analyzes local work from block/expression body. | Use member `Symbol` and `Body`. |
| `RecursiveCallAnalyzer` | Resolves symbol and summarizes block/expression body. | Use member `Symbol` and `Body`; unsupported missing body remains `Unknown`/unsupported. |
| Interprocedural/direct-recursion caches | No syntax-key dependency; keys are method symbol plus options. | No change; member is not a cache owner or cache key wrapper. |
| Diagnostics | Method-level diagnostics use method identifier location/name. Invocation diagnostics use invocation locations. | Member-level diagnostics use `DiagnosticLocation`/`DisplayName`; invocation diagnostics unchanged. |
| Configuration | Options are read by syntax tree. | Member exposes `SyntaxTree`; options remain in `InterproceduralAnalysisContext`. |

### Responsibility Classification

- `declaration SyntaxNode`: belongs to the executable member identity because body traversal and syntax tree access are tied to the member being analyzed.
- `IMethodSymbol`: belongs to callable identity and cache/recursion identity; it is not an execution context.
- `block body` / `expression body`: belongs to executable member body, separated from broader declaration syntax.
- `diagnostic location` and `display name`: belongs to diagnostic identity for method-level diagnostics.
- `SemanticModel`: does not belong to the member; it is semantic execution context and may be obtained from the current callback or per-compilation cache.
- `Compilation`: does not belong to the member; it is owned by `InterproceduralAnalysisContext`.
- `Analyzer options`, budgets, caches, root state, diagnostics builders, recurrence solvers, and cancellation tokens do not belong to the member; they describe how analysis is executed.

## Design

### Model

The refactor introduces:

- `ExecutableMember`: internal immutable representation of the executable member currently being analyzed.
- `ExecutableMemberBody`: small value object for block-bodied and expression-bodied executable syntax.

For issue #8, `ExecutableMember` supports only ordinary `MethodDeclarationSyntax` creation. Unsupported future member kinds are deliberately not created by this issue.

`ExecutableMember` contains:

- `SyntaxNode Declaration`
- `IMethodSymbol Symbol`
- `ExecutableMemberBody Body`
- `Location DiagnosticLocation`
- `string DisplayName`
- `SyntaxTree SyntaxTree`

It does not contain:

- `SemanticModel`
- `Compilation`
- `ComplexityAnalyzerOptions`
- interprocedural caches
- root traversal state
- diagnostics builders/reporters
- recurrence solvers
- `CancellationToken`

### Pipeline Shape

```text
Roslyn method callback
  -> ExecutableMember.TryCreateOrdinaryMethod(...)
  -> InterproceduralAnalysisContext.GetOptions(member.SyntaxTree)
  -> MethodAnalysisContext.Create(semanticModel, member.Symbol, options, caches/root state, token)
  -> analyzers consume member.Body/member.Symbol/member.DiagnosticLocation
```

### Baseline Validation Before Development

Dependencies were confirmed via GitHub CLI on 2026-08-26:

- #31 closed by merged PR #37.
- #32 closed by merged PR #38.
- #33 closed by merged PR #39.
- #34 closed by merged PR #40.
- #35 closed by merged PR #41.

Local baseline before production-code refactor:

- `dotnet restore .\ComplexityAnalysis.Analyzers.slnx` with SDK `10.0.400` from the user profile: passed.
- `dotnet build .\ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore`: passed with the 15 existing `IDE0046` warnings previously recorded by phase 07.x.
- Focused contracts `AnalyzerCharacterizationBaselineTests|AnalyzerPerformanceBudgetContractTests|PerformanceSyntheticCorpusTests|AnalyzerPackageContractTests|AnalyzerPackageConsumerContractTests|AnalyzerHostCompatibilityContractTests`: passed, 81/81.

## Validation

Post-development validation used SDK `10.0.400` from `C:\Users\rodrigooliveira\.dotnet`.

- `dotnet build .\ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore`: passed with the 15 existing `IDE0046` warnings.
- Focused pipeline validation `ExecutableMemberTests|ComplexityAnalyzerTests|MethodComplexityExtractorTests|SourceMethodResolverTests|RecursiveCallAnalyzerTests|RecurrenceExtractorTests|PhaseFiveInterproceduralContractTests|PhaseSixRecurrenceContractTests`: passed, 265/265.
- `dotnet test .\ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build`: passed, 628/628.
- `dotnet pack .\src\ComplexityAnalysis.Analyzers\ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output .\artifacts\local-packages`: passed.
- `dotnet build .\performance\ComplexityAnalysis.Analyzers.Performance\ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed`: passed; analyzer report included `ComplexityAnalysis.Analyzers.ComplexityAnalyzer` with `<0,001 s` and `<1%` in this local run.
- `git diff --check`: passed.

# Issue #13 SDD - Parameter Count Analysis

## Specification

Parameter Count is a deterministic structural metric for supported executable
members. It answers only:

```text
How many parameters did the author explicitly declare for this executable member?
```

The metric is independent from Big-O, Cyclomatic Complexity, Maximum Nesting
Depth, NLOC, statement count, token count, Cognitive Complexity, and Halstead
Metrics. It does not infer design quality, dependency-injection pressure,
parameter-object suitability, or public API compatibility.

### Counting Convention

Parameter Count counts source-declared parameters. Each declared parameter counts
exactly once.

Counted:

- ordinary method parameters;
- constructor parameters;
- operator and conversion-operator parameters;
- local-function parameters;
- lambda parameters;
- anonymous-method parameters when an explicit parameter list is present;
- extension-method `this` receivers, because they are explicit source
  parameters;
- `params`, `ref`, `in`, `out`, optional/defaulted, and modern parameter
  modifiers as one parameter each;
- indexer parameters for supported accessor roots, because they are explicitly
  declared on the containing indexer signature.

Not counted:

- generic type parameters and generic constraints;
- implicit instance `this`;
- captured variables;
- compiler-generated or synthetic parameters;
- implicit accessor `value` for property `set`/`init` and event `add`/`remove`;
- call-site arguments.

### Supported Member Kinds

Parameter Count follows the issue #9 executable-member support matrix:

| Member kind | Parameter Count |
| --- | --- |
| Ordinary method with a body | Supported |
| Bodyless method declaration | Deferred; not an executable root for this metric |
| Constructor/static constructor | Supported |
| Destructor/finalizer | Unsupported by executable-member abstraction |
| Property getter/setter/init accessor | Supported; ordinary property accessors count `0` |
| Indexer getter/setter/init accessor | Supported as accessor root; explicit indexer parameters count, implicit `value` does not |
| Event add/remove accessor | Supported; implicit `value` counts `0` |
| Operator | Supported |
| Conversion operator | Supported |
| Local function | Supported |
| Simple/parenthesized lambda | Supported |
| Anonymous method | Supported; `delegate { }` counts `0` |
| Expression-bodied property | Supported as getter; counts `0` |
| Primary constructors | Deferred; class/struct/record declarations are not executable-member roots today |

### Source Of Truth

The source of truth is syntax, not `IMethodSymbol.Parameters`.

Syntax directly represents parameters declared by the author and naturally
excludes compiler-generated machinery. Symbols remain useful for executable
member identity, display names, diagnostics, and existing analysis orchestration,
but are not authoritative for this metric because accessor symbols can expose
implicit parameters such as property/event `value`, and compiler/language
lowering can introduce parameters not present in the source declaration.

Indexer accessors are the one controlled cross-node case: the executable root is
the accessor, while the explicitly declared index parameters live on the
containing `IndexerDeclarationSyntax`. The calculator reads that containing
syntax and still excludes setter/init `value`.

### Threshold Semantics

Configuration key:

```ini
complexity_analyzers.maximum_parameters = 3
```

The threshold is opt-in. Missing or invalid configuration produces no
Parameter Count diagnostic. Valid values are non-negative base-10 integers.

Comparison is strict:

| Actual | Maximum | Result |
| --- | --- | --- |
| `actual < maximum` | configured | no diagnostic |
| `actual == maximum` | configured | no diagnostic |
| `actual > maximum` | configured | report |

`0` is valid: parameterless members do not report, members with one or more
declared parameters report.

### Diagnostic

The next available `BIG2xxx` ID after #12 is `BIG2006`.

`BIG2006` reports at the executable member's stable diagnostic location when a
supported member declares more parameters than the configured maximum.

Message:

```text
Member '{member}' declares {actual} parameters, exceeding configured maximum {threshold}
```

Diagnostic properties:

- `parameterCount`;
- `threshold`.

## Discovery

#12 is complete and merged through PR #53 on 2026-08-27. It added independent
size-metric diagnostics `BIG2003`, `BIG2004`, and `BIG2005`, each opt-in through
non-negative integer thresholds and reported from the shared executable-member
pipeline.

#9's current executable-member abstraction supports methods, constructors,
accessors, operators, conversion operators, local functions, lambdas, anonymous
methods, and expression-bodied properties as roots when they have executable
bodies. Destructors and primary constructors are not represented.

`IMethodSymbol.Parameters` matches source-declared count for ordinary methods,
constructors, operators, conversions, and local functions in ordinary cases. It
does not match the chosen source-declared convention for property setters,
init accessors, and event accessors because those symbols expose implicit
`value`. For indexer accessors, symbol parameters can include both index
parameters and setter `value`, while the source convention wants only the
explicit index parameters.

## Design

The implementation adds:

- `ParameterCount`, a dedicated internal value object;
- `ParameterCountCalculator`, a syntax-based calculator;
- `complexity_analyzers.maximum_parameters`;
- `BIG2006` and `parameterCount`;
- focused calculator and diagnostic tests;
- EN/PT-BR analyzer catalog and configuration documentation;
- release tracking in `AnalyzerReleases.Unshipped.md`.

The calculator performs no body traversal, semantic analysis, filesystem I/O,
network I/O, process execution, telemetry, or whole-compilation scan. Work is
bounded by the parameter list length for the current executable member.

The analyzer reports Parameter Count before Big-O extraction, alongside the
other structural thresholds. When the threshold is unset, no calculator work is
performed beyond normal executable-member creation and option lookup.

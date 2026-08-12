# Analyzer Catalog

[English](analyzers.md) | [Português (Brasil)](../pt-BR/analyzers.md)

This page is the canonical user-facing catalog of diagnostics currently exposed by `ComplexityAnalysis.Analyzers`.

The implementation was inventoried from `DiagnosticDescriptor`, `SupportedDiagnostics`, `Diagnostic.Create`, and `BIG` rule IDs in the current code. Through Phase 3, only `BIG9000` exists as a public diagnostic.

Internal analysis capabilities are documented separately from diagnostics. The internal model and Roslyn extraction can derive several asymptotic forms, but product diagnostics that surface those results to developers are not part of Phase 3.

## BIG9000 - Analyzer Execution Probe

| Property | Value |
| --- | --- |
| ID | `BIG9000` |
| Title | `Analyzer execution probe` |
| Category | `Infrastructure` |
| Default severity | `Info` |
| Enabled by default | `false` |
| Message | `ComplexityAnalysis.Analyzers execution probe is active` |
| Description | Reports once per compilation when explicitly enabled to prove the analyzer executed. |
| Introduced | Phase 1 - Analyzer Foundation |

## What It Detects

`BIG9000` detects analyzer execution infrastructure, not application-code behavior.

It proves that the analyzer package was:

- loaded by the compiler or host;
- initialized;
- executed;
- able to emit diagnostics.

The analyzer registers a compilation action and reports the probe at a source location when source is available. Tests cover that it is emitted at most once per compilation when explicitly enabled.

## Why It Matters

The probe is useful when validating packaging, local consumption, CI smoke tests, or editor/compiler integration.

If `BIG9000` appears, it does not mean your code has a problem. It means the execution probe was explicitly enabled and the analyzer successfully ran.

`BIG9000` does not:

- identify inefficient code;
- calculate Big-O;
- inspect loops for a public warning;
- represent a product performance rule;
- indicate a bug in the consumer project.

## Example

Any C# compilation can produce the probe when it is explicitly enabled:

```csharp
public sealed class Sample
{
    public int M() => 42;
}
```

The diagnostic is independent from this method's complexity. It is reported by compilation-level analyzer infrastructure.

## Configuration

Keep the probe disabled:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Enable it for a local smoke test:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = suggestion
```

Make it highly visible in a temporary CI or package-consumption test:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Setting `warning` changes the consumer-configured severity. The analyzer descriptor still defines `BIG9000` as `Info` and disabled by default.

Do not keep `BIG9000` enabled permanently in normal projects unless you intentionally want a recurring infrastructure signal.

## Planned / Not Yet Available

Product diagnostics based on extracted Big-O complexity are planned for a later phase. No future rule IDs are documented as available here because they are not present in the current code.

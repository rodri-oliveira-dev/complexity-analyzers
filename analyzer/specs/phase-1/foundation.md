# Phase 1 — Analyzer Foundation

## Status

Specification for Phase 1, step 1/5. This document is the versioned SDD contract for the Analyzer Foundation delivery.

The current repository already contains an isolated `analyzer/` workspace and a bootstrap project file for `ComplexityAnalysis.Analyzers`. This step does not add analyzer implementation code, Roslyn `DiagnosticAnalyzer` classes, tests, or ports from the inherited implementation.

## Objective

Establish the foundation for an isolated Roslyn Analyzer product that can later report algorithmic complexity diagnostics for C# code while keeping the inherited `complexity-hints` projects intact.

Phase 1 proves the packaging and execution infrastructure only. It does not calculate Big-O complexity.

## Scope

- Define the isolated analyzer project boundary under `analyzer/`.
- Define the future analyzer project location: `analyzer/src/ComplexityAnalysis.Analyzers/`.
- Define the future test project location: `analyzer/tests/ComplexityAnalysis.Analyzers.Tests/`.
- Define target frameworks, dependencies, packaging behavior, and validation expectations.
- Define the infrastructure-only diagnostic probe `BIG9000`.
- Define acceptance criteria for the complete Phase 1 delivery.
- Initialize minimal handoff files for future isolated chats.

## Out of Scope

- Implementing Big-O, recurrence extraction, loop analysis, LINQ analysis, BCL complexity lookup, or any product diagnostic.
- Creating or modifying analyzer implementation classes during specification step 1/5.
- Porting code from `complexity-hints`.
- Adding `DiagnosticAnalyzer` classes during this step.
- Adding `Microsoft.CodeAnalysis.CSharp.Workspaces`.
- Adding `MathNet.Numerics`.
- Adding `ProjectReference`, binary references, local packages, or transitive dependencies to inherited projects.
- Modifying inherited projects outside `analyzer/`.

## Architecture

The new analyzer product lives entirely under `analyzer/`. Inherited projects remain reference implementation only and must not be referenced by project, binary, or package dependency.

Reference implementation projects include, but are not limited to:

- `src/ComplexityAnalysis.Core`
- `src/ComplexityAnalysis.Roslyn`
- `src/ComplexityAnalysis.Solver`
- `src/ComplexityAnalysis.Engine`
- `src/ComplexityAnalysis.Calibration`
- `src/ComplexityAnalysis.IDE`

The analyzer assembly is loaded by Roslyn/compiler hosts as an analyzer package asset. It is not a runtime library for applications being analyzed.

Future code may copy and adapt narrowly scoped algorithms from the inherited implementation only when required by a later spec, with characterization tests and attribution to the source file or class.

## Target Frameworks

The main analyzer project must be:

```text
analyzer/src/ComplexityAnalysis.Analyzers/
```

The analyzer target framework must be:

```text
netstandard2.0
```

Rationale: Roslyn analyzers are loaded by compiler and IDE hosts. `netstandard2.0` preserves broad host compatibility and avoids coupling the analyzer to the runtime target of the consumer application.

The test project must be:

```text
analyzer/tests/ComplexityAnalysis.Analyzers.Tests/
```

The test project may target the modern SDK used by this workspace, preferably:

```text
net10.0
```

Tests are not required to target `netstandard2.0`.

## Dependencies

Initial Roslyn package version:

```text
Microsoft.CodeAnalysis.CSharp = 4.8.0
```

This version is deliberate and must not be automatically upgraded to the latest version.

`Microsoft.CodeAnalysis.Analyzers` must also be present in a stable version compatible with the Roslyn 4.x package family.

Roslyn dependencies used only during analyzer build or development must use:

```xml
PrivateAssets="all"
```

The analyzer must not add these packages during Phase 1:

- `Microsoft.CodeAnalysis.CSharp.Workspaces`
- `Microsoft.CodeAnalysis.Workspaces`
- `MathNet.Numerics`

The analyzer must not add any dependency on inherited projects.

## Diagnostic Probe

Phase 1 must create exactly one infrastructure diagnostic probe:

```text
BIG9000
```

Conceptual name:

```text
Analyzer execution probe
```

Required behavior:

- Severity: `Info`
- Enabled by default: `false`
- Activatable explicitly from a consumer project through `.editorconfig`
- Reports at most one diagnostic per compilation when enabled
- Represents analyzer infrastructure only
- Does not represent a product rule
- Does not calculate Big-O
- Does not inspect loops, LINQ, recursion, or complexity patterns

The probe exists only to prove that the analyzer package is built, packaged, loaded by the compiler, executed, and able to produce a diagnostic.

## Packaging

The project must be designed for distribution as a Roslyn Analyzer NuGet package.

The analyzer assembly must be packed under:

```text
analyzers/dotnet/cs/
```

The package must not treat the analyzer as a conventional runtime library and should avoid placing the analyzer assembly under `lib/netstandard2.0/`.

The package must not expose Roslyn as a transitive dependency of the consumer project.

## Testing Strategy

Phase 1 tests must prove infrastructure behavior, not complexity analysis.

Required test coverage for the completed phase:

- Analyzer type is a valid `DiagnosticAnalyzer`.
- `BIG9000` descriptor exists with ID `BIG9000`.
- `BIG9000` severity is `Info`.
- `BIG9000` is disabled by default.
- No diagnostics are reported when the probe is not enabled.
- At most one `BIG9000` diagnostic is reported per compilation when explicitly enabled.
- A temporary consumer project can consume the packed analyzer and enable `BIG9000` with `.editorconfig`.
- `dotnet build` of the temporary consumer reports `BIG9000`.
- Package contents include the analyzer assembly under `analyzers/dotnet/cs/`.
- Package metadata does not expose Roslyn packages as runtime transitive dependencies.
- No inherited project is modified or referenced.

## Performance Constraints

Analyzer performance is a functional requirement.

Future analyzer implementation must:

- Call `ConfigureGeneratedCodeAnalysis`.
- Enable `EnableConcurrentExecution()` when safe.
- Respect `CancellationToken`.
- Avoid network access.
- Avoid file system I/O.
- Avoid starting processes.
- Avoid heavy reflection.
- Avoid mutable static state.
- Be deterministic.
- Avoid side effects.
- Avoid access to external environment state.

The `BIG9000` probe must do constant, minimal work and must report no more than once per compilation.

## Compatibility Constraints

- Analyzer project target framework remains `netstandard2.0`.
- Test project may use `net10.0`.
- Roslyn starts at `Microsoft.CodeAnalysis.CSharp` 4.8.0.
- Roslyn build/development dependencies use `PrivateAssets="all"`.
- No `ProjectReference` is allowed from the analyzer project.
- No dependency on inherited projects is allowed.
- No Workspaces package is allowed in Phase 1.
- No `MathNet.Numerics` package is allowed in Phase 1.
- Generated code handling must be explicit.
- The analyzer must remain compatible with compiler/IDE hosts that load analyzer assemblies from NuGet analyzer assets.

## Acceptance Criteria

- AC01 — Project `ComplexityAnalysis.Analyzers` exists in isolation.
- AC02 — Analyzer targets `netstandard2.0`.
- AC03 — No `ProjectReference` exists for inherited projects.
- AC04 — Roslyn remains at the initially compatible private version.
- AC05 — `Microsoft.CodeAnalysis.Workspaces` is not present.
- AC06 — `MathNet.Numerics` is not present.
- AC07 — A valid `DiagnosticAnalyzer` exists.
- AC08 — `BIG9000` exists only as an execution probe.
- AC09 — `BIG9000` is disabled by default.
- AC10 — Automated tests prove execution and controlled emission of the probe.
- AC11 — `dotnet build` works in Release.
- AC12 — `dotnet pack` produces a valid `.nupkg`.
- AC13 — Analyzer assembly is placed under `analyzers/dotnet/cs/`.
- AC14 — A temporary consumer can install the package and enable `BIG9000`.
- AC15 — `dotnet build` of the consumer reports `BIG9000`.
- AC16 — Inherited projects remain unchanged.
- AC17 — Original inherited build continues working as before Phase 1.

## Definition of Done

Phase 1 is done when all acceptance criteria pass, the implementation remains isolated under `analyzer/`, the package layout is verified, the temporary consumer smoke test proves `BIG9000`, and the handoff file identifies the next step with only minimal semantic context.

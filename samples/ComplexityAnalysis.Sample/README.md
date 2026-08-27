# ComplexityAnalysis.Sample

This sample is a tiny console app that demonstrates how `ComplexityAnalysis.Analyzers` runs during a normal build.

It references the analyzer project as a Roslyn analyzer so contributors can clone the repository, build the sample, and see diagnostics without publishing or installing a local package first.

## Build

From the repository root:

```bash
dotnet restore samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj
dotnet build samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj
```

## Expected diagnostics

The sample intentionally contains code that is useful for analyzer demonstration, not necessarily production style.

- `BIG1001` is expected for `blockedCustomerIds.Contains(customerId)` inside the `foreach` loop in `CountBlockedCustomers`.
- `BIG2001` is expected for `ClassifyOrder` because the sample `.editorconfig` sets `complexity_analyzers.maximum_cyclomatic_complexity = 3`.

The sample `.editorconfig` is local to this sample directory and raises these diagnostics to warnings so they are visible in build output.

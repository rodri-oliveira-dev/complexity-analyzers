# Configuration

[English](configuration.md) | [Português (Brasil)](../pt-BR/configuration.md)

`ComplexityAnalysis.Analyzers` uses standard Roslyn diagnostic severity configuration. There are no custom analyzer options through Phase 3.

## .editorconfig Format

Use the standard rule-specific format:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Common values include:

```text
none
silent
suggestion
warning
error
default
```

The exact build behavior is controlled by the .NET compiler and SDK conventions for analyzer diagnostics.

## BIG9000 Default

`BIG9000` is disabled by default in its descriptor:

```text
Default severity: Info
Enabled by default: false
```

With no explicit configuration, tests confirm the analyzer does not report `BIG9000`.

## Keep BIG9000 Disabled

Use this in normal projects:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## Enable BIG9000 for a Smoke Test

Use `suggestion` when you want a low-severity visible signal:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = suggestion
```

Use `warning` when you want the probe to be obvious in a temporary package-consumption or CI smoke test:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

`warning` changes the severity configured by the consumer. It does not mean `BIG9000` is originally a warning. The analyzer defines it as an `Info` diagnostic.

## Disable It Again

After the smoke test, remove the explicit setting or set:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Do not keep `BIG9000` enabled permanently unless you intentionally want an infrastructure diagnostic on every compilation where the analyzer runs.

## What Is Not Configurable Yet

Through Phase 3, there are no public options for:

- Big-O thresholds;
- loop complexity severity;
- BCL or LINQ mapping behavior;
- recursion handling;
- method-call resolution;
- product diagnostic IDs.

Those capabilities are not exposed as diagnostics yet.

# Security Policy

## Supported Versions

Security fixes are prioritized for the latest released version and the current `main` branch.

Older versions may receive fixes when practical, but long-term support is not guaranteed.

## Reporting Security Issues

Please use a private GitHub security reporting channel when one is available for this repository. Do not include sensitive details in a public issue.

When reporting, include the affected version or commit, relevant .NET/Roslyn host information, a minimal reproduction, and the expected impact.

## Triage

Maintainers will review reports as soon as reasonably possible. Response and remediation timelines depend on severity, release complexity, maintainer availability, and coordinated disclosure needs.

Sensitive details should remain private until a fix or mitigation is available.

## Project Security Practices

The analyzer is designed for bounded and deterministic compiler/IDE execution. Its analysis hot paths should not require network access, process execution, filesystem I/O, or telemetry. Repository automation also uses code scanning, dependency review, automated dependency updates, quality analysis, and protected-branch checks as complementary controls.

# Issue 35 Release Quality Governance

This SDD artifact records the Specification, Discovery, Design, Development,
Validation, and Delivery plan for issue #35.

Issues #31 through #34 are complete and merged:

| Issue | PR | Merge commit | Contract produced |
| --- | --- | --- | --- |
| #31 | #37 `test: establish analyzer correctness baseline` | `9ee8057` | Correctness characterization, supported/unsupported behavior matrix, conservative `Unknown`, descriptor/package/safety baseline. |
| #32 | #38 `perf: define analyzer performance budgets and regression gates` | `06efd98` | Performance budgets, structural gates, cache review, material regression policy, performance harness. |
| #33 | #39 `test: formalize Roslyn host and package compatibility` | `00f9d37` | Roslyn baseline, `netstandard2.0` host contract, package layout, consumer tests, .NET 8/9/10 compatibility matrix. |
| #34 | #40 `feat: improve diagnostic explainability` | `222b190` | Diagnostic message/properties convention, EN/PT-BR analyzer catalog guidance, release tracking for changed diagnostics. |

Roadmap #36 keeps #35 as the final phase 07 hardening item before #8.

## Specification

The canonical source of truth for future change readiness is:

- `docs/en/development/quality-gates.md`
- localized equivalent: `docs/pt-BR/development/quality-gates.md`

The policy must answer which evidence is needed for a change to be ready without
making every validation mandatory for every change.

It defines:

1. change types and risk categories;
2. required validations per type;
3. automated evidence versus human review evidence;
4. documentation and release-tracking requirements;
5. cases where a validation is not applicable.

The central principle is:

```text
Validation must be proportional to risk.
```

Normal validation is separate from production release. No tag, NuGet publication,
GitHub Packages publication, or GitHub Release is part of ordinary DoD.

## Discovery

| Source | Rule | Area | Automated? | Duplicate? | Conflict? |
| --- | --- | --- | --- | --- | --- |
| `AGENTS.md` | Work from repo root; preserve independent analyzer package; validate proportionally; no release/publish/push without explicit request. | Agent governance, package, release safety. | Partly. | Yes, with Copilot/custom agent. | No; needs pointer to canonical DoD. |
| `CONTRIBUTING.md` | Run core checks, keep changes focused, update docs/release tracking when public diagnostics change, avoid weakening validation. | Contributor guidance. | Partly. | Yes, with AGENTS and Copilot. | No; needs change-type matrix reference and docs-only lightweight path. |
| `.github/copilot-instructions.md` | Preserve `Unknown`, performance, compatibility, package layout, proportional validation, release safety. | Copilot guidance. | Partly. | Yes. | No; needs canonical DoD reference. |
| `.github/agents/complexity-analyzer-architect.agent.md` | Same analyzer invariants plus role-specific validation expectations. | Custom agent. | Partly. | Yes. | No; needs canonical DoD reference. |
| `specs/issue-31-analyzer-characterization-baseline.md` | Characterization baseline; `Unknown`; diagnostics descriptors; generated code; package contract. | Correctness. | Yes through tests. | No. | No. |
| `specs/issue-32-analyzer-performance-baseline.md` and `performance/README.md` | Budgets, structural gates, cache/cancellation review, material regression policy. | Performance. | Yes for structural gates; timing informational. | Some summary duplicate in docs architecture. | No. |
| `specs/issue-33-roslyn-host-package-compatibility.md` and architecture docs | Roslyn 4.8 baseline, `netstandard2.0`, package contract, .NET 8/9/10 consumer matrix. | Compatibility/package. | Yes. | Some summary duplicate in README/architecture. | No. |
| `specs/issue-34-diagnostic-explainability-guidance.md` and analyzer catalog docs | Evidence-based diagnostic messages/properties and conditional guidance. | Diagnostic UX. | Yes through tests where applicable. | Some summary duplicate in docs. | No. |
| `.github/workflows/complexity-analyzers-ci.yml` | `Quality`, `Package`, `Compatibility (8.0.x)`, `Compatibility (9.0.x)`, `Compatibility (10.0.x)`, `Performance`. | Required CI. | Yes. | Intentional overlap with analyzer-ci. | No. |
| `.github/workflows/analyzer-ci.yml` | `Validate analyzer`, `SonarQube Cloud`, `Pack analyzer`; dependency review is informational because it is `continue-on-error`. | Required CI/quality/package. | Yes. | Intentional overlap for coverage/Sonar/package artifact. | No. |
| `.github/workflows/release.yml` | Manual `workflow_dispatch`, main branch only, semver input, tag verification, Trusted Publishing, GitHub Packages, GitHub Release. | Release. | Yes when intentionally run. | No. | No. |
| `.github/dependabot.yml` | Group Roslyn updates; ignore `Microsoft.CodeAnalysis.CSharp` above `4.8.0`; ignore major analyzer-authoring rules. | Dependency governance. | Yes for PR creation policy. | No. | No. |
| GitHub ruleset `main` | Requires PR, linear history, thread resolution, exact status checks, CodeQL constraints. | Branch policy. | Yes. | No. | No. |
| GitHub ruleset `release-tags` | Protects `refs/tags/v*` from deletion, update, and non-fast-forward. | Release safety. | Yes. | No. | No. |
| `SECURITY.md` | Analyzer hot paths should not require network, process, filesystem I/O, or telemetry. | Security. | Partly through performance budget tests. | Yes, with performance docs. | No. |

No issue templates or PR template existed before this issue.

Rules still primarily human-reviewed:

- deciding the change type and applicability of focused validation;
- false-positive/false-negative reasoning;
- public documentation scope;
- semantic-version impact;
- diagnostic guidance quality;
- whether timing variance is material.

Rules already automated:

- solution restore/build/test;
- package layout inspection;
- consumer package smoke on supported .NET SDK hosts;
- structural performance harness;
- compiler analyzer reporting path;
- coverage report shape;
- SonarQube quality gate;
- required status checks and tag protection.

No deterministic CI gap justified a new workflow in this issue. The missing
signal was discoverability and consistency, not test execution.

## Design

The governance shape is:

```text
canonical quality policy
  -> CONTRIBUTING
  -> AGENTS
  -> Copilot instructions
  -> custom agent
  -> PR template
```

Long-form requirements live in the canonical policy. Other entry points provide
short routing text only.

The PR template is intentionally small and uses "when applicable" so docs-only
or test-only work is not forced through analyzer-specific gates.

No CI workflow change is made because existing required checks already cover the
deterministic gates from #31 through #34, and adding a new check here would
increase maintenance cost without clear new signal.

## Development

Implemented:

- added canonical English and Brazilian Portuguese quality-gates docs;
- added this issue #35 SDD artifact;
- updated `docs/README.md` navigation;
- updated `CONTRIBUTING.md` to route validation through the canonical DoD;
- updated `AGENTS.md`, `.github/copilot-instructions.md`, and the custom agent
  with concise canonical DoD references;
- added `.github/pull_request_template.md`;
- left CI unchanged intentionally.

## Validation Plan

Because this is governance and documentation plus PR template, validation is
proportional:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
git diff --check
git diff
```

Focused gates mapped by the canonical policy remain available for future PRs:

- `AnalyzerCharacterizationBaselineTests` for #31 correctness evidence;
- `AnalyzerPerformanceBudgetContractTests` and `PerformanceSyntheticCorpusTests`
  for #32 performance evidence;
- `AnalyzerPackageContractTests`, `AnalyzerPackageConsumerContractTests`, and
  `AnalyzerHostCompatibilityContractTests` for #33 compatibility/package
  evidence;
- diagnostic message/property tests and EN/PT-BR catalog docs for #34 UX
  evidence.

No package, dependency, Roslyn, diagnostic, analyzer behavior, performance
budget, workflow, release, or version change is intended.

## Delivery

Before commit:

- run the validation plan;
- review `git diff --check` and full `git diff`;
- confirm no generated artifacts are staged;
- confirm no required check names changed;
- confirm no release action was performed.

The PR should be titled `docs: establish release-quality governance for analyzer
changes` and include `Closes #35`.

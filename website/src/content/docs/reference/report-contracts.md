---
title: Report contracts
description: Understand console, JSON, SARIF, snapshot, comparison, and evidence outputs.
sidebar:
  order: 2
---

## Console

The default human-readable report is for local inspection. Do not parse its presentation text as an automation contract.

## JSON and snapshots

JSON is the machine-readable report and snapshot foundation. It preserves stable member identities, known or `Unknown` complexity status, independent structural and size metrics, assumptions, and evidence where the selected contract includes them. Snapshot mode normalizes volatile data for deterministic comparison.

Consumers should validate `schemaVersion` and tolerate documented additive fields rather than binding to incidental property order.

## SARIF

SARIF output uses SARIF 2.1.0 for compatible code-scanning consumers. Locations are repository-relative where possible. A SARIF file is a report transport, not proof that every result is a compiler diagnostic.

## Comparison

Comparison matches stable member identities and orders only compatible, known Big-O expressions. Unknown or incompatible evidence remains incomparable. `--fail-on-regression` changes the exit code only for proven regressions.

For the exact current field names, defaults, and examples, use the synchronized [CLI reference](/complexity-analyzers/cli/reference/) from the repository source.

---
title: Snapshot comparison
description: Produce deterministic snapshots and fail only on proven complexity regressions.
sidebar:
  order: 5
---

Create a baseline and a current snapshot using the JSON report contract:

```bash
complexity-analysis analyze App.csproj --format json --snapshot --output baseline.json
complexity-analysis analyze App.csproj --format json --snapshot --output current.json
complexity-analysis compare baseline.json current.json --format json --fail-on-regression
```

Members are matched by stable identity. A known increase such as `O(n)` to `O(n²)` can be classified as a proven regression and return exit code `2` when `--fail-on-regression` is enabled.

An `Unknown` estimate, missing member, or incompatible evidence is not silently ordered against a known complexity. The comparison records the result as incomparable so the reviewer can investigate without a false gate.

Commit or retain baselines only according to your repository's artifact and review policy. Snapshot comparison is deterministic and does not depend on Git history or an external service.

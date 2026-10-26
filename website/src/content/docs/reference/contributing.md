---
title: Contributing
description: Build, test, document, and review changes to Complexity Analyzers.
sidebar:
  order: 4
---

Read the repository's [contribution guide](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CONTRIBUTING.md), [code of conduct](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CODE_OF_CONDUCT.md), and [security policy](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/SECURITY.md) before opening a change.

## Documentation changes

- Edit portal-owned narrative pages under `website/src/content/docs/` in both locales.
- Edit synchronized technical material under `docs/en/` and `docs/pt-BR/`, then run `npm run sync:docs` from `website/`.
- Keep route structure and technical meaning equivalent across languages.
- Do not present planned behavior as implemented.

## Validation

Run `npm ci` once, then `npm run validate`. For analyzer, CLI, or MCP behavior changes, also run the repository's proportional .NET quality gates. Automated accessibility checks do not replace keyboard, screen-reader, zoom, and cognitive-accessibility review.

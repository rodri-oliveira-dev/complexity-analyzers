# Complexity Analyzers documentation portal

This directory contains the static Astro + Starlight portal for the Analyzer, CLI, and MCP products. English is served at `/complexity-analyzers/` and Brazilian Portuguese at `/complexity-analyzers/pt-br/`.

The planned production URL is `https://rodri-oliveira-dev.github.io/complexity-analyzers/`. Do not describe the portal as published until the GitHub Pages deployment job succeeds.

## Requirements

- Node.js 24 or later
- npm 11; the committed `package-lock.json` is authoritative

## Local development

From the repository root:

```bash
cd website
npm ci
npm run dev
```

The development hook synchronizes selected repository documents before Astro starts. Open `http://localhost:4321/complexity-analyzers/` so assets and routes use the configured base path.

## Validation

```bash
npm run check
npm test
npm run build
npm run test:routes
npm run test:links
npm run test:seo
npm run test:html
npm run test:spelling
```

`npm run validate` runs the main check, unit-test, build, route, link, SEO, and HTML gates. Search uses Starlight's Pagefind integration and is available in the production build, not the development server.

Accessibility and Lighthouse run in separate GitHub Actions workflows against representative English and Portuguese production routes. Automated checks target WCAG 2.2 AA-aligned outcomes where supported, but do not establish complete conformance or replace manual keyboard, screen-reader, zoom, and cognitive-accessibility review.

## Content structure

Portal-owned editorial content lives in `src/content/docs/`, with Portuguese counterparts under `src/content/docs/pt-br/`. Keep equivalent routes and technical meaning in both languages.

Repository technical documentation remains authoritative under `../docs/en/` and `../docs/pt-BR/`. `scripts/sync-docs.mjs` maps selected source documents to portal routes, adds deterministic frontmatter, rewrites mapped Markdown links and images, and emits ignored files before development, checks, tests, and builds.

Do not edit a generated page carrying the **Source synced** or **Fonte sincronizada** badge. Edit its source in `docs/`, run `npm run sync:docs`, and review both generated locales locally.

## Translation workflow

1. Add or update the English editorial page.
2. Add or update the complete Brazilian Portuguese counterpart at the same route below `pt-br/`.
3. Preserve code contracts, command names, diagnostic IDs, schema fields, and product names.
4. Run the full validation and verify the language selector keeps the corresponding route.

For synchronized technical documents, update both source-language files and keep the explicit map in `scripts/sync-docs.mjs` aligned.

## Design and assets

The portal uses product-specific design tokens in `src/styles/tokens.css`, reusable presentation styling in `src/styles/custom.css`, SVG logo assets, and `public/social-preview.png` for social cards. Keep sufficient contrast in both themes and avoid decorative changes that obscure technical content.

## GitHub Pages publication

`.github/workflows/documentation-portal.yml` validates pull requests but builds and deploys the Pages artifact only from `main`. Separate workflows cover SEO/HTML/spelling, automated accessibility, and Lighthouse budgets.

In repository **Settings → Pages**, configure **Source = GitHub Actions** once. No custom domain is required for the planned project URL. Publishing the portal does not change Analyzer, CLI, or MCP release workflows.

import { readdir, readFile } from 'node:fs/promises';
import { relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const websiteRoot = resolve(fileURLToPath(new URL('..', import.meta.url)));
const dist = resolve(websiteRoot, 'dist');
const files = [];

async function walk(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) await walk(path);
    else files.push(path);
  }
}

await walk(dist);
const htmlFiles = files.filter((file) => file.endsWith('.html'));
const routeFromFile = (file) => {
  const path = relative(dist, file).split(sep).join('/');
  if (path === 'index.html') return '';
  return path.replace(/(?:\/index)?\.html$/, '');
};
const routes = new Set(htmlFiles.map(routeFromFile));

const required = [
  '', 'getting-started', 'getting-started/quick-install', 'getting-started/first-diagnostic', 'getting-started/choose-product',
  'learn', 'learn/complexity-models', 'learn/operations-recursion-unknown', 'learn/cognitive-complexity', 'learn/halstead-metrics',
  'analyzer', 'analyzer/installation', 'analyzer/diagnostics', 'analyzer/configuration',
  'cli', 'cli/github-actions', 'cli/reference',
  'mcp', 'mcp/agent-workflows', 'mcp/reference',
  'examples', 'examples/collection-and-linq-costs', 'examples/recursion-and-cognition', 'examples/refactoring', 'examples/snapshot-comparison',
  'reference', 'reference/report-contracts', 'reference/architecture', 'reference/security-baseline', 'reference/development-quality-gates', 'reference/releases', 'reference/releases/mcp-1-0-0', 'reference/releases/mcp-1-0-1', 'reference/contributing'
];

const failures = [];
for (const route of required) {
  if (!routes.has(route)) failures.push(`missing English route: /${route}`);
  const localized = route ? `pt-br/${route}` : 'pt-br';
  if (!routes.has(localized)) failures.push(`missing Portuguese route: /${localized}`);
}

for (const route of routes) {
  if (route === '404' || route === 'pt-br/404') continue;
  if (route.startsWith('pt-br/')) {
    const english = route.slice('pt-br/'.length);
    if (!routes.has(english)) failures.push(`Portuguese route has no English equivalent: /${route}`);
  } else if (route === '') {
    if (!routes.has('pt-br')) failures.push('English homepage has no Portuguese equivalent.');
  } else if (route !== 'pt-br' && !routes.has(`pt-br/${route}`)) {
    failures.push(`English route has no Portuguese equivalent: /${route}`);
  }
}

// These routes use both the splash layout (without a sidebar) and the standard docs layout.
// Native Starlight selectors must remain available in the independent mobile header menu.
for (const route of ['', 'getting-started']) {
  for (const locale of ['en', 'pt-br']) {
    const slug = [locale === 'pt-br' ? 'pt-br' : '', route].filter(Boolean).join('/');
    const file = resolve(dist, slug, 'index.html');
    const html = await readFile(file, 'utf8');
    const preferences = html.match(/<details\b[^>]*class="[^"]*\bmobile-preferences\b[^"]*"[^>]*>([\s\S]*?)<\/details>/)?.[1];
    const location = slug || 'homepage';

    if (!preferences) {
      failures.push(location + ': missing mobile preferences menu.');
      continue;
    }
    if (!preferences.includes('<summary') || !preferences.includes('aria-label=')) {
      failures.push(location + ': mobile preferences trigger has no accessible label.');
    }
    if (!preferences.includes('<starlight-theme-select>') || !preferences.includes('<starlight-lang-select>')) {
      failures.push(location + ': expected native Starlight preference controls.');
    }
    if (['auto', 'dark', 'light'].some((value) => !preferences.includes('value="' + value + '"'))) {
      failures.push(location + ': missing one or more native theme options.');
    }

    const counterpart = (locale === 'en' ? 'pt-br/' : '') + (route ? route + '/' : '');
    const expectedLanguage = '/complexity-analyzers/' + counterpart;
    if (!preferences.includes('value="' + expectedLanguage + '"')) {
      failures.push(location + ': missing translated route option ' + expectedLanguage);
    }
  }
}

const searchBundle = files.some((file) => relative(dist, file).split(sep).join('/') === 'pagefind/pagefind.js');
if (!searchBundle) failures.push('Starlight Pagefind search bundle was not generated.');

for (const file of htmlFiles) {
  const html = await readFile(file, 'utf8');
  if (html.includes('href="/favicon.svg"')) failures.push(`${relative(dist, file)} uses a root-relative favicon outside the configured base.`);
}

if (failures.length) throw new Error(`Route validation failed (${failures.length}):\n${failures.join('\n')}`);
console.log(`Validated ${routes.size} routes, bilingual parity, base-path assets, and Pagefind output.`);

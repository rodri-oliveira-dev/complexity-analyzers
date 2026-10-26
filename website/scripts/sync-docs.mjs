import { mkdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, posix, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const websiteRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repositoryRoot = resolve(websiteRoot, '..');
const contentRoot = resolve(websiteRoot, 'src/content/docs');
const repositoryUrl = 'https://github.com/rodri-oliveira-dev/complexity-analyzers';
const basePath = '/complexity-analyzers/';

const englishMappings = [
  ['docs/en/getting-started.md', 'analyzer/installation.md'],
  ['docs/en/analyzers.md', 'analyzer/diagnostics.md'],
  ['docs/en/configuration.md', 'analyzer/configuration.md'],
  ['docs/en/cognitive-complexity.md', 'learn/cognitive-complexity.md'],
  ['docs/en/halstead-metrics.md', 'learn/halstead-metrics.md'],
  ['docs/en/project-tooling.md', 'cli/reference.md'],
  ['docs/en/mcp-server.md', 'mcp/reference.md'],
  ['docs/en/architecture.md', 'reference/architecture.md'],
  ['docs/en/development/security-baseline.md', 'reference/security-baseline.md'],
  ['docs/en/development/quality-gates.md', 'reference/development-quality-gates.md'],
  ['docs/en/releases/mcp-1.0.0.md', 'reference/releases/mcp-1-0-0.md']
];

const mappings = [
  ...englishMappings.map(([source, destination]) => ({ source, destination, locale: 'en' })),
  ...englishMappings.map(([source, destination]) => ({
    source: source.replace('docs/en/', 'docs/pt-BR/'),
    destination: `pt-br/${destination}`,
    locale: 'pt-BR'
  }))
];

const sourceToRoute = new Map(mappings.map(({ source, destination }) => [source.toLowerCase(), destination.replace(/\.md$/, '/')]));

function repositoryPath(path) {
  return relative(repositoryRoot, path).split(sep).join('/');
}

function quoteYaml(value) {
  return `'${value.replaceAll("'", "''")}'`;
}

function titleFrom(markdown, fallback) {
  return markdown.match(/^#\s+(.+)$/m)?.[1]?.replace(/[*`]/g, '').trim() ?? fallback;
}

function descriptionFrom(markdown, fallback) {
  const paragraphs = markdown
    .replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, '')
    .replace(/^#\s+.+\r?\n/, '')
    .split(/\r?\n\s*\r?\n/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph && !/^(?:#|```|:::|>|-|\*|\d+\.)/.test(paragraph))
    .filter((paragraph) => !/^\[(?:Português|English|Read in English|Leia em português)\b/i.test(paragraph));
  const plain = paragraphs[0]
    ?.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    .replace(/<[^>]+>/g, '')
    .replace(/[*_`]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
  if (!plain) return fallback;
  return plain.length <= 170 ? plain : `${plain.slice(0, 167).replace(/\s+\S*$/, '')}…`;
}

function routeForDestination(destination) {
  return `${basePath}${destination.replace(/\.md$/, '/')}`;
}

function rewrite(markdown, sourcePath, mapping, title) {
  let body = markdown.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, '');
  body = body.replace(/^#\s+.+\r?\n/, '');
  body = body.replace(/^\[(?:Português[^\]]*|English|Read in English|Leia em português[^\]]*)\]\([^\n]+\)\s*\r?\n+/i, '');

  body = body.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, (match, alt, target) => {
    if (/^(?:https?:|data:|\/)/.test(target)) return match;
    const absolute = resolve(dirname(sourcePath), target);
    const publicRelative = relative(resolve(websiteRoot, 'public'), absolute).split(sep).join('/');
    if (!publicRelative.startsWith('../') && publicRelative !== '..') return `![${alt}](${basePath}${publicRelative})`;
    return `![${alt}](https://raw.githubusercontent.com/rodri-oliveira-dev/complexity-analyzers/main/${repositoryPath(absolute)})`;
  });

  body = body.replace(/\[([^\]]+)\]\(([^)]+\.md(?:#[^)]+)?)\)/g, (match, label, target) => {
    if (/^(?:https?:|mailto:)/.test(target)) return match;
    const [filePart, hash = ''] = target.split('#');
    const absolute = resolve(dirname(sourcePath), filePart);
    const route = sourceToRoute.get(repositoryPath(absolute).toLowerCase());
    if (route) return `[${label}](${basePath}${route}${hash ? `#${hash}` : ''})`;
    return `[${label}](${repositoryUrl}/blob/main/${repositoryPath(absolute)}${hash ? `#${hash}` : ''})`;
  });

  body = body.replace(/\[([^\]]+)\]\((?!https?:|mailto:|#|\/)([^)]+)\)/g, (match, label, target) => {
    const [filePart, hash = ''] = target.split('#');
    return `[${label}](${repositoryUrl}/blob/main/${repositoryPath(resolve(dirname(sourcePath), filePart))}${hash ? `#${hash}` : ''})`;
  });

  const sourceRelative = repositoryPath(sourcePath);
  const description = descriptionFrom(markdown, mapping.locale === 'pt-BR' ? `Documentação técnica de ${title}.` : `${title} technical documentation.`);
  const badge = mapping.locale === 'pt-BR' ? 'Fonte sincronizada' : 'Source synced';
  const order = posix.basename(mapping.destination).startsWith('installation') ? 2 : undefined;
  return `---\neditUrl: ${quoteYaml(`${repositoryUrl}/edit/main/${sourceRelative}`)}\ntitle: ${quoteYaml(title)}\ndescription: ${quoteYaml(description)}\nsidebar:\n${order ? `  order: ${order}\n` : ''}  badge:\n    text: ${badge}\n    variant: note\n---\n\n<!-- Generated by scripts/sync-docs.mjs from ${sourceRelative}. Do not edit this file. -->\n\n${body.trim()}\n`;
}

for (const [index, mapping] of mappings.entries()) {
  const sourcePath = resolve(repositoryRoot, mapping.source);
  const destinationPath = resolve(contentRoot, mapping.destination);
  const temporaryPath = `${destinationPath}.${process.pid}.${index}.tmp`;
  const markdown = await readFile(sourcePath, 'utf8');
  const title = titleFrom(markdown, posix.basename(mapping.destination, '.md'));
  await mkdir(dirname(destinationPath), { recursive: true });
  try {
    await writeFile(temporaryPath, rewrite(markdown, sourcePath, mapping, title), 'utf8');
    await rename(temporaryPath, destinationPath);
  } finally {
    await rm(temporaryPath, { force: true });
  }
}

console.log(`Synchronized ${mappings.length} source documents.`);

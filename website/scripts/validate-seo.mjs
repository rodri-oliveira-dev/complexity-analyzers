import { readdir, readFile } from 'node:fs/promises';
import { relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../dist', import.meta.url)));
const site = 'https://rodri-oliveira-dev.github.io';
const base = '/complexity-analyzers/';
const htmlFiles = [];

async function walk(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) await walk(path);
    else if (entry.name.endsWith('.html')) htmlFiles.push(path);
  }
}
await walk(root);

const attr = (html, name, value) => html.match(new RegExp(`<meta[^>]+${name}=["']${value}["'][^>]+content=["']([^"']+)["']`, 'i'))?.[1]
  ?? html.match(new RegExp(`<meta[^>]+content=["']([^"']+)["'][^>]+${name}=["']${value}["']`, 'i'))?.[1];
const link = (html, rel, language) => {
  const tags = [...html.matchAll(/<link\b[^>]*>/gi)].map((match) => match[0]);
  const tag = tags.find((candidate) => new RegExp(`rel=["']${rel}["']`, 'i').test(candidate) && (!language || new RegExp(`hreflang=["']${language}["']`, 'i').test(candidate)));
  return tag?.match(/href=["']([^"']+)["']/i)?.[1];
};

const failures = [];
const indexable = new Set();
for (const file of htmlFiles) {
  const path = relative(root, file).split(sep).join('/');
  const html = await readFile(file, 'utf8');
  const is404 = path === '404.html' || path === 'pt-br/404/index.html';
  const title = html.match(/<title>([^<]+)<\/title>/i)?.[1]?.trim();
  const description = attr(html, 'name', 'description');
  const robots = attr(html, 'name', 'robots');
  const canonical = link(html, 'canonical');
  if (!title) failures.push(`${path}: missing title`);
  if (!description) failures.push(`${path}: missing meta description`);
  if (is404) {
    if (!robots?.includes('noindex')) failures.push(`${path}: 404 must be noindex`);
    continue;
  }
  if (!canonical?.startsWith(`${site}${base}`)) failures.push(`${path}: invalid canonical ${canonical ?? '(missing)'}`);
  else indexable.add(canonical);
  for (const language of ['en', 'pt-BR']) if (!link(html, 'alternate', language)) failures.push(`${path}: missing hreflang ${language}`);
  if (attr(html, 'property', 'og:image') !== `${site}${base}social-preview.png`) failures.push(`${path}: invalid Open Graph image`);
  if (attr(html, 'name', 'twitter:card') !== 'summary_large_image') failures.push(`${path}: invalid Twitter card`);
  const jsonLd = html.match(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/i)?.[1];
  if (!jsonLd) failures.push(`${path}: missing JSON-LD`);
  else { try { JSON.parse(jsonLd); } catch { failures.push(`${path}: invalid JSON-LD`); } }
}

const sitemapIndex = await readFile(resolve(root, 'sitemap-index.xml'), 'utf8');
const sitemapFiles = [...sitemapIndex.matchAll(/<loc>[^<]*\/(sitemap-[^<]+\.xml)<\/loc>/g)].map((match) => match[1]);
if (!sitemapFiles.length) failures.push('sitemap-index.xml contains no child sitemap');
const sitemapUrls = new Set();
for (const sitemapFile of sitemapFiles) {
  const xml = await readFile(resolve(root, sitemapFile), 'utf8');
  for (const block of xml.matchAll(/<url>([\s\S]*?)<\/url>/g)) {
    const location = block[1].match(/<loc>([^<]+)<\/loc>/)?.[1];
    if (!location) continue;
    sitemapUrls.add(location);
    if (!location.startsWith(`${site}${base}`)) failures.push(`${sitemapFile}: URL outside base: ${location}`);
    const alternates = [...block[1].matchAll(/<xhtml:link\b/g)].length;
    if (alternates !== 2) failures.push(`${sitemapFile}: ${location} has ${alternates} alternates; expected 2`);
  }
}
for (const url of indexable) if (!sitemapUrls.has(url)) failures.push(`sitemap missing ${url}`);
for (const url of sitemapUrls) if (!indexable.has(url)) failures.push(`sitemap contains non-indexable ${url}`);

if (failures.length) throw new Error(`SEO validation failed (${failures.length}):\n${failures.join('\n')}`);
console.log(`Validated metadata, JSON-LD, hreflang, and sitemap for ${indexable.size} pages.`);

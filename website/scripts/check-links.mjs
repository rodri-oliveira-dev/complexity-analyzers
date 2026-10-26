import { access, readdir, readFile } from 'node:fs/promises';
import { dirname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../dist', import.meta.url)));
const base = '/complexity-analyzers/';
const origin = 'https://portal.invalid';
const htmlFiles = [];

async function walk(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) await walk(path);
    else if (entry.name.endsWith('.html')) htmlFiles.push(path);
  }
}
await walk(root);

const ids = new Map();
for (const file of htmlFiles) {
  const html = await readFile(file, 'utf8');
  ids.set(file, new Set([...html.matchAll(/\sid=["']([^"']+)["']/g)].map((match) => match[1])));
}

const failures = [];
for (const file of htmlFiles) {
  const html = await readFile(file, 'utf8');
  const relativeFile = relative(root, file).split(sep).join('/');
  const pagePath = `${base}${relativeFile === 'index.html' ? '' : relativeFile.replace(/index\.html$/, '')}`;
  const references = [...html.matchAll(/\s(?:href|src)=["']([^"']+)["']/g)].map((match) => match[1]);
  for (const reference of references) {
    if (/^(?:https?:|mailto:|tel:|data:|javascript:)/.test(reference)) continue;
    const url = new URL(reference, `${origin}${pagePath}`);
    if (!url.pathname.startsWith(base)) {
      failures.push(`${relativeFile}: local reference escapes base path: ${reference}`);
      continue;
    }
    const local = decodeURIComponent(url.pathname.slice(base.length));
    let target = resolve(root, local);
    if (url.pathname.endsWith('/')) target = resolve(target, 'index.html');
    else if (!/\.[a-z0-9]+$/i.test(url.pathname)) target = resolve(target, 'index.html');
    try { await access(target); } catch { failures.push(`${relativeFile}: missing target ${reference}`); continue; }
    if (url.hash && target.endsWith('.html')) {
      const targetIds = ids.get(target) ?? new Set([...(await readFile(target, 'utf8')).matchAll(/\sid=["']([^"']+)["']/g)].map((match) => match[1]));
      const fragment = decodeURIComponent(url.hash.slice(1));
      if (fragment && !targetIds.has(fragment)) failures.push(`${relativeFile}: missing fragment ${reference}`);
    }
  }
}

if (failures.length) throw new Error(`Internal link validation failed (${failures.length}):\n${failures.join('\n')}`);
console.log(`Validated internal links and fragments across ${htmlFiles.length} HTML files.`);

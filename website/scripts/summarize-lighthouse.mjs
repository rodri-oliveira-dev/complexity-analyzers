import { appendFile, readFile, readdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const defaultConfig = require('../lighthouserc.cjs');
const categoryLabels = { performance: 'Performance', accessibility: 'Accessibility', 'best-practices': 'Best Practices', seo: 'SEO' };

export function median(values) {
  if (!values.length || values.some((value) => !Number.isFinite(value))) throw new Error('Median requires one or more finite values.');
  const sorted = [...values].sort((left, right) => left - right);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

export function analyzeReports(reports, config = defaultConfig) {
  if (!reports.length) throw new Error('No Lighthouse JSON reports were found.');

  const samplesByUrl = createSampleBuckets(config);
  for (const report of reports) {
    collectReportSamples(report, config, samplesByUrl);
  }

  const scores = new Map();
  for (const [url, samples] of samplesByUrl) {
    scores.set(url, calculateMedians(url, samples, config.numberOfRuns));
  }

  const gates = Object.entries(config.assertions).map(([category, assertion]) => {
    const actual = Math.min(...[...scores.values()].map((entry) => entry[category]));
    return { category, actual, ...assertion, result: gateResult(actual, assertion) };
  });
  return { scores, gates, failed: gates.some((gate) => gate.result === 'FAIL') };
}

function createSampleBuckets(config) {
  const categories = Object.keys(config.assertions);
  return new Map(config.urls.map((url) => [
    url,
    Object.fromEntries(categories.map((category) => [category, []])),
  ]));
}

function collectReportSamples(report, config, samplesByUrl) {
  const url = report.requestedUrl;
  if (!samplesByUrl.has(url)) throw new Error(`Unexpected or missing requested URL in Lighthouse report: ${url ?? '<missing>'}.`);
  if (!report.categories) throw new Error(`Lighthouse report for ${url} has no categories.`);

  for (const category of Object.keys(config.assertions)) {
    const score = report.categories[category]?.score;
    if (!Number.isFinite(score)) throw new Error(`Lighthouse report for ${url} is missing category ${category}.`);
    samplesByUrl.get(url)[category].push(score);
  }
}

function calculateMedians(url, samples, numberOfRuns) {
  for (const [category, values] of Object.entries(samples)) {
    if (values.length !== numberOfRuns) throw new Error(`Expected ${numberOfRuns} valid ${category} measurements for ${url}, found ${values.length}.`);
  }

  return Object.fromEntries(
    Object.entries(samples).map(([category, values]) => [category, median(values)]),
  );
}

function gateResult(actual, assertion) {
  if (actual >= assertion.minScore) return 'PASS';
  return assertion.level === 'error' ? 'FAIL' : 'WARN';
}

const percent = (value) => `${Math.round(value * 100)}%`;

export function renderSummary(analysis, config = defaultConfig) {
  const categories = Object.keys(config.assertions);
  const lines = ['# Lighthouse', '', `Scores are the median of ${config.numberOfRuns} independent runs per representative page.`, '', `| URL | ${categories.map((category) => categoryLabels[category]).join(' | ')} |`, `| --- | ${categories.map(() => '---:').join(' | ')} |`];
  for (const [url, scores] of analysis.scores) lines.push(`| \`${new URL(url).pathname}\` | ${categories.map((category) => percent(scores[category])).join(' | ')} |`);
  lines.push('', '## Quality gates', '', '| Category | Threshold | Enforcement | Lowest median | Result |', '| --- | ---: | --- | ---: | --- |');
  for (const gate of analysis.gates) lines.push(`| ${categoryLabels[gate.category]} | ${percent(gate.minScore)} | ${gate.level === 'error' ? 'Blocking' : 'Warning'} | ${percent(gate.actual)} | ${gate.result} |`);
  return `${lines.join('\n')}\n`;
}

export async function loadReports(directory) {
  let files;
  try { files = (await readdir(directory)).filter((file) => /^report-\d+-\d+\.json$/.test(file)); }
  catch (error) {
    if (error.code === 'ENOENT') throw new Error('Lighthouse report directory was not created.');
    throw error;
  }
  const reports = await Promise.all(files.map(async (file) => JSON.parse(await readFile(join(directory, file), 'utf8'))));
  if (!reports.length) throw new Error('No Lighthouse JSON reports were found.');
  return reports;
}

async function main() {
  const directory = '.lighthouseci';
  const analysis = analyzeReports(await loadReports(directory));
  const summary = renderSummary(analysis);
  await writeFile(join(directory, 'summary.md'), summary, 'utf8');
  if (process.env.GITHUB_STEP_SUMMARY) await appendFile(process.env.GITHUB_STEP_SUMMARY, summary, 'utf8');
  console.log(summary);
  if (analysis.failed) process.exitCode = 1;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) await main();

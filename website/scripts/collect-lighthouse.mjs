import { spawn } from 'node:child_process';
import { mkdir, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';

const require = createRequire(import.meta.url);
const config = require('../lighthouserc.cjs');
const lighthouseCli = require.resolve('lighthouse/cli/index.js');
const reportDirectory = resolve('.lighthouseci');

function run(command, args) {
  return new Promise((resolveRun, reject) => {
    const child = spawn(command, args, { stdio: 'inherit' });
    child.once('error', reject);
    child.once('exit', (code, signal) => {
      if (code === 0) resolveRun();
      else {
        const exitReason = signal ? `signal ${signal}` : `code ${code}`;
        reject(new Error(`Lighthouse exited with ${exitReason}.`));
      }
    });
  });
}

function collectReport({ url, urlIndex, runIndex }) {
  const outputPath = resolve(reportDirectory, `report-${urlIndex + 1}-${runIndex}.json`);
  console.log(`Lighthouse run ${runIndex}/${config.numberOfRuns}: ${url}`);
  return run(process.execPath, [
    lighthouseCli,
    url,
    '--quiet',
    '--output=json',
    `--output-path=${outputPath}`,
    `--chrome-flags=${config.chromeFlags.join(' ')}`,
    `--only-categories=${Object.keys(config.assertions).join(',')}`,
  ]);
}

await rm(reportDirectory, { recursive: true, force: true });
await mkdir(reportDirectory, { recursive: true });

const reports = config.urls.flatMap((url, urlIndex) =>
  Array.from({ length: config.numberOfRuns }, (_, index) => ({ url, urlIndex, runIndex: index + 1 })));

await reports.reduce(
  (previous, report) => previous.then(() => collectReport(report)),
  Promise.resolve(),
);

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
      else reject(new Error(`Lighthouse exited with ${signal ? `signal ${signal}` : `code ${code}`}.`));
    });
  });
}

await rm(reportDirectory, { recursive: true, force: true });
await mkdir(reportDirectory, { recursive: true });

for (const [urlIndex, url] of config.urls.entries()) {
  for (let runIndex = 1; runIndex <= config.numberOfRuns; runIndex += 1) {
    const outputPath = resolve(reportDirectory, `report-${urlIndex + 1}-${runIndex}.json`);
    console.log(`Lighthouse run ${runIndex}/${config.numberOfRuns}: ${url}`);
    await run(process.execPath, [
      lighthouseCli,
      url,
      '--quiet',
      '--output=json',
      `--output-path=${outputPath}`,
      `--chrome-flags=${config.chromeFlags.join(' ')}`,
      `--only-categories=${Object.keys(config.assertions).join(',')}`,
    ]);
  }
}

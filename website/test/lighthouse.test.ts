import { describe, expect, it } from 'vitest';
import { analyzeReports, median } from '../scripts/summarize-lighthouse.mjs';

const config = {
  urls: ['http://127.0.0.1:4321/page/'],
  numberOfRuns: 3,
  chromeFlags: [],
  assertions: {
    performance: { level: 'warn', minScore: 0.85 },
    accessibility: { level: 'error', minScore: 0.95 },
    'best-practices': { level: 'error', minScore: 0.95 },
    seo: { level: 'error', minScore: 0.95 },
  },
};

const report = (scores: Record<string, number> = {}) => ({
  requestedUrl: config.urls[0],
  categories: Object.fromEntries(Object.keys(config.assertions).map((category) => [category, { score: scores[category] ?? 1 }])),
});

describe('Lighthouse quality gates', () => {
  it('calculates odd and even medians without mutating the samples', () => {
    const samples = [0.9, 0.7, 0.8];
    expect(median(samples)).toBe(0.8);
    expect(median([0.7, 0.9, 0.8, 1])).toBeCloseTo(0.85);
    expect(samples).toEqual([0.9, 0.7, 0.8]);
  });

  it.each(Object.entries(config.assertions))('evaluates the %s quality gate', (category, assertion) => {
    expect(analyzeReports([report(), report(), report()], config).gates.find((gate) => gate.category === category)?.result).toBe('PASS');
    const failing = analyzeReports([report({ [category]: assertion.minScore - 0.01 }), report({ [category]: assertion.minScore - 0.02 }), report({ [category]: assertion.minScore - 0.03 })], config);
    expect(failing.gates.find((gate) => gate.category === category)?.result).toBe(assertion.level === 'error' ? 'FAIL' : 'WARN');
  });

  it('keeps performance warnings non-blocking and required-category failures blocking', () => {
    const warning = analyzeReports([report({ performance: 0.8 }), report({ performance: 0.8 }), report({ performance: 0.8 })], config);
    expect(warning.failed).toBe(false);
    expect(warning.gates.find((gate) => gate.category === 'performance')?.result).toBe('WARN');
    const failure = analyzeReports([report({ seo: 0.9 }), report({ seo: 0.9 }), report({ seo: 0.9 })], config);
    expect(failure.failed).toBe(true);
    expect(failure.gates.find((gate) => gate.category === 'seo')?.result).toBe('FAIL');
  });

  it('fails when no reports are provided', () => expect(() => analyzeReports([], config)).toThrow('No Lighthouse JSON reports'));

  it('fails when a required category is absent', () => {
    const invalid = report();
    delete invalid.categories.accessibility;
    expect(() => analyzeReports([invalid, report(), report()], config)).toThrow('missing category accessibility');
  });

  it('fails when fewer than three valid measurements are produced', () => {
    expect(() => analyzeReports([report(), report()], config)).toThrow('Expected 3 valid performance measurements');
  });
});

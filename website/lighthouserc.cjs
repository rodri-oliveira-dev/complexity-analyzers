module.exports = {
  urls: [
    'http://127.0.0.1:4321/complexity-analyzers/',
    'http://127.0.0.1:4321/complexity-analyzers/cli/reference/',
    'http://127.0.0.1:4321/complexity-analyzers/pt-br/',
    'http://127.0.0.1:4321/complexity-analyzers/pt-br/mcp/reference/',
  ],
  numberOfRuns: 3,
  chromeFlags: ['--headless=new', '--no-sandbox', '--disable-dev-shm-usage'],
  assertions: {
    performance: { level: 'warn', minScore: 0.85 },
    accessibility: { level: 'error', minScore: 0.95 },
    'best-practices': { level: 'error', minScore: 0.95 },
    seo: { level: 'error', minScore: 0.95 },
  },
};

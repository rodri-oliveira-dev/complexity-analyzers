module.exports = {
  ci: {
    collect: {
      url: [
        'http://127.0.0.1:4321/complexity-analyzers/',
        'http://127.0.0.1:4321/complexity-analyzers/cli/reference/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/mcp/reference/',
      ],
      numberOfRuns: 3,
      settings: { chromeFlags: '--no-sandbox --disable-dev-shm-usage' }
    },
    assert: {
      assertions: {
        'categories:performance': ['warn', { minScore: 0.85, aggregationMethod: 'median' }],
        'categories:accessibility': ['error', { minScore: 0.95, aggregationMethod: 'median' }],
        'categories:best-practices': ['error', { minScore: 0.95, aggregationMethod: 'median' }],
        'categories:seo': ['error', { minScore: 0.95, aggregationMethod: 'median' }]
      }
    }
  }
};

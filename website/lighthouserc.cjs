module.exports = {
  ci: {
    collect: {
      url: [
        'http://127.0.0.1:4321/complexity-analyzers/',
        'http://127.0.0.1:4321/complexity-analyzers/getting-started/',
        'http://127.0.0.1:4321/complexity-analyzers/learn/complexity-models/',
        'http://127.0.0.1:4321/complexity-analyzers/analyzer/diagnostics/',
        'http://127.0.0.1:4321/complexity-analyzers/cli/reference/',
        'http://127.0.0.1:4321/complexity-analyzers/mcp/reference/',
        'http://127.0.0.1:4321/complexity-analyzers/examples/',
        'http://127.0.0.1:4321/complexity-analyzers/reference/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/getting-started/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/learn/complexity-models/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/analyzer/diagnostics/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/cli/reference/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/mcp/reference/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/examples/',
        'http://127.0.0.1:4321/complexity-analyzers/pt-br/reference/'
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

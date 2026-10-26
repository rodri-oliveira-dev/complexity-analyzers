import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import starlight from '@astrojs/starlight';

const repository = 'https://github.com/rodri-oliveira-dev/complexity-analyzers';

export default defineConfig({
  site: 'https://rodri-oliveira-dev.github.io',
  base: '/complexity-analyzers',
  trailingSlash: 'always',
  integrations: [
    sitemap({
      filter: (page) => !/\/404(?:\.html|\/)$/.test(new URL(page).pathname),
      i18n: { defaultLocale: 'en', locales: { en: 'en', 'pt-br': 'pt-BR' } }
    }),
    starlight({
      title: 'Complexity Analyzers',
      description: 'Conservative complexity evidence for C# builds, automation, and AI tools.',
      logo: {
        light: './src/assets/logo.svg',
        dark: './src/assets/logo-dark.svg',
        alt: 'Complexity Analyzers',
        replacesTitle: true
      },
      favicon: '/favicon.svg',
      social: [{ icon: 'github', label: 'GitHub', href: repository }],
      editLink: { baseUrl: `${repository}/edit/main/website/` },
      lastUpdated: true,
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        'pt-br': { label: 'Português (Brasil)', lang: 'pt-BR' }
      },
      customCss: ['./src/styles/tokens.css', './src/styles/custom.css'],
      components: { Header: './src/components/Header.astro' },
      routeMiddleware: './src/routeData.ts',
      sidebar: [
        { label: 'Getting started', translations: { 'pt-BR': 'Primeiros passos' }, items: [
          { label: 'Ecosystem overview', translations: { 'pt-BR': 'Visão geral do ecossistema' }, slug: 'getting-started' },
          { label: 'Quick installation', translations: { 'pt-BR': 'Instalação rápida' }, slug: 'getting-started/quick-install' },
          { label: 'First diagnostic', translations: { 'pt-BR': 'Primeiro diagnóstico' }, slug: 'getting-started/first-diagnostic' },
          { label: 'Choose a product', translations: { 'pt-BR': 'Escolha um produto' }, slug: 'getting-started/choose-product' }
        ]},
        { label: 'Learn complexity', translations: { 'pt-BR': 'Aprenda complexidade' }, items: [{ autogenerate: { directory: 'learn' } }] },
        { label: 'Roslyn Analyzer', items: [{ autogenerate: { directory: 'analyzer' } }] },
        { label: 'CLI', items: [{ autogenerate: { directory: 'cli' } }] },
        { label: 'MCP Server', translations: { 'pt-BR': 'Servidor MCP' }, items: [{ autogenerate: { directory: 'mcp' } }] },
        { label: 'Examples', translations: { 'pt-BR': 'Exemplos' }, items: [{ autogenerate: { directory: 'examples' } }] },
        { label: 'Reference', translations: { 'pt-BR': 'Referência' }, items: [{ autogenerate: { directory: 'reference' } }] }
      ]
    })
  ],
  markdown: { shikiConfig: { themes: { light: 'github-light', dark: 'github-dark' } } },
  vite: { build: { cssMinify: 'lightningcss' } }
});

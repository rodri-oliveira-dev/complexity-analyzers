import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

const siteUrl = 'https://rodri-oliveira-dev.github.io/complexity-analyzers/';
const repositoryUrl = 'https://github.com/rodri-oliveira-dev/complexity-analyzers';
const imageUrl = `${siteUrl}social-preview.png`;

type HeadTag = 'link' | 'style' | 'title' | 'base' | 'meta' | 'script' | 'noscript' | 'template';
const tag = (name: HeadTag, attrs: Record<string, string | boolean | undefined>, content?: string) => ({ tag: name, attrs, content });

const sectionNames: Record<string, Record<string, string>> = {
  'getting-started': { en: 'Getting started', 'pt-BR': 'Primeiros passos' },
  learn: { en: 'Learn complexity', 'pt-BR': 'Aprenda complexidade' },
  analyzer: { en: 'Roslyn Analyzer', 'pt-BR': 'Roslyn Analyzer' },
  cli: { en: 'CLI', 'pt-BR': 'CLI' },
  action: { en: 'GitHub Action', 'pt-BR': 'GitHub Action' },
  mcp: { en: 'MCP Server', 'pt-BR': 'Servidor MCP' },
  examples: { en: 'Examples', 'pt-BR': 'Exemplos' },
  reference: { en: 'Reference', 'pt-BR': 'Referência' }
};

export const onRequest = defineRouteMiddleware((context) => {
  const route = context.locals.starlightRoute;
  const { title } = route.entry.data;
  const description = route.entry.data.description ?? title;
  const canonical = new URL(context.url.pathname, siteUrl).href;
  const isNotFound = route.id === '404' || route.id.endsWith('/404');
  const isHomepage = route.id === '' || route.id === 'index' || route.id === 'pt-br' || route.id === 'pt-br/index';
  const socialAlt = route.lang === 'pt-BR'
    ? 'Complexity Analyzers — evidências de complexidade para C#'
    : 'Complexity Analyzers — complexity evidence for C#';

  route.head.push(
    tag('meta', { name: 'robots', content: isNotFound ? 'noindex,follow' : 'index,follow,max-image-preview:large' }),
    tag('meta', { property: 'og:type', content: isHomepage ? 'website' : 'article' }),
    tag('meta', { property: 'og:image', content: imageUrl }),
    tag('meta', { property: 'og:image:type', content: 'image/png' }),
    tag('meta', { property: 'og:image:width', content: '1200' }),
    tag('meta', { property: 'og:image:height', content: '630' }),
    tag('meta', { property: 'og:image:alt', content: socialAlt }),
    tag('meta', { name: 'twitter:card', content: 'summary_large_image' }),
    tag('meta', { name: 'twitter:title', content: title }),
    tag('meta', { name: 'twitter:description', content: description }),
    tag('meta', { name: 'twitter:image', content: imageUrl }),
    tag('meta', { name: 'twitter:image:alt', content: socialAlt })
  );

  if (isNotFound) return;

  const graph: Record<string, unknown>[] = [
    {
      '@type': 'WebSite', '@id': `${siteUrl}#website`, name: 'Complexity Analyzers documentation',
      url: siteUrl, description: 'Bilingual documentation for the Complexity Analyzers C# analysis ecosystem.',
      inLanguage: ['en', 'pt-BR']
    },
    {
      '@type': 'SoftwareSourceCode', '@id': `${siteUrl}#software`, name: 'Complexity Analyzers', url: siteUrl,
      description: 'Open-source C# complexity analysis through Roslyn, a .NET CLI, a local MCP server, and GitHub Actions.',
      programmingLanguage: 'C#', runtimePlatform: '.NET', codeRepository: repositoryUrl,
      license: `${repositoryUrl}/blob/main/LICENSE`, sameAs: [repositoryUrl]
    }
  ];

  if (!isHomepage) {
    const pathWithoutLocale = route.locale ? route.id.replace(new RegExp(`^${route.locale}/?`), '') : route.id;
    const section = pathWithoutLocale.split('/')[0] ?? '';
    const labels = sectionNames[section];
    const home = route.lang === 'pt-BR' ? `${siteUrl}pt-br/` : siteUrl;
    const breadcrumbItems: Record<string, unknown>[] = [
      { '@type': 'ListItem', position: 1, name: 'Complexity Analyzers', item: home }
    ];
    if (labels && pathWithoutLocale !== section) {
      breadcrumbItems.push({ '@type': 'ListItem', position: 2, name: labels[route.lang] ?? labels.en, item: `${home}${section}/` });
    }
    breadcrumbItems.push({ '@type': 'ListItem', position: breadcrumbItems.length + 1, name: title, item: canonical });
    graph.push(
      { '@type': 'BreadcrumbList', '@id': `${canonical}#breadcrumb`, itemListElement: breadcrumbItems },
      {
        '@type': ['Article', 'TechArticle'], '@id': `${canonical}#article`, headline: title, description, url: canonical,
        inLanguage: route.lang, image: imageUrl, isPartOf: { '@id': `${siteUrl}#website` }, about: { '@id': `${siteUrl}#software` }
      }
    );
  }

  route.head.push(tag('script', { type: 'application/ld+json' }, JSON.stringify({ '@context': 'https://schema.org', '@graph': graph }).replaceAll('<', '\\u003c')));
});

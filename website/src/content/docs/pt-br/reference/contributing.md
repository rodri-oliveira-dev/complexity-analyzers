---
title: Contribuição
description: Compile, teste, documente e revise mudanças no Complexity Analyzers.
sidebar:
  order: 4
---

Leia o [guia de contribuição](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CONTRIBUTING.pt-BR.md), o [código de conduta](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CODE_OF_CONDUCT.md) e a [política de segurança](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/SECURITY.md) antes de abrir uma mudança.

## Mudanças de documentação

- Edite páginas editoriais do portal em `website/src/content/docs/` nos dois idiomas.
- Edite material técnico sincronizado em `docs/en/` e `docs/pt-BR/` e execute `npm run sync:docs` em `website/`.
- Mantenha estrutura de rotas e significado técnico equivalentes entre idiomas.
- Não apresente comportamento planejado como implementado.

## Validação

Execute `npm ci` uma vez e depois `npm run validate`. Para mudanças de comportamento no analyzer, CLI ou MCP, execute também os quality gates .NET proporcionais do repositório. Testes automatizados de acessibilidade não substituem revisão com teclado, leitor de tela, zoom e aspectos cognitivos.

---
title: Escolhendo Analyzer, CLI, MCP ou Action
description: Escolha a integração de complexidade para IDE, automação, GitHub CI ou agentes de IA.
sidebar:
  order: 4
---

## Escolha o Analyzer quando

- desenvolvedores precisam de feedback durante edição ou compilação;
- a política deve ficar no `.editorconfig` e nos diagnósticos Roslyn;
- integração zero em runtime é importante.

## Escolha a CLI quando

- você precisa de relatórios de projetos ou soluções em scripts locais, terminais ou pipelines personalizados;
- deseja controlar a instalação da CLI, a comparação de snapshots e o armazenamento dos artefatos;
- precisa de JSON/SARIF, detecção de duplicação, análise de regressões ou quality gates opcionais fora de uma Action reutilizável.

## Escolha a GitHub Action quando

- seu repositório usa GitHub Actions e precisa de verificações reutilizáveis em runners Ubuntu suportados;
- você quer quality gates opcionais, relatórios JSON/SARIF e comparação de regressões em PRs sem manter scripts de instalação da CLI;
- prefere que a Action instale uma versão publicada e fixa da CLI e apresente os resultados no resumo do workflow.

A [Complexity Analysis Action `v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0),
com versionamento independente, foi publicada em 30 de outubro de 2026.
Por padrão, utiliza `ComplexityAnalysis.Tool` `1.0.1`; Analyzer, CLI, MCP e
Action mantêm versões independentes.

Para maior reprodutibilidade, use o commit revisado da release no workflow:
`rodri-oliveira-dev/complexity-analysis-action@2d807ef29ca03c685d224543a135d8c30dfe2c67`.
A tag publicada `@v1.0.0` também está disponível; a referência móvel `@v1`
**ainda não foi criada**. Consulte o [guia da Action](/complexity-analyzers/pt-br/action/)
e os [exemplos publicados](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/v1.0.0/examples). Publicar a GitHub Release não
comprova, por si só, a disponibilidade da listagem no Marketplace.

## Escolha o MCP quando

- um agente local compatível deve descobrir ferramentas de análise;
- o agente precisa da evidência exata para uma identidade estável de membro;
- leituras devem ficar dentro de uma raiz configurada, com limites de tempo, arquivos e bytes.

## Combine de forma deliberada

Um caminho comum usa Analyzer para feedback imediato, CLI para relatórios locais, GitHub Action para checks reutilizáveis no CI e MCP para investigação. A Action invoca a CLI publicada e não reimplementa o motor de análise. Mantenha explícitos os versionamentos de cada produto e as permissões dos workflows.

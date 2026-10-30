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

- você precisa de relatórios do projeto ou solução;
- a CI deve consumir JSON ou SARIF;
- snapshots, comparação de regressões, duplicação ou quality gates opt-in são necessários.

## Escolha a GitHub Action quando

- seu repositório usa GitHub Actions e precisa de verificações reutilizáveis no CI;
- você quer quality gates, SARIF e comparação de regressões em PRs sem manter scripts próprios;
- é possível usar runners Ubuntu e seguir a orientação de SHA completo durante a fase pré-release.

[Conheça a Action](/complexity-analyzers/pt-br/action/). A primeira `v1.0.0` está planejada e ainda não publicada.

## Escolha o MCP quando

- um agente local compatível deve descobrir ferramentas de análise;
- o agente precisa da evidência exata para uma identidade estável de membro;
- leituras devem ficar dentro de uma raiz configurada, com limites de tempo, arquivos e bytes.

## Combine de forma deliberada

Um caminho comum usa Analyzer para feedback imediato, CLI para relatórios locais, GitHub Action para checks reutilizáveis no CI e MCP para investigação. A Action invoca a CLI e não reimplementa o motor. Mantenha versões e permissões explícitas.

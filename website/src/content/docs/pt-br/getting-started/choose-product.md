---
title: Escolhendo Analyzer, CLI ou MCP
description: Selecione o produto adequado para IDE, CI, relatórios ou fluxos com agentes.
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

## Escolha o MCP quando

- um agente local compatível deve descobrir ferramentas de análise;
- o agente precisa da evidência exata para uma identidade estável de membro;
- leituras devem ficar dentro de uma raiz configurada, com limites de tempo, arquivos e bytes.

## Combine de forma deliberada

Um caminho comum usa Analyzer para feedback imediato, CLI para governança agregada e MCP para investigação. Instalar um produto não instala os demais. Mantenha cada configuração explícita e fixe as versões de CLI/MCP em automações.

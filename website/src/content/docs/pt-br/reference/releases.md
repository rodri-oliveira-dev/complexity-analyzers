---
title: Changelog e releases
description: Verifique versões publicadas e diferencie recursos no repositório de disponibilidade de pacotes.
sidebar:
  order: 3
---

Use as [GitHub Releases](https://github.com/rodri-oliveira-dev/complexity-analyzers/releases) e o [`CHANGELOG.md`](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CHANGELOG.md) como histórico de releases.

Os pacotes Analyzer, CLI e MCP são consumíveis de forma independente e podem ter evidências de publicação distintas. Uma versão citada na documentação-fonte só está disponível quando o pacote pode ser verificado no registro correspondente.

## GitHub Action v1.0.0

A [Complexity Analysis Action v1.0.0](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0), com versionamento independente, foi publicada em 30 de outubro de 2026. Consulte o [guia de instalação da Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action#quick-start) para adicioná-la a um workflow do GitHub Actions, ou leia as [notas da release](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0).

A tag `@v1.0.0` aponta para o commit `2d807ef29ca03c685d224543a135d8c30dfe2c67`. Para maior reprodutibilidade, utilize esse SHA completo em `uses:`. A referência móvel `@v1` ainda não foi criada; não a utilize até que o alias seja criado. A existência da GitHub Release não comprova, por si só, a disponibilidade da listagem no Marketplace.

A Action invoca `ComplexityAnalysis.Tool` e não substitui as versões independentes do Analyzer, da CLI ou do MCP.

Para detalhes da publicação MCP `1.0.0` e metadados do registro, consulte a [nota de release](/complexity-analyzers/pt-br/reference/releases/mcp-1-0-0/) sincronizada.

O MCP `1.0.1` está preparado, mas ainda não foi publicado. Sua [nota
sincronizada de preparação](/complexity-analyzers/pt-br/reference/releases/mcp-1-0-1/)
registra o escopo de manutenção e as etapas de publicação que continuam
protegidas.

Este portal está planejado para `https://rodri-oliveira-dev.github.io/complexity-analyzers/`. Não trate essa URL como publicada antes de o workflow do GitHub Pages concluir com sucesso.

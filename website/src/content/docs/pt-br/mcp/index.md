---
title: Servidor MCP local
description: Exponha ferramentas de análise de complexidade somente leitura a agentes locais compatíveis por stdio.
sidebar:
  order: 1
---

`ComplexityAnalysis.Mcp` é uma ferramenta .NET 10 empacotada separadamente. Ela expõe análise a clientes compatíveis por `stdio` local; não é um agente autônomo e nunca edita código-fonte.

## Ferramentas

- `analyze_project` analisa um `.csproj`, `.sln` ou `.slnx` abaixo da raiz configurada e retorna resposta completa ou resumo limitado.
- `query_member_evidence` obtém evidência exata de uma identidade estável, podendo reutilizar uma análise imutável em cache por `analysisId`.
- `compare_snapshots` compara snapshots JSON determinísticos e reporta apenas regressões comprovadas.

## Limite operacional

O host é local, somente stdio e somente leitura. Não possui transporte HTTP, execução de shell, ferramentas de escrita, telemetria ou chamadas de rede durante a análise. Ele rejeita caminhos fora de `--root`, travessia por links/reparse points, entradas sem suporte e trabalhos além dos limites de tempo, bytes e arquivos.

Consulte o guia de [clientes e fluxos com agentes](./agent-workflows/) ou a [referência MCP completa](./reference/) sincronizada.

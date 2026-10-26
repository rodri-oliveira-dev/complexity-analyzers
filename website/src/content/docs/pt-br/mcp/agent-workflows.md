---
title: Configuração de clientes e fluxos com agentes
description: Configure VS Code, Codex e Claude Desktop e use evidências MCP com eficiência.
sidebar:
  order: 2
---

Clientes iniciam o mesmo comando instalado com uma raiz absoluta:

```json
{
  "command": "complexity-mcp",
  "args": ["--root", "/caminho/absoluto/para/o/projeto"]
}
```

## VS Code e GitHub Copilot

Crie `.vscode/mcp.json` com um servidor `stdio` nomeado e `${workspaceFolder}` como raiz. Use **MCP: List Servers** para iniciar ou inspecionar.

## Codex

```bash
codex mcp add complexity-analysis -- complexity-mcp --root /caminho/absoluto/para/o/projeto
codex mcp list
```

## Claude Desktop

Adicione o comando em `mcpServers` no `claude_desktop_config.json` e reinicie completamente o Claude Desktop. Aplicações gráficas podem herdar um `PATH` diferente do shell interativo.

## Investigação eficiente

Para um projeto grande:

1. chame `analyze_project` com `responseMode: "summary"`;
2. inspecione `isTruncated`, `totalMemberCount` e as identidades estáveis;
3. chame `query_member_evidence` com o `analysisId` retornado e o `StableId` escolhido;
4. peça ao agente para separar estimativas comprovadas, hipóteses e razões de `Unknown`.

O cache é limitado e exato. Se uma análise não foi retida ou foi removida, o servidor falha explicitamente em vez de substituir dados possivelmente desatualizados.

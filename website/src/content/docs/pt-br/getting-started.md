---
title: Visão geral do ecossistema
description: Entenda as integrações Roslyn, CLI, MCP e GitHub Action e suas evidências.
sidebar:
  order: 1
---

Complexity Analyzers é um ecossistema de análise para C# com três produtos NuGet independentes e uma GitHub Action mantida separadamente, que utiliza a CLI. Eles compartilham o modelo de análise, mas atuam em diferentes momentos do desenvolvimento.

| Produto | Melhor uso | Quando executa | Saída principal |
| --- | --- | --- | --- |
| `ComplexityAnalysis.Analyzers` | Feedback na IDE e no build | Compilação Roslyn | Diagnósticos `BIG*` |
| `ComplexityAnalysis.Tool` | Revisão, automação e CI | Invocação explícita da CLI | Relatórios console, JSON ou SARIF |
| `ComplexityAnalysis.Mcp` | Fluxos com agentes de IA locais | Cliente MCP invoca uma ferramenta | Análise e evidências estruturadas |
| [Complexity Analysis Action](/complexity-analyzers/pt-br/action/) | Checks reutilizáveis no GitHub CI e nos PRs | Workflow invoca a CLI publicada | Quality gates, relatórios e resumos de regressão |

## O que o ecossistema analisa

O mecanismo compartilhado estima Big-O para um subconjunto documentado de C#, reconhece operações selecionadas de BCL e LINQ, segue chamadas seguras de código-fonte com budgets limitados e resolve famílias selecionadas de recursão direta. Ele também mede complexidade ciclomática e cognitiva, profundidade de aninhamento, tamanho de método e número de parâmetros.

A análise é estática e conservadora. Ela não executa o programa, não mede tráfego de produção e não promete um resultado para todo método C# válido. Quando sintaxe e semântica não oferecem evidência suficiente, o resultado é `Unknown`.

## Requisitos

- O pacote do analyzer tem como alvo `netstandard2.0` e é consumido por hosts Roslyn compatíveis.
- A CLI e o servidor MCP são ferramentas .NET 10 e exigem o runtime .NET 10.
- A Action atualmente suporta runners Ubuntu e instala o .NET 10; sua primeira versão estável v1.0.0 está planejada, mas não publicada.
- A análise de projetos e soluções aceita entradas `.csproj`, `.sln` e `.slnx` conforme documentado.

Continue com a [instalação rápida](/complexity-analyzers/pt-br/getting-started/quick-install/) ou [escolha o produto adequado](/complexity-analyzers/pt-br/getting-started/choose-product/).

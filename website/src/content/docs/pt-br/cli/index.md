---
title: CLI de projeto
description: Gere relatórios determinísticos em console, JSON e SARIF para projetos e soluções C#.
sidebar:
  order: 1
---

`ComplexityAnalysis.Tool` é uma ferramenta .NET 10 versionada separadamente. Ela analisa entradas `.csproj`, `.sln` e `.slnx` quando invocada explicitamente e produz relatórios agregados sem alterar o comportamento do analyzer nos builds.

```bash
complexity-analysis analyze MySolution.slnx
complexity-analysis analyze MyProject.csproj --format json --output report.json
complexity-analysis analyze MyProject.csproj --format sarif --output report.sarif
```

## Recursos

- relatórios determinísticos em console, JSON e SARIF 2.1.0;
- snapshots e comparação sem exigir Git;
- gates opt-in para Big-O, métricas estruturais, tamanho, parâmetros e duplicação;
- detecção normalizada de código C# duplicado;
- exclusões explícitas e códigos de saída estáveis.

Nenhum threshold falha o build por padrão. O código `0` significa sucesso e gates configurados aprovados, `1` indica entrada/carregamento/relatório inválido, `2` indica falha em gate habilitado e `130` indica cancelamento.

Use a [referência completa da CLI](./reference/) para argumentos e contratos exatos e o guia de [GitHub Actions](./github-actions/) para integração na CI.

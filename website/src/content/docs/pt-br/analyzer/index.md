---
title: Roslyn Analyzer
description: Adicione diagnósticos conservadores de complexidade a builds e análise de IDE em C#.
sidebar:
  order: 1
---

`ComplexityAnalysis.Analyzers` executa dentro do Roslyn durante compilação e análise na IDE. O projeto tem como alvo `netstandard2.0`, não adiciona dependência em runtime às aplicações consumidoras e só reporta dentro dos limites documentados da análise estática.

## Modelo de integração

Instale o pacote como analyzer de desenvolvimento. Hosts suportados carregam seu assembly a partir de `analyzers/dotnet/cs/` no pacote. O código da aplicação nunca chama o analyzer.

O analyzer habilita execução concorrente quando segura, ignora código gerado de forma explícita, respeita cancelamento e evita rede ou I/O em hot paths.

## Famílias de diagnósticos

- `BIG0001` é uma estimativa opt-in de complexidade conhecida.
- `BIG1001`–`BIG1006` cobrem padrões custosos, recursão exponencial e threshold Big-O configurado.
- `BIG2001`–`BIG2007` cobrem complexidade ciclomática, aninhamento, tamanho, parâmetros e threshold de complexidade cognitiva.
- `BIG9000` é uma sonda de infraestrutura desabilitada, não uma recomendação de performance.

Continue com [instalação e compatibilidade](./installation/), o [catálogo de diagnósticos](./diagnostics/) ou a [configuração por .editorconfig](./configuration/).

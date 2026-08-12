---
name: coverage-analysis
description: Use esta skill para analisar cobertura e confianca comportamental do analyzer, parsers, walkers, visitors, analise sintatica, analise semantica, diagnostics, false positives, false negatives, edge cases e regressao de performance.
---

# Coverage Analysis

## Objetivo

Usar cobertura como sinal de confianca comportamental para o analyzer, nao como meta cosmetica de percentual.

## Quando usar

- O usuario mencionar coverage, cobertura, gaps, hotspots ou risco de regressao.
- Um PR alterar logica de parsing, walking, semantic model, diagnostics ou heuristicas de complexidade.
- Houver duvida sobre false positives, false negatives ou edge cases.
- For necessario priorizar testes antes de portar logica do projeto original.

## Regras obrigatorias

- Nao alterar testes apenas para inflar percentual.
- Nao aceitar teste sem assert significativo como melhoria real.
- Nao reduzir threshold para contornar falha sem instrucao explicita.
- Nao adicionar pacote com `Version=` em `PackageReference`.
- Cobertura deve representar comportamento validado.
- Ao portar logica do projeto original, criar testes de caracterizacao.

## Hotspots de maior risco

- Diagnostics e severidades.
- Sintaxe C# complexa, incluindo loops, LINQ, recursao e fluxos condicionais.
- Uso de `SemanticModel`.
- Generated code e codigo parcial.
- Cancelamento e analise concorrente.
- False positives de alta severidade.
- False negatives em padroes comuns.
- Regressao de performance em hot paths.

## Processo

1. Identifique a area alterada e o comportamento publico esperado.
2. Leia testes proximos antes de propor ferramenta nova.
3. Classifique gaps por risco real, nao por linhas descobertas.
4. Priorize casos que distinguem diagnostico correto, ausencia correta de diagnostico e resultado inconclusivo.
5. Inclua cenarios negativos quando false positives forem provaveis.
6. Para logica portada, registre origem e cubra comportamento observado.
7. Valide com comandos proporcionais ao escopo.

## Saida esperada

- Diagnostico objetivo dos gaps.
- Lista priorizada de comportamentos sem cobertura suficiente.
- Separacao entre cobertura baixa aceitavel e cobertura baixa arriscada.
- Validacoes executadas ou motivo para nao executar.

---
title: Modelos de complexidade
description: Diferencie Big-O, tempo e espaço, complexidade ciclomática e cognitiva, aninhamento, tamanho e Halstead.
sidebar:
  order: 2
---

## Big-O e complexidade temporal

Big-O expressa como o trabalho cresce em relação ao tamanho da entrada. `O(1)`, `O(log n)`, `O(n)`, `O(n log n)` e `O(n²)` descrevem classes de crescimento, não duração em cronômetro. O analyzer compõe limites de loops suportados, operações conhecidas, chamadas seguras de código-fonte e recorrências selecionadas.

Estimativas também carregam garantias e hipóteses. Uma busca em hash pode ser `O(1)` esperada com códigos hash bem distribuídos e hashing/igualdade de custo constante; isso não é uma promessa incondicional de pior caso.

## Complexidade espacial

Os diagnósticos públicos e relatórios atuais focam estimativas de tempo algorítmico e métricas estruturais/de tamanho. Eles não oferecem uma estimativa pública geral de espaço auxiliar. Operações com alocação, como materialização, podem ser reconhecidas como custosas, mas isso não equivale a uma análise completa de complexidade espacial.

## Métricas estruturais

Complexidade ciclomática é independente de Big-O: um método de tempo constante pode conter muitos desvios. Complexidade cognitiva adiciona penalidades por quebras estruturais e aninhamento conforme a convenção documentada. Profundidade máxima mede profundidade, não quantidade de caminhos.

## Tamanho e Halstead

NLOC, quantidade de statements, tokens e parâmetros são medidas de tamanho configuráveis por threshold. Métricas Halstead existem internamente como base para tooling e relatórios, mas ainda não há diagnóstico público de threshold Halstead.

Nenhuma métrica isolada comprova manutenibilidade. Use sinais combinados e examine o código e a evidência de cada resultado.

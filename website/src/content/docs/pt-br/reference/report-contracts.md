---
title: Contratos de relatórios
description: Entenda saídas em console, JSON, SARIF, snapshots, comparação e evidências.
sidebar:
  order: 2
---

## Console

O relatório legível padrão serve para inspeção local. Não interprete seu texto de apresentação como contrato de automação.

## JSON e snapshots

JSON é a base de relatórios legíveis por máquina e snapshots. Ele preserva identidades estáveis de membros, status conhecido ou `Unknown`, métricas estruturais/de tamanho independentes, hipóteses e evidências quando o contrato escolhido as inclui. O modo snapshot normaliza dados voláteis para comparação determinística.

Consumidores devem validar `schemaVersion` e tolerar campos aditivos documentados em vez de depender da ordem incidental de propriedades.

## SARIF

A saída SARIF usa SARIF 2.1.0 para consumidores compatíveis de code scanning. Localizações são relativas ao repositório quando possível. Um arquivo SARIF é transporte de relatório, não prova de que todo resultado é um diagnóstico do compilador.

## Comparação

A comparação usa identidades estáveis e ordena somente expressões Big-O conhecidas e compatíveis. Evidência desconhecida ou incompatível permanece incomparável. `--fail-on-regression` muda o código de saída apenas para regressões comprovadas.

Para nomes de campos, padrões e exemplos atuais exatos, use a [referência da CLI](/complexity-analyzers/pt-br/cli/reference/) sincronizada do documento-fonte.

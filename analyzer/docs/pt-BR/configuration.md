# Configuracao

[English](../en/configuration.md) | Portugues (Brasil)

`ComplexityAnalysis.Analyzers` usa a configuracao padrao de severidade de diagnostics Roslyn. A Phase 5 nao define opcoes customizadas do analyzer.

## Formato .editorconfig

Use o formato padrao por regra:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Valores comuns incluem:

```text
none
silent
suggestion
warning
error
default
```

O compilador e o SDK determinam o comportamento exato do build para cada severidade configurada.

## Padroes

| ID | Severidade padrao | Habilitado por padrao |
| --- | --- | --- |
| `BIG0001` | `Info` | `false` |
| `BIG1001` | `Info` | `true` |
| `BIG1002` | `Info` | `true` |
| `BIG1003` | `Info` | `true` |
| `BIG1004` | `Info` | `true` |
| `BIG9000` | `Info` | `false` |

## Visibilidade Local Recomendada

`BIG0001` e informational e desabilitado por padrao. Habilite quando quiser que o analyzer mostre estimativas conhecidas de metodos:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Promova diagnostics acionaveis quando quiser ve-los no build:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
```

Mantenha o probe de infraestrutura desabilitado em projetos normais:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Habilite o probe apenas em testes de consumo de pacote ou smoke tests de CI:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

## Desabilitar Regras

Use `none` para qualquer regra que voce nao queira reportar:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = none
dotnet_diagnostic.BIG1001.severity = none
dotnet_diagnostic.BIG1002.severity = none
dotnet_diagnostic.BIG1003.severity = none
dotnet_diagnostic.BIG1004.severity = none
dotnet_diagnostic.BIG9000.severity = none
```

## O Que Nao E Configuravel

A Phase 5 nao expoe opcoes para:

- thresholds de Big-O;
- mappings customizados de operacoes;
- mudar comportamento de mappings BCL ou LINQ;
- tratamento de recursao;
- analise de call graph;
- limites de profundidade ou budget de metodos da analise interprocedural;
- code fixes;
- complexidade de memoria, paralela ou probabilistica.

Operacoes nao suportadas ou nao resolvidas continuam como `Unknown`; nao ha opcao para converte-las em uma classe de complexidade conhecida.

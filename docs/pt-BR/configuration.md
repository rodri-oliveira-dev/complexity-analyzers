# Configuracao

[English](../en/configuration.md) | Portugues (Brasil)

`ComplexityAnalysis.Analyzers` usa duas camadas de configuracao:

- opcoes `complexity_analyzers.*` controlam comportamento do analyzer;
- `dotnet_diagnostic.<RULE_ID>.severity` controla severidade de diagnostics Roslyn.

As opcoes de comportamento sao lidas por APIs Roslyn de analyzer config. O analyzer nao faz parsing manual de arquivos `.editorconfig`. As opcoes podem ser globais ou por arquivo; quando as duas existem, a opcao especifica do arquivo vence para aquela syntax tree.

Valores invalidos sao seguros: nao quebram o build e nao reportam falha do analyzer. O analyzer volta para o default documentado daquela opcao.

## Opcoes do Analyzer

| Opcao | Tipo | Default | Valores permitidos | Proposito |
| --- | --- | --- | --- | --- |
| `complexity_analyzers.interprocedural_analysis` | Boolean | `true` | `true`, `false` | Habilita expansao de metodos fonte suportados na mesma compilation. |
| `complexity_analyzers.recursion_analysis` | Boolean | `true` | `true`, `false` | Habilita extracao de recursao direta suportada e solucao de recorrencias. |
| `complexity_analyzers.max_call_depth` | Integer | `5` | `0` a `16` | Limita a profundidade de expansao de metodos fonte. |
| `complexity_analyzers.max_methods_per_root` | Integer | `32` | `0` a `128` | Limita expansoes nao cacheadas de metodos fonte por analise de metodo raiz. |
| `complexity_analyzers.maximum_complexity` | String | `none` | `none`, `constant`, `log_n`, `n`, `n_log_n`, `n2`, `n3`, `exponential`, `factorial` | Habilita `BIG1006` quando uma estimativa conhecida e comparavel excede esse threshold. |

Valores booleanos ignoram maiusculas/minusculas depois de remover espacos nas extremidades. Valores inteiros precisam ser inteiros decimais nao negativos, sem sinal, ponto decimal, separadores ou espacos internos. Valores de threshold sao case-sensitive.

Valores fora dos limites publicos de budget voltam para o default: `max_call_depth = 5` e `max_methods_per_root = 32`.

## Exemplo

```ini
[*.cs]

complexity_analyzers.interprocedural_analysis = true
complexity_analyzers.recursion_analysis = true
complexity_analyzers.max_call_depth = 5
complexity_analyzers.max_methods_per_root = 32
complexity_analyzers.maximum_complexity = n_log_n

dotnet_diagnostic.BIG1006.severity = warning
```

## Comportamento do Threshold

`complexity_analyzers.maximum_complexity` e opt-in. O default `none` significa que `BIG1006` nao reporta.

`BIG1006` reporta apenas quando todos estes pontos sao verdadeiros:

- a complexidade do metodo e conhecida;
- o threshold configurado nao e `none`;
- a estimativa e o threshold sao comparaveis pelo modelo atual do analyzer;
- a estimativa e maior que o threshold.

Complexidade `Unknown` nao produz diagnostic de threshold. Complexidade multivariada incomparavel, como uma expressao sobre variaveis independentes, tambem pode nao produzir diagnostic de threshold. `BIG1006` e um sinal pratico do analyzer, nao uma prova matematica universal.

Exemplos:

| Estimativa real | Threshold | Resultado |
| --- | --- | --- |
| `O(n^2)` | `n_log_n` | Reporta `BIG1006`. |
| `O(n log n)` | `n_log_n` | Nao reporta. |
| `O(n)` | `n_log_n` | Nao reporta. |
| `Unknown` | `n` | Nao reporta. |
| Expressao multivariada incomparavel | `n2` | Nao reporta. |

## Feature Flags

`complexity_analyzers.interprocedural_analysis = false` impede expansao para callees fonte suportados. Analise intraprocedural e operacoes BCL/LINQ suportadas continuam ativas.

`complexity_analyzers.recursion_analysis = false` impede extracao e solucao de recorrencias de recursao direta, incluindo `BIG1005`. Analise intraprocedural nao recursiva e expansao de metodos fonte nao recursivos ainda podem rodar quando a analise interprocedural esta habilitada.

## Budgets

O analyzer e limitado por opcoes publicas e hard limits:

- profundidade de chamada default: `5`;
- profundidade maxima configuravel: `16`;
- metodos fonte por raiz default: `32`;
- maximo configuravel de metodos fonte por raiz: `128`.

Ao atingir uma fronteira de budget, as chamadas afetadas ficam conservadoras, normalmente `Unknown`. Um budget pequeno e util para smoke tests ou consumidores muito cautelosos, mas pode reduzir a cobertura de `BIG0001`, `BIG1004` e `BIG1006` em codigo com muitas chamadas fonte.

## Severidade de Diagnostics

Use o formato Roslyn padrao por regra:

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

O compilador e o SDK determinam o comportamento exato do build para cada severidade.

## Defaults de Diagnostics

| ID | Severidade default | Habilitado por default |
| --- | --- | --- |
| `BIG0001` | `Info` | `false` |
| `BIG1001` | `Info` | `true` |
| `BIG1002` | `Info` | `true` |
| `BIG1003` | `Info` | `true` |
| `BIG1004` | `Info` | `true` |
| `BIG1005` | `Info` | `true` |
| `BIG1006` | `Info` | `true` |
| `BIG9000` | `Info` | `false` |

`BIG1006` e habilitado por default como descriptor, mas fica funcionalmente inativo ate que `complexity_analyzers.maximum_complexity` seja configurado com um threshold concreto.

## Configuracoes Comuns

Habilite estimativas por metodo:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Promova diagnostics acionaveis e threshold checks:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
dotnet_diagnostic.BIG1005.severity = warning
complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

Prove temporariamente que o pacote carregou:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Mantenha o probe de infraestrutura desabilitado em projetos normais:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## O Que Nao E Configuravel

O analyzer nao expoe opcoes para mappings customizados de operacoes, comportamento de mappings BCL/LINQ, selecao de familias de recorrencia, tolerancias de teoremas, analise de solution inteira, code fixes, complexidade de memoria, complexidade paralela ou complexidade probabilistica.

Operacoes nao suportadas ou nao resolvidas continuam `Unknown`; nao ha opcao para converte-las em uma classe de complexidade conhecida.

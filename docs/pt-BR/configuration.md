# Configuração

[English](../en/configuration.md) | Português (Brasil)

`ComplexityAnalysis.Analyzers` usa duas camadas de configuração:

- opções `complexity_analyzers.*` controlam o comportamento do analyzer;
- `dotnet_diagnostic.<RULE_ID>.severity` controla a severidade dos diagnósticos Roslyn.

As opções de comportamento são lidas pelas APIs de analyzer config do Roslyn. O analyzer não faz parsing manual de arquivos `.editorconfig`. As opções podem ser globais ou específicas por arquivo; quando as duas existem, o valor específico do arquivo prevalece para aquela syntax tree.

Valores inválidos são tratados com segurança: não quebram o build nem geram falha do analyzer. O analyzer retorna ao valor padrão documentado para aquela opção.

## Opções do analyzer

| Opção | Tipo | Padrão | Valores permitidos | Finalidade |
| --- | --- | --- | --- | --- |
| `complexity_analyzers.interprocedural_analysis` | Boolean | `true` | `true`, `false` | Habilita expansão de métodos-fonte suportados na mesma compilation. |
| `complexity_analyzers.recursion_analysis` | Boolean | `true` | `true`, `false` | Habilita extração de recursão direta suportada e resolução de recorrências. |
| `complexity_analyzers.max_call_depth` | Integer | `5` | `0` até `16` | Limita a profundidade de expansão de métodos-fonte. |
| `complexity_analyzers.max_methods_per_root` | Integer | `32` | `0` até `128` | Limita expansões não cacheadas de métodos-fonte por análise de método raiz. |
| `complexity_analyzers.maximum_complexity` | String | `none` | `none`, `constant`, `log_n`, `n`, `n_log_n`, `n2`, `n3`, `exponential`, `factorial` | Habilita `BIG1006` quando uma estimativa conhecida e comparável excede esse threshold. |
| `complexity_analyzers.maximum_cyclomatic_complexity` | Integer | sem valor | Inteiro decimal positivo | Habilita `BIG2001` quando a Cyclomatic Complexity de um executable member suportado excede esse threshold. |
| `complexity_analyzers.cyclomatic_complexity_mode` | String | `standard` | `standard`, `modified_mccabe` | Seleciona a contabilização de `switch` para Cyclomatic Complexity. |

Valores booleanos ignoram diferenças entre maiúsculas e minúsculas depois da remoção de espaços nas extremidades. Valores inteiros devem ser números decimais não negativos, sem sinal, ponto decimal, separadores ou espaços internos. Valores de threshold são case-sensitive.

Valores fora dos limites públicos de budget retornam ao padrão: `max_call_depth = 5` e `max_methods_per_root = 32`. Thresholds ciclomáticos inválidos retornam para sem valor configurado. Modos ciclomáticos inválidos retornam para `standard`.

## Exemplo

```ini
[*.cs]

complexity_analyzers.interprocedural_analysis = true
complexity_analyzers.recursion_analysis = true
complexity_analyzers.max_call_depth = 5
complexity_analyzers.max_methods_per_root = 32
complexity_analyzers.maximum_complexity = n_log_n
complexity_analyzers.maximum_cyclomatic_complexity = 10
complexity_analyzers.cyclomatic_complexity_mode = standard

dotnet_diagnostic.BIG1006.severity = warning
dotnet_diagnostic.BIG2001.severity = warning
```

## Comportamento do threshold

`complexity_analyzers.maximum_complexity` é opt-in. O valor padrão `none` significa que `BIG1006` não reporta.

`BIG1006` reporta somente quando todos estes pontos são verdadeiros:

- a complexidade do método é conhecida;
- o threshold configurado não é `none`;
- a estimativa e o threshold são comparáveis pelo modelo atual do analyzer;
- a estimativa é maior que o threshold.

Complexidade `Unknown` não produz diagnóstico de threshold. Complexidade multivariada incomparável, como uma expressão sobre variáveis independentes, também pode não produzir diagnóstico. `BIG1006` é um sinal prático de análise estática, não uma prova matemática universal.

Exemplos:

| Estimativa real | Threshold | Resultado |
| --- | --- | --- |
| `O(n^2)` | `n_log_n` | Reporta `BIG1006`. |
| `O(n log n)` | `n_log_n` | Não reporta. |
| `O(n)` | `n_log_n` | Não reporta. |
| `Unknown` | `n` | Não reporta. |
| Expressão multivariada incomparável | `n2` | Não reporta. |

## Comportamento de Cyclomatic Complexity

`complexity_analyzers.maximum_cyclomatic_complexity` é opt-in. Quando não está
configurado ou é inválido, `BIG2001` não reporta. Valores válidos são inteiros
decimais positivos. Igualdade não reporta; apenas valor real estritamente maior
gera diagnóstico.

Cyclomatic Complexity é complexidade estrutural de caminhos de controle, não
complexidade algorítmica de tempo. Um membro pode ser `O(n)` e, ao mesmo tempo,
ter Cyclomatic Complexity `12`. `BIG2001` não afeta `BIG0001`, `BIG1006`, análise
interprocedural, análise de recursão nem `maximum_complexity`.

A convenção standard usa baseline `1 + decision points` e conta constructs
documentados como `if`, loops, `catch`, `?:`, short-circuit `&&`/`||`, cases/arms
de `switch`, guards `when` e patterns `or`. No modo `modified_mccabe`, cada
switch statement ou switch expression contribui uma decisão para a família de
`switch` em vez de uma por case/arm não default; guards e patterns `or` continuam
sendo contados separadamente.

Exemplos:

| Valor real | Threshold | Resultado |
| --- | --- | --- |
| `9` | `10` | Não reporta. |
| `10` | `10` | Não reporta. |
| `11` | `10` | Reporta `BIG2001`. |

## Feature flags

`complexity_analyzers.interprocedural_analysis = false` impede expansão para callees fonte suportados. A análise intraprocedural e a análise de operações BCL/LINQ suportadas permanecem ativas.

`complexity_analyzers.recursion_analysis = false` impede extração e resolução de recorrências de recursão direta, incluindo `BIG1005`. Análise intraprocedural não recursiva e expansão de métodos-fonte não recursivos ainda podem executar quando a análise interprocedural está habilitada.

## Budgets de análise

O analyzer é limitado por opções públicas e hard limits:

- profundidade de chamada padrão: `5`;
- profundidade máxima configurável: `16`;
- métodos-fonte por raiz padrão: `32`;
- máximo configurável de métodos-fonte por raiz: `128`.

Ao atingir uma fronteira de budget, as chamadas afetadas permanecem conservadoras, normalmente `Unknown`. Um budget pequeno pode ser útil em smoke tests ou para consumidores mais cautelosos, mas pode reduzir a cobertura de `BIG0001`, `BIG1004` e `BIG1006` em código com muitas chamadas a métodos-fonte.

## Severidade dos diagnósticos

Use o formato padrão do Roslyn por regra:

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

## Valores padrão dos diagnósticos

| ID | Severidade padrão | Habilitado por padrão |
| --- | --- | --- |
| `BIG0001` | `Info` | `false` |
| `BIG1001` | `Info` | `true` |
| `BIG1002` | `Info` | `true` |
| `BIG1003` | `Info` | `true` |
| `BIG1004` | `Info` | `true` |
| `BIG1005` | `Info` | `true` |
| `BIG1006` | `Info` | `true` |
| `BIG2001` | `Info` | `true` |
| `BIG9000` | `Info` | `false` |

`BIG1006` é habilitado por padrão como descriptor, mas permanece funcionalmente inativo até que `complexity_analyzers.maximum_complexity` seja configurado com um threshold concreto.

`BIG2001` é habilitado por padrão como descriptor, mas permanece funcionalmente inativo até que `complexity_analyzers.maximum_cyclomatic_complexity` seja configurado com um threshold concreto.

## Configurações comuns

Habilite estimativas por método:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Promova diagnósticos acionáveis e checks de threshold:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
dotnet_diagnostic.BIG1002.severity = warning
dotnet_diagnostic.BIG1003.severity = warning
dotnet_diagnostic.BIG1004.severity = warning
dotnet_diagnostic.BIG1005.severity = warning
complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
complexity_analyzers.maximum_cyclomatic_complexity = 10
complexity_analyzers.cyclomatic_complexity_mode = modified_mccabe
dotnet_diagnostic.BIG2001.severity = warning
```

Comprove temporariamente que o pacote carregou:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Mantenha o probe de infraestrutura desabilitado em projetos normais:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## O que não é configurável

O analyzer não expõe opções para mappings customizados de operações, regras customizadas de decision points ciclomáticos além do modo de `switch` documentado, comportamento dos mappings BCL/LINQ, seleção de famílias de recorrência, tolerâncias de teoremas, análise de solution inteira, code fixes, complexidade de memória, complexidade paralela ou complexidade probabilística.

Operações não suportadas ou não resolvidas permanecem `Unknown`; não existe opção para convertê-las em uma classe de complexidade conhecida.

# Convencao de Cognitive Complexity

[English](../en/cognitive-complexity.md) | Portugues (Brasil)

Este documento define a convencao de Cognitive Complexity C# de
`ComplexityAnalysis.Analyzers`. Ela e uma convencao documentada do projeto para
estimar o custo de compreensao estrutural de um executable member suportado. Nao
ha alegacao de equivalencia exata com ferramentas externas.

Cognitive Complexity e independente de Big-O, Cyclomatic Complexity, Maximum
Control-Flow Nesting Depth, NLOC, statement count, token count, Parameter Count,
metricas Halstead e qualquer maintainability index futuro. Os valores nao sao
combinados em um unico score.

## Baseline

Codigo executavel straight-line possui Cognitive Complexity `0`.

```csharp
int Add(int left, int right)
{
    return left + right;
}
```

O score esperado e `0`.

## Modelo de Score

O calculator percorre apenas o body pertencente ao executable member atual.
Local functions, lambdas e anonymous methods aninhados sao fronteiras de
executable member e sao pontuados independentemente.

Para cada quebra estrutural de fluxo de controle:

```text
incremento = 1 + nesting atual de fluxo de controle
```

O nesting atual inicia em `0`. Quando um construct estrutural possui body
aninhado, esse body e visitado com `nesting atual + 1`. Branches irmaos reutilizam
o mesmo nesting e nao acumulam a profundidade uns dos outros.

O score usa soma saturada de inteiros.

## Regras Estruturais

| Construct | Incremento estrutural | Penalidade de nesting |
| --- | --- | --- |
| `if` | `+1` | `+ nesting atual` |
| `else if` | `+1` | `+ nesting atual`; sem nesting artificial pela cadeia |
| `else` | `+1` | Nenhuma |
| `for` | `+1` | `+ nesting atual` |
| `foreach` / `foreach var` | `+1` | `+ nesting atual` |
| `while` | `+1` | `+ nesting atual` |
| `do` | `+1` | `+ nesting atual` |
| statement `switch` | `+1` pela familia do switch | `+ nesting atual`; cases sao branches irmaos |
| switch expression | `+1` pela familia do switch | `+ nesting atual`; arms sao branches irmaos |
| `catch` | `+1` por catch clause | `+ nesting atual`; catches sao irmaos |
| guard `when` / filtro de catch | `+1` | `+ nesting atual` onde o guard aparece |
| expressao condicional `?:` | `+1` | `+ nesting atual` |
| sequencia booleana `&&` / `||` | `+1` pela primeira sequencia logica, mais `+1` por mudanca de operador | Nenhuma |
| sequencia de pattern `and` / `or` | `+1` pela primeira sequencia logica de pattern, mais `+1` por mudanca de operador | Nenhuma |
| chamada direta recursiva a si mesmo | `+1` uma vez por membro quando comprovada por identidade de simbolo | Nenhuma |
| `break`, `continue`, `goto`, `goto case`, `goto default` | `+1` por statement | Nenhuma |

## `if`, `else if` E `else`

Um `if` e uma quebra estrutural. Um `else if` tambem e uma quebra estrutural, mas
e avaliado no mesmo nesting do `if` original; a forma sintatica nao torna a
cadeia mais profunda por si so. Um `else` final adiciona `1` por ser um branch
alternativo, mas nao recebe penalidade de nesting.

Bodies dentro da cadeia continuam sendo visitados em um nivel de nesting mais
profundo.

## Loops

`for`, `foreach`, `foreach var`, `while` e `do` adicionam `1 + nesting atual`.
Seus bodies sao visitados em um nivel de nesting mais profundo. Condicoes e
expressoes de iteracao sao visitadas no nesting atual e podem adicionar custo de
sequencia booleana.

## Switch

Um statement `switch` contribui uma vez pela familia do switch. Labels `case`,
labels de pattern e `default` nao adicionam por si mesmos. Cada section do
switch e um branch irmao. Fluxo de controle dentro de uma section e visitado em
um nivel de nesting mais profundo.

Uma switch expression segue a mesma politica: a expressao contribui uma vez,
arms nao adicionam por si mesmos, e cada expressao de arm e visitada em um nivel
de nesting mais profundo.

Labels e arms com patterns ainda podem adicionar custo de sequencia de pattern.
Guards `when` adicionam incremento de guard.

## `try`, `catch` E `finally`

`try` e `finally` nao adicionam score por si mesmos. Cada clause `catch`
adiciona um incremento estrutural e seu bloco e visitado em um nivel de nesting
mais profundo. Multiplos catches sao branches irmaos. Um filtro de catch adiciona
o mesmo custo de guard que `when`.

## Expressoes Condicionais

`condition ? whenTrue : whenFalse` adiciona `1 + nesting atual`. A condicao e
visitada no nesting atual. As duas expressoes de resultado sao visitadas em um
nivel de nesting mais profundo, entao ternarios aninhados recebem penalidades
locais de nesting.

## Sequencias Booleanas

Cadeias booleanas short-circuit sao contadas como quebras de compreensao dentro
de condicoes e expressoes:

| Expressao | Custo da sequencia booleana |
| --- | --- |
| `a && b` | `1` |
| `a && b && c` | `1` |
| `a || b || c` | `1` |
| `a && b || c` | `2` |
| `(a && b) || (c && d)` | `3` |

Parenteses sozinhos nao adicionam score. Eles so importam quando a estrutura
Roslyn subjacente muda a sequencia de operadores logicos encontrada.

## Patterns E Guards

Patterns `and` e `or` usam a mesma politica de sequencia de `&&` e `||`.
`not`, relational patterns, property patterns, list patterns, declaration
patterns, constant patterns, discard patterns e var patterns nao adicionam por
si mesmos, embora seus subpatterns aninhados continuem sendo inspecionados.

Guards `when` em labels de switch e arms de switch expression, alem de filtros de
catch, adicionam `1 + nesting atual`. A expressao do guard e entao inspecionada
para custo de sequencia booleana.

## Recursao

Recursao direta a si mesmo adiciona `1` uma vez por executable member quando o
alvo da chamada e comprovado com identidade de simbolo Roslyn. O calculator nao
usa nomes textuais de metodos e nao faz analise de call graph do projeto inteiro.
Recursao mutua fica fora da convencao de Cognitive Complexity.

Roots de lambda, anonymous method e propriedade expression-bodied nao participam
atualmente da pontuacao de recursao direta.

## Jumps E Exclusoes

Statements `break`, `continue` e `goto` adicionam `1` porque interrompem o fluxo
linear local. `goto case` e `goto default` seguem a mesma regra.

Os constructs abaixo sao deliberadamente excluidos, exceto quando contem outro
construct contado:

- `return`;
- statements `throw` e throw expressions;
- `await`;
- `yield return` e `yield break`;
- `lock`, `using`, `fixed`, `checked` e `unchecked`;
- blocos lexicos simples;
- initializers de objeto, collection, array, propriedade e anonymous object;
- member access, invocacao, atribuicao, aritmetica, null-coalescing e expressoes
  null-conditionais;
- comentarios e whitespace.

## Threshold E Diagnostics

`complexity_analyzers.maximum_cognitive_complexity` e um threshold inteiro nao
negativo opt-in. Configuracao ausente ou invalida deixa o threshold sem valor e
nao produz diagnostico.

`BIG2007` reporta somente quando:

- o executable member e suportado e possui body;
- o threshold esta configurado com um inteiro nao negativo valido;
- a Cognitive Complexity real e estritamente maior que o threshold.

Valores abaixo do threshold e iguais ao threshold nao reportam.

A localizacao do diagnostico e a localizacao estavel do executable member.
Diagnostic properties incluem `cognitiveComplexity` e `threshold`.

## Exemplo Passo A Passo

```csharp
void M(bool a, bool b, bool c)
{
    if (a)
    {
        while (b)
        {
            if (c)
            {
            }
        }
    }
}
```

| Construct | Incremento base | Incremento de nesting | Subtotal |
| --- | --- | --- | --- |
| `if` externo no nesting `0` | `1` | `0` | `1` |
| `while` no nesting `1` | `1` | `1` | `2` |
| `if` interno no nesting `2` | `1` | `2` | `3` |
| Total |  |  | `6` |

Decisoes irmas flat sao mais baratas:

```csharp
if (a) {}
if (b) {}
if (c) {}
```

Cada `if` esta no nesting `0`, entao o total e `3`. O exemplo aninhado acima e
maior porque o nesting local aumenta o custo de compreensao.

## Limitacoes

Cognitive Complexity nao mede complexidade de runtime, complexidade de memoria,
qualidade de dominio, legibilidade subjetiva, estilo de formatacao, volume
Halstead ou qualidade de design de API. Ela nao e automaticamente equivalente a
nenhuma ferramenta externa e nao prescreve refatoracao automatica.

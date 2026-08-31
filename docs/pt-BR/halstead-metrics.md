# Métricas Halstead para C#

[English](../en/halstead-metrics.md) | Português (Brasil)

`ComplexityAnalysis.Analyzers` define uma capacidade interna de métricas
Halstead específica para C# em executable members suportados. A implementação
usa a mesma abstração de executable member das regras públicas do analyzer, então
ownership de membro, isolamento de executáveis aninhados, política de código
gerado, cancelamento e execução concorrente preservam as mesmas fronteiras
arquiteturais.

Ainda não há diagnóstico Halstead público nem threshold de `.editorconfig`. O
projeto não possui, neste momento, uma única métrica Halstead derivada com
threshold de manutenibilidade suficientemente claro e defensável para projetos
C# em geral. Adicionar uma regra `BIG2xxx` apenas por simetria deixaria o
contrato público do analyzer mais ruidoso sem guidance sustentado por evidência.

## Contagens Primitivas

As contagens primitivas são:

```text
n1 = operadores distintos
n2 = operandos distintos
N1 = total de operadores
N2 = total de operandos
```

As contagens são baseadas no código-fonte, determinísticas e limitadas a um
executable member suportado. Comentários, espaços em branco, diretivas de
pré-processador e trivia de sintaxe não contribuem.

## Métricas Derivadas

A convenção do projeto deriva:

```text
vocabulário                 n = n1 + n2
comprimento                 N = N1 + N2
comprimento calculado       N^ = n1 * log2(n1) + n2 * log2(n2)
volume                      V = N * log2(n)
dificuldade                 D = (n1 / 2) * (N2 / n2)
esforço                     E = D * V
tempo estimado de implementação T = E / 18
bugs entregues estimados    B = V / 3000
```

Entradas degeneradas são tratadas explicitamente para que membros vazios ou
triviais produzam valores finitos e não negativos. A formatação usa cultura
invariante e formato round-trip `G17` para texto numérico determinístico.

## Classificação

Operadores são constructs de sintaxe C# que executam, selecionam, invocam,
acessam, criam, transferem controle ou alteram o significado executável do
código. Pontuação não é contada mecanicamente; ela só conta quando faz parte de
uma operação documentada.

A convenção inclui operadores aritméticos, comparação, igualdade, lógicos,
bitwise, atribuição, null-coalescing, acesso null-conditional, invocação, acesso
a membro/elemento, criação, arrows de lambda/expression body, range/index, spread
em collection expression, combinadores de pattern, arms e guards de switch, fluxo
de controle, `await`, `yield`, `throw`, `using`, `lock`, `fixed`, `checked` e
`unchecked`.

Operandos são valores de código-fonte, referências simbólicas, nomes de valores
declarados e nomes de tipos que participam de código executável. Identidades de
identificadores usam informações de símbolo do Roslyn quando elas já estão
disponíveis e ajudam na correção; nomes não resolvidos retornam a texto estável
de sintaxe. Identidades de literais usam valores lógicos quando o Roslyn os
expõe de forma barata. Renomear identificadores pode alterar identidade de
operando, mas não altera contagens de operadores.

## Ownership

Local functions, lambdas e anonymous methods aninhados são raízes executáveis
independentes. Um membro pai não inclui o body do executável aninhado nas suas
contagens Halstead. Sintaxe de header que pertence à expressão ou ao statement do
pai, como arrow de lambda ou nome de declaração de local function, ainda pode ser
contada no pai conforme a convenção de classificação.

## Não Equivalência

Esses valores são uma convenção reproduzível deste projeto. Nenhuma equivalência
numérica exata é alegada com Lizard, Visual Studio ou qualquer outra
implementação de Halstead sem um teste futuro de compatibilidade que comprove
essa equivalência.

# Arquitetura

[English](../en/architecture.md) | Portugues (Brasil)

`ComplexityAnalysis.Analyzers` e um workspace isolado de pacote Roslyn analyzer. Ate a Phase 6, ele contem infraestrutura do analyzer, modelo de complexidade sem Roslyn, extracao de metodos, mapping semantico de operacoes conhecidas, analise interprocedural limitada de metodos fonte, solucao limitada de recorrencias de recursao direta e diagnostics publicos.

## Pipeline Atual

```text
codigo-fonte C#
    |
    v
Roslyn Syntax + SemanticModel
    |
    v
Analysis
    |
    +-- resolucao de tamanho de entrada
    +-- operacoes basicas
    +-- limites de loop
    +-- operacoes conhecidas BCL/LINQ
    +-- chamadas seguras de metodos fonte
    +-- extracao de recursao direta
    +-- solucao de recorrencias
    +-- extracao de metodo
    |
    v
Complexity Model
    |
    +-- expressoes atomicas
    +-- comparacao de crescimento
    +-- composicao
    +-- Unknown
    |
    v
DiagnosticAnalyzer
    |
    +-- BIG0001 complexidade estimada
    +-- BIG1001 lookup linear dentro de iteracao
    +-- BIG1002 materializacao dentro de iteracao
    +-- BIG1003 ordenacao dentro de iteracao
    +-- BIG1004 chamada fonte dentro de iteracao
    +-- BIG1005 crescimento recursivo exponencial
    `-- BIG9000 probe de infraestrutura
```

## Fronteira do Pacote Analyzer

O pacote e carregado por hosts de compilador e IDE:

```text
codigo-fonte da aplicacao
    |
    | compilado por
    v
compilador Roslyn / host de IDE
    |
    | carrega
    v
ComplexityAnalysis.Analyzers
```

O projeto do analyzer targeteia `netstandard2.0` para compatibilidade com hosts. Ele e empacotado como asset de analyzer em:

```text
analyzers/dotnet/cs/
```

Ele nao e empacotado como uma biblioteca runtime normal. Aplicacoes consumidoras nao chamam classes do analyzer em tempo de execucao.

## Isolamento do Projeto

A implementacao herdada de `complexity-hints` permanece uma referencia conceitual. Nao ha `ProjectReference`, dependencia binaria ou dependencia de pacote local do analyzer isolado para projetos herdados.

Isso mantem o pacote pequeno, deterministico e independente. Dependencias Roslyn usadas para autoria do analyzer sao assets privados e nao devem virar dependencias transitivas do consumidor.

## Complexity Model

O modelo fica em:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Model/
```

Ele e intencionalmente livre de Roslyn. Ele representa valores de complexidade independentemente da sintaxe C# para manter as operacoes matematicas imutaveis, deterministicas e testaveis sem APIs do compilador.

Comportamentos implementados incluem:

- formas atomicas como constante, polinomial-logaritmica, exponencial, fatorial e `Unknown`;
- formatacao de formas Big-O comuns, incluindo potencias fracionarias deterministicas como `O(n^1.585)`;
- comparacao de crescimento para expressoes da mesma variavel;
- incomparabilidade conservadora para variaveis independentes;
- composicao sequencial, aninhada e por ramificacao.

## Roslyn Extraction

A camada de analise fica em:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/
```

Ela parte de um metodo por vez. A Phase 6 pode seguir chamadas de metodos fonte suportados sob demanda e resolver recursao direta selecionada, mas nao cria call graph da compilation inteira, nao resolve recursao mutua e nao inspeciona corpos de metodos nao relacionados.

Responsabilidades principais:

- `MethodComplexityExtractor`: coordena analise de metodo, bloco, statement, loop, ramificacao e switch.
- `MethodAnalysisContext`: armazena contexto semantico local ao metodo, variaveis canonicas de tamanho de entrada, fatos locais de limite de loop e cancellation.
- `InputSizeResolver`: mapeia parametros elegiveis para variaveis deterministicas como `n`, `m`, `k`, `p` e `v5`.
- `BasicOperationAnalyzer`: classifica statements e expressoes comprovadas como constantes e delega operacoes conhecidas suportadas.
- `LoopBoundAnalyzer`: reconhece limites de loop constantes, lineares, logaritmicos e enumerables conhecidos.
- `KnownOperationComplexityAnalyzer`: compoe custos de invocacoes BCL/LINQ conhecidas, propriedades, acesso por indice, operacoes terminais e pipelines deferred consumidas.

## Chamadas Fonte Interprocedurais

Analise interprocedural significa que o analyzer pode incluir o custo de um callee fonte suportado na estimativa do caller. O resultado do callee e cacheado como template relativo aos parametros do proprio callee, e depois a substituicao de argumentos mapeia esse template de volta para as dimensoes do caller.

Fluxo conceitual:

```text
Caller
  |
  v
Invocation resolution
  |
  +-- Known BCL/LINQ
  |
  `-- Source method
          |
      cache/template
          |
      substitution
          |
          v
Caller complexity
```

Fronteira de ciclo:

```text
Cycle detected
      |
      v
Unknown
      |
      v
Unknown, exceto quando recursao direta e extraida e resolvida separadamente
```

O traversal e sob demanda. Um callee fonte e analisado apenas quando uma invocacao e visitada a partir do metodo raiz atual, a resolucao de operacao conhecida BCL/LINQ nao se aplica, o dispatch e seguro e o budget interno permite expansao. O analyzer nao pre-analisa todos os metodos e nao cria graph completo da compilation.

Dispatch fonte seguro inclui metodos static, private, ordinarios nao virtuais e dispatch sealed quando o alvo de runtime e comprovado. Dispatch de interface, dispatch virtual inseguro, dynamic dispatch, invocacao de delegate, reflection, metodos externos apenas em metadata, construtores, propriedades, operadores, local functions e lambdas como alvos independentes continuam fora de escopo.

Limites internos restringem profundidade de chamada e metodos expandidos por analise raiz. Resultados `Unknown` continuam conservadores para chamadas nao resolvidas, dispatch inseguro, fonte indisponivel, binding de argumento nao comprovado, fronteiras de budget, cancellation e ciclos. Recursao direta pode ser resolvida apenas pelo pipeline de recorrencia abaixo. Recursao mutua e detectada, mas nao resolvida.

## Recursao Direta e Solucao de Recorrencias

A infraestrutura de recorrencias fica em:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/Recursion/
```

Extracao e solvers sao responsabilidades separadas. `RecursiveCallAnalyzer` identifica invocacoes semanticamente diretas e resume caminhos de execucao recursivos. `RecurrenceExtractor` exige evidencia de base case, seleciona a dimensao da recorrencia, exclui invocacoes diretamente recursivas do custo de trabalho local e cria uma `RecurrenceRelation` interna. `RecurrenceSolver` tenta solvers limitados e retorna resultados explicitos solved, unsupported, invalid ou numerically inconclusive.

As familias implementadas sao recorrencias de soma/decremento, um subconjunto exponencial simples de coeficiente constante, Master Theorem e um subconjunto restrito/limitado de Akra-Bazzi. Trabalho numerico e deterministico e limitado por caps internos de iteracao. O analyzer nao faz integracao numerica geral, execucao de subprocessos, solucao baseada em reflection, I/O, acesso a rede, MathNet, SymPy, Workspaces, varredura de recorrencias na compilation inteira ou chamadas a projetos solver herdados.

Branches recursivos mutuamente exclusivos sao path-sensitive: branches estilo binary search com uma chamada recursiva por branch produzem um termo recursivo por caminho. Chamadas recursivas sequenciais no mesmo caminho podem somar multiplicidade.

Formatos de recorrencia nao suportados, trabalho local desconhecido, base case ausente, argumentos nao redutores, cancellation, resultado numericamente inconclusivo e recursao mutua continuam `Unknown`.

## Operacoes Conhecidas

A infraestrutura de operacoes conhecidas fica em:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/KnownOperations/
```

Mappings carregam identidade semantica, complexidade, tipo de execucao, proveniencia, metadados e informacao de caso quando relevante. A resolucao usa simbolos Roslyn e identidades de operacao, nao apenas nomes textuais de metodos.

Operacoes LINQ deferred como `Where` e `OrderBy` sao cobradas como setup quando criadas. O custo de enumeracao ou ordenacao e contado quando uma operacao terminal suportada ou `foreach` consome a pipeline.

Invocacoes nao suportadas ou nao resolvidas continuam como `Unknown`.

## Camada de Diagnostics

`ComplexityAnalyzer` expoe:

- `BIG0001` no identificador do metodo quando a complexidade estimada e conhecida e o diagnostic esta habilitado.
- `BIG1001` na invocacao de lookup linear dentro de iteracao analisavel.
- `BIG1002` na invocacao de materializacao dentro de iteracao analisavel.
- `BIG1003` na invocacao de ordenacao deferred somente quando consumo suportado e comprovado dentro de iteracao analisavel.
- `BIG1004` na chamada de metodo fonte suportada com complexidade conhecida dependente de entrada dentro de iteracao analisavel.
- `BIG1005` no metodo recursivo suportado cuja recorrencia direta resolvida e exponencial.
- `BIG9000` uma vez por compilation quando habilitado explicitamente.

Analise de codigo gerado e desabilitada, execucao concorrente e habilitada, e hot paths do analyzer devem continuar livres de I/O, rede, execucao de processos e comportamento pesado baseado em reflection.

## Por Que Nao Ha Workspaces

`Microsoft.CodeAnalysis.Workspaces` esta intencionalmente ausente. A Phase 6 nao implementa `CodeFixProvider`, analise de graph de projeto inteiro, carregamento de solution ou recursos de workspace de IDE que justificariam essa dependencia.

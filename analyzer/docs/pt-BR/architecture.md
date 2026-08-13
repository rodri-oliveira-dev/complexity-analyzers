# Arquitetura

[English](../en/architecture.md) | Portugues (Brasil)

`ComplexityAnalysis.Analyzers` e um workspace isolado de pacote Roslyn analyzer. Ate a Phase 4, ele contem infraestrutura do analyzer, modelo de complexidade sem Roslyn, extracao intraprocedural, mapping semantico de operacoes conhecidas e diagnostics publicos.

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
- formatacao de formas Big-O comuns;
- comparacao de crescimento para expressoes da mesma variavel;
- incomparabilidade conservadora para variaveis independentes;
- composicao sequencial, aninhada e por ramificacao.

## Roslyn Extraction

A camada de analise fica em:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Analysis/
```

Ela analisa um metodo por vez. O extractor nao cria call graph, nao segue chamadas locais do projeto, nao resolve recursao e nao inspeciona corpos de outros metodos.

Responsabilidades principais:

- `MethodComplexityExtractor`: coordena analise de metodo, bloco, statement, loop, ramificacao e switch.
- `MethodAnalysisContext`: armazena contexto semantico local ao metodo, variaveis canonicas de tamanho de entrada, fatos locais de limite de loop e cancellation.
- `InputSizeResolver`: mapeia parametros elegiveis para variaveis deterministicas como `n`, `m`, `k`, `p` e `v5`.
- `BasicOperationAnalyzer`: classifica statements e expressoes comprovadas como constantes e delega operacoes conhecidas suportadas.
- `LoopBoundAnalyzer`: reconhece limites de loop constantes, lineares, logaritmicos e enumerables conhecidos.
- `KnownOperationComplexityAnalyzer`: compoe custos de invocacoes BCL/LINQ conhecidas, propriedades, acesso por indice, operacoes terminais e pipelines deferred consumidas.

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
- `BIG9000` uma vez por compilation quando habilitado explicitamente.

Analise de codigo gerado e desabilitada, execucao concorrente e habilitada, e hot paths do analyzer devem continuar livres de I/O, rede, execucao de processos e comportamento pesado baseado em reflection.

## Por Que Nao Ha Workspaces

`Microsoft.CodeAnalysis.Workspaces` esta intencionalmente ausente. A Phase 4 nao implementa `CodeFixProvider`, analise em nivel de projeto, carregamento de solution ou recursos de workspace de IDE que justificariam essa dependencia.

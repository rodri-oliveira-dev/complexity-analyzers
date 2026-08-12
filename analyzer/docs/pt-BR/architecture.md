# Arquitetura

[English](../en/architecture.md) | Português (Brasil)

`ComplexityAnalysis.Analyzers` e um workspace isolado de pacote Roslyn analyzer. Ate a Phase 3, ele contem tres camadas: infraestrutura do analyzer, um modelo de complexidade sem Roslyn e extracao intraprocedural de sintaxe e semantica C# para esse modelo.

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
    | implementado internamente
    X diagnostics de produto Big-O ainda nao conectados
    |
DiagnosticAnalyzer
    `-- probe de infraestrutura BIG9000
```

A camada de extracao atualmente retorna valores internos `ComplexityExpression`. Ela nao reporta diagnostics de produto aos usuarios.

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

O projeto do analyzer targeteia `netstandard2.0` para ampla compatibilidade com hosts. Ele e empacotado como asset de analyzer em:

```text
analyzers/dotnet/cs/
```

Ele nao e empacotado como uma biblioteca runtime normal. Aplicacoes consumidoras nao chamam classes do analyzer em tempo de execucao.

## Isolamento do Projeto

A implementacao herdada de `complexity-hints` permanece uma referencia conceitual:

```text
implementacao herdada
        |
        | fonte conceitual/de referencia
        v
ComplexityAnalysis.Analyzers
```

Nao ha `ProjectReference`, dependencia binaria ou dependencia de pacote local do analyzer isolado para os projetos herdados. Isso mantem o pacote pequeno, deterministico e independente.

## Complexity Model

O modelo fica em:

```text
analyzer/src/ComplexityAnalysis.Analyzers/Model/
```

Ele e intencionalmente livre de Roslyn. Ele representa valores de complexidade independentemente da sintaxe C#, para que as operacoes matematicas continuem pequenas, imutaveis, deterministicas e testaveis sem APIs do compilador.

Comportamentos implementados do modelo incluem:

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

Ela analisa um metodo por vez. Isso e intraprocedural por decisao de design na Phase 3. O extractor nao cria call graph, nao segue chamadas locais do projeto, nao resolve recursao e nao inspeciona corpos de outros metodos.

As principais responsabilidades sao separadas entre:

- `MethodComplexityExtractor`: coordena analise de metodo, bloco, statement, loop, ramificacao e switch.
- `MethodAnalysisContext`: armazena contexto semantico local ao metodo, variaveis canonicas de tamanho de entrada, fatos locais de limite de loop e cancellation.
- `InputSizeResolver`: mapeia parametros elegiveis para variaveis deterministicas como `n`, `m`, `k`, `p` e `v5`.
- `BasicOperationAnalyzer`: classifica apenas statements e expressoes comprovadamente constantes.
- `LoopBoundAnalyzer`: reconhece limites de loop constantes, lineares e logaritmicos suportados.

## Exemplos de Extracao

Estes exemplos descrevem resultados internos de extracao cobertos por testes. Eles nao sao diagnostics visiveis ao usuario na Phase 3.

```csharp
void M(int[] items)
{
    foreach (var item in items)
    {
        var x = item + 1;
    }
}
```

Resultado interno: `O(n)`.

```csharp
void M(int[] items)
{
    foreach (var outer in items)
    {
        foreach (var inner in items)
        {
            var x = outer + inner;
        }
    }
}
```

Resultado interno: `O(n^2)`.

```csharp
void M()
{
    Visit();
}
```

Resultado interno: `Unknown`, porque chamadas de metodos locais do projeto nao sao resolvidas na Phase 3.

## Unknown

`Unknown` e uma decisao de seguranca. Ele significa que o analyzer nao conseguiu provar uma complexidade assintotica segura para a construcao.

`Unknown` nao significa `O(1)`, nao significa `O(n)` e nao representa, por si so, um problema de performance. Ele evita que comportamento nao suportado seja transformado em palpite inseguro.

## Camada Atual de Diagnostics

O `DiagnosticAnalyzer` atual expoe exatamente um diagnostic:

- `BIG9000` - analyzer execution probe.

Ele e registrado por uma compilation action, desabilitado por padrao e reporta no maximo uma vez por compilation quando habilitado explicitamente. Ele e apenas infraestrutura e nao consome o resultado da extracao de complexidade da Phase 3.

## Por Que Nao Ha Workspaces

`Microsoft.CodeAnalysis.Workspaces` esta intencionalmente ausente. A Phase 3 nao implementa `CodeFixProvider`, analise em nivel de projeto, carregamento de solution ou recursos de workspace de IDE que justificariam essa dependencia.

# Arquitetura

[English](../en/architecture.md) | Português (Brasil)

`ComplexityAnalysis.Analyzers` é um pacote Roslyn Analyzer independente. A raiz do repositório representa a fronteira do produto: o código do analyzer fica em `src/`, os testes em `tests/`, a validação de performance em `performance/` e a documentação em `docs/`.

O design prioriza resultados conservadores, análise limitada, comportamento determinístico e compatibilidade com hosts de compilador e IDE.

## Pipeline de análise

```text
código-fonte C#
    |
    v
sintaxe Roslyn + SemanticModel
    |
    v
análise do método
    |
    +-- configuração do analyzer
    +-- resolução de tamanho de entrada
    +-- operações básicas
    +-- limites de loop
    +-- operações BCL/LINQ conhecidas
    +-- chamadas seguras a métodos-fonte
    +-- extração de recursão direta
    +-- resolução de recorrências
    |
    v
modelo de complexidade
    |
    +-- expressões atômicas
    +-- comparação de crescimento
    +-- composição
    +-- Unknown
    |
    v
DiagnosticAnalyzer
    |
    +-- BIG0001 complexidade estimada
    +-- BIG1001 busca linear dentro de iteração
    +-- BIG1002 materialização dentro de iteração
    +-- BIG1003 ordenação dentro de iteração
    +-- BIG1004 chamada a método-fonte dentro de iteração
    +-- BIG1005 crescimento recursivo exponencial
    +-- BIG1006 threshold configurado excedido
    `-- BIG9000 probe de execução
```

## Fronteira do pacote

O pacote é carregado por hosts de compilador e IDE:

```text
código-fonte da aplicação
    |
    | compilado por
    v
compilador Roslyn / host da IDE
    |
    | carrega
    v
ComplexityAnalysis.Analyzers
```

O projeto do analyzer targeteia `netstandard2.0` para compatibilidade com hosts e é empacotado como asset de analyzer em:

```text
analyzers/dotnet/cs/
```

Ele não é uma biblioteca de runtime. Aplicações consumidoras não chamam classes do analyzer, e as dependências usadas para autoria com Roslyn permanecem privadas em vez de serem expostas transitivamente.

## Estrutura do repositório

As principais áreas do produto são:

```text
src/ComplexityAnalysis.Analyzers/
    Analysis/
    Configuration/
    Diagnostics/
    Model/
    ComplexityAnalyzer.cs

tests/
performance/
docs/
```

O repositório não depende mais de projetos herdados da implementação anterior. O analyzer é desenvolvido e validado diretamente a partir da solução na raiz.

## Modelo de complexidade

O modelo independente de Roslyn fica em:

```text
src/ComplexityAnalysis.Analyzers/Model/
```

Ele representa complexidade separadamente da sintaxe C#, mantendo o comportamento matemático imutável, determinístico e testável sem APIs do compilador.

O modelo suporta:

- formas constantes, polinomiais/logarítmicas, exponenciais, fatoriais e `Unknown`;
- formatação determinística, incluindo potências fracionárias como `O(n^1.585)`;
- comparação de crescimento para expressões comparáveis;
- incomparabilidade conservadora para variáveis independentes;
- composição sequencial, aninhada e por branches.

Quando o analyzer não consegue comprovar um resultado seguro, o modelo preserva `Unknown` em vez de forçar uma classe de complexidade estimada.

## Análise com Roslyn

A principal camada de análise fica em:

```text
src/ComplexityAnalysis.Analyzers/Analysis/
```

A análise parte de um método e avalia informações sintáticas e semânticas suportadas. As responsabilidades incluem extração de métodos, resolução de tamanho de entrada, classificação de operações básicas, análise de limites de loop, mapeamento de operações conhecidas, propagação de chamadas a métodos-fonte e tratamento de recursão direta.

Alguns componentes representativos são:

- `MethodComplexityExtractor` para composição do método e de seu corpo;
- `MethodAnalysisContext` para contexto semântico e dimensões de entrada;
- `InputSizeResolver` para dimensões canônicas como `n`, `m`, `k`, `p` e variáveis posteriores;
- `BasicOperationAnalyzer` para trabalho básico comprovado;
- `LoopBoundAnalyzer` para limites de loop suportados;
- `KnownOperationComplexityAnalyzer` para custos BCL/LINQ suportados.

O analyzer não exige um call graph completo da compilation ou da solution.

## Operações BCL e LINQ conhecidas

As operações conhecidas são resolvidas pela identidade do símbolo Roslyn, e não apenas pelo nome textual do método. Isso evita que um método definido pelo usuário chamado `Contains`, `Where` ou `ToList` seja interpretado acidentalmente como uma operação do framework.

A infraestrutura de operações conhecidas fica em:

```text
src/ComplexityAnalysis.Analyzers/Analysis/KnownOperations/
```

Operações LINQ deferred como `Where` e `OrderBy` não são contabilizadas como enumeração completa apenas por serem criadas. O custo de enumeração ou ordenação é aplicado quando o consumo suportado é comprovado, por exemplo por uma operação terminal ou `foreach`.

Operações não suportadas ou não resolvidas permanecem `Unknown`.

## Chamadas interprocedurais a métodos-fonte

A análise interprocedural pode incluir o custo de um callee fonte suportado na estimativa do caller. O resultado do callee pode ser representado como um template relativo aos seus parâmetros e depois substituído usando os argumentos do caller.

```text
Caller
  |
  v
resolução da invocação
  |
  +-- operação BCL/LINQ conhecida
  |
  `-- método-fonte suportado
          |
      template/cache
          |
      substituição de argumentos
          |
          v
complexidade do Caller
```

O traversal é sob demanda. Um método-fonte só é analisado quando é alcançado a partir da raiz atual, a resolução de operação conhecida não se aplica, o dispatch é seguro e o budget configurado/interno permite a expansão.

O dispatch suportado inclui métodos static, private, métodos ordinários não virtuais e dispatch sealed quando o alvo de runtime pode ser comprovado. Dispatch virtual/interface inseguro, dynamic dispatch, delegates, reflection, métodos externos disponíveis apenas em metadata, construtores, propriedades, operadores, local functions e lambdas como alvos independentes permanecem fora do escopo interprocedural suportado.

Ciclos são detectados de forma conservadora. Recursão direta pode ser tratada pelo pipeline de recorrências; recursão mútua é detectada, mas não resolvida.

## Recursão direta e resolução de recorrências

A análise de recorrências fica em:

```text
src/ComplexityAnalysis.Analyzers/Analysis/Recursion/
```

O pipeline separa detecção, extração da recorrência e resolução. Uma recorrência suportada exige recursão direta identificada semanticamente, evidência compatível de base case, argumento comprovadamente redutor e trabalho local conhecido.

As famílias de solver suportadas incluem:

- recorrências de soma/decremento;
- um subconjunto exponencial simples e limitado;
- formas do Master Theorem;
- um subconjunto restrito e limitado de Akra-Bazzi.

A implementação é determinística e limitada. Ela não executa resolução simbólica geral de recorrências, integração numérica geral, processos externos, acesso de rede, integração com MathNet/SymPy ou chamadas a projetos solver externos.

Formatos não suportados, base case ausente, argumentos não redutores, trabalho local desconhecido, cancelamento, inconclusão numérica e recursão mútua permanecem `Unknown`.

## Configuração

A configuração é lida por meio do `AnalyzerConfigOptionsProvider` do Roslyn; o analyzer não faz parsing manual de `.editorconfig`.

As opções públicas de comportamento são:

- `complexity_analyzers.interprocedural_analysis`;
- `complexity_analyzers.recursion_analysis`;
- `complexity_analyzers.max_call_depth`;
- `complexity_analyzers.max_methods_per_root`;
- `complexity_analyzers.maximum_complexity`.

Valores específicos por syntax tree sobrescrevem valores globais para aquela árvore. Valores inválidos retornam aos defaults documentados em vez de gerar falhas do analyzer.

Veja [Configuração](configuration.md) para detalhes.

## Diagnósticos

`ComplexityAnalyzer` expõe:

- `BIG0001` para estimativas de complexidade por método, opt-in;
- `BIG1001` para buscas lineares suportadas dentro de iteração analisável;
- `BIG1002` para materialização suportada dentro de iteração analisável;
- `BIG1003` para ordenação consumida e suportada dentro de iteração analisável;
- `BIG1004` para chamadas suportadas a métodos-fonte com custo dependente de entrada dentro de iteração analisável;
- `BIG1005` para recursão direta suportada com crescimento exponencial resolvido;
- `BIG1006` para estimativas conhecidas e comparáveis acima de um threshold configurado;
- `BIG9000` como probe de execução opt-in.

A análise de código gerado é desabilitada e a execução concorrente do analyzer é habilitada.

Veja o [Catálogo de Analyzers](analyzers.md) para o comportamento de cada regra.

## Performance e validação do pacote

O analyzer foi projetado para execução dentro do compilador/IDE e mantém hot paths livres de acesso de rede, execução de processos, telemetria e scans obrigatórios da solution inteira.

A validação de performance usa workloads sintéticos determinísticos e invariantes estruturais em vez de thresholds estreitos dependentes da máquina. O caminho `ReportAnalyzer=true` do compilador é usado para verificar o reporting de execução do analyzer.

A validação do contrato do pacote garante que `ComplexityAnalysis.Analyzers.dll` seja empacotado em `analyzers/dotnet/cs/`, e não em `lib/`, e que as dependências de autoria não se tornem dependências de runtime do consumidor.

O CI também valida o consumo do pacote com hosts SDK .NET 8, .NET 9 e .NET 10.

## Por que não há dependência de Workspaces

`Microsoft.CodeAnalysis.Workspaces` está intencionalmente ausente. O projeto não implementa `CodeFixProvider`, carregamento de solution, análise baseada em workspace de projeto inteiro ou outros recursos de IDE que exijam essa dependência.

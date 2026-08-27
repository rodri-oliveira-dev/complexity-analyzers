# Baseline de performance do analyzer

[English](README.md) | Português (Brasil)

Este diretório contém validações de performance reproduzíveis para
`ComplexityAnalysis.Analyzers`.

> Este documento é a versão em português de `performance/README.md`. Em caso de divergência, a versão em inglês é a referência canônica.

O analyzer executa dentro dos hosts do compilador e da IDE, portanto performance faz parte do comportamento funcional. O projeto separa gates estruturais determinísticos de medições de tempo que variam conforme hardware e runners compartilhados de CI.

## Modelo de performance

O analyzer deve permanecer:

- livre de I/O de filesystem, I/O de rede, execução de processos e telemetria nos hot paths do analyzer;
- livre de análise obrigatória de solution inteira e construção obrigatória de call graph da compilation inteira;
- sob demanda para traversal de métodos-fonte;
- limitado para traversal interprocedural e resolução de recorrências;
- cancellation-aware em caminhos de análise não triviais;
- seguro para execução concorrente;
- conservador nos limites, preferindo `Unknown` a uma análise ilimitada ou insegura.

Código gerado permanece excluído da análise de syntax nodes.

## Workloads

A baseline usa quatro grupos de workload.

| Workload | Forma representativa | Objetivo |
| --- | --- | --- |
| Tiny | Métodos straight-line, uma known operation, um loop simples. | Overhead básico do analyzer e reporting comum de métodos. |
| Small | Loops, LINQ, chamadas simples a métodos-fonte e known BCL operations. | Análise semântica comum por método. |
| Medium | Múltiplos métodos, callees compartilhados, cadeias interprocedurais, iteração aninhada, consumo de LINQ deferred, recursão direta suportada, fluxo de controle flat-heavy, fluxo de controle deep-heavy, nesting misto de switch/try/loop e executable members aninhados. | Execução representativa do analyzer no compilador e separação entre path count e nesting depth. |
| Stress/adversarial synthetic | Call chains próximas de `max_call_depth`, fanout próximo de `max_methods_per_root`, ciclos, chamadas repetidas ao mesmo callee, recorrências não suportadas, exaustão do solver numérico, cancelamento. | Terminação, boundedness, ownership de cache e fallback conservador. |

Os fontes dos workloads estão versionados em:

- `performance/ComplexityAnalysis.Analyzers.Performance/TimingWorkload.cs`
- `performance/.editorconfig`
- `tests/ComplexityAnalysis.Analyzers.Tests/PerformanceSyntheticCorpusTests.cs`
- `tests/ComplexityAnalysis.Analyzers.Tests/AnalyzerPerformanceBudgetContractTests.cs`

## Budgets

Os budgets atuais vêm da implementação, não de suposições existentes apenas na documentação.

| Budget | Padrão | Máximo rígido | Comportamento quando excedido |
| --- | --- | --- | --- |
| Profundidade de source call | `5` | `16` | A source call afetada permanece `Unknown`; o traversal não cruza o limite. |
| Expansões uncached de source methods por root | `32` | `128` | A source call afetada permanece `Unknown`; a contagem de templates únicos concluídos é limitada para fixtures determinísticas. |
| Expansões de bracket do Akra-Bazzi restrito | `16` | Default interno fixo | Inconclusão numérica, seguida de `Unknown` no nível do analyzer. |
| Iterações de bisseção do Akra-Bazzi restrito | `64` | Default interno fixo | Inconclusão numérica, seguida de `Unknown` no nível do analyzer. |
| Expoente máximo do Akra-Bazzi restrito | `1024` | Default interno fixo | Inconclusão numérica, seguida de `Unknown` no nível do analyzer. |

Outros recurrence solvers são limitados pela forma extraída da recorrência e pela lista fixa de famílias de solver.

## Contrato de cache

| Cache | Owner | Lifetime | Limite esperado |
| --- | --- | --- | --- |
| Known operation registry | Singleton static imutável | Lifetime do processo | Tabela de mappings fixa. |
| Interprocedural template cache | `InterproceduralAnalysisContext` | Um contexto de análise por compilation | Métodos-fonte alcançados vezes variantes distintas de options. |
| Direct recurrence cache | `InterproceduralAnalysisContext` | Um contexto de análise por compilation | Métodos recursivos alcançados vezes variantes distintas de options. |
| Semantic model cache | `InterproceduralAnalysisContext` | Um contexto de análise por compilation | Syntax trees alcançadas. |
| Analyzer options cache | `InterproceduralAnalysisContext` | Um contexto de análise por compilation | Syntax trees alcançadas. |

Os caches mutáveis são por compilation e usam concurrent dictionaries. Nenhum cache mutável static cross-compilation do analyzer faz parte da baseline.

## Gates estruturais rígidos

Estes checks são apropriados para bloquear CI porque são determinísticos:

- traversal para nos budgets configurados de call depth e methods per root;
- fanout no hard maximum público funciona e fanout acima do máximo é interrompido;
- chamadas repetidas reutilizam o mesmo template concluído do callee;
- ciclos terminam de forma conservadora;
- budgets zero desabilitam expansão de source de forma conservadora;
- limites numéricos de recorrência retornam inconclusivo em vez de entrar em loop ou estimar;
- tokens cancelados interrompem a análise sem crescimento de cache nos caminhos cobertos;
- exclusão de generated code e execução concorrente permanecem explícitas;
- testes de package layout e compatibilidade de consumer permanecem verdes;
- o source de produção do analyzer permanece livre de símbolos proibidos de I/O, rede, processo e telemetria nos hot paths, verificados por binding semântico do Roslyn em vez de substring matching.

As suites estruturais principais são:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
```

A suite completa também inclui a baseline de caracterização da issue #31:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
```

## Medições informativas

Elapsed time e tempo do analyzer reportado pelo compilador são sinais úteis de tendência, mas não são gates estreitos de CI nesta baseline.

Execute o teste de synthetic corpus para imprimir o elapsed time local:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
```

Execute o caminho de reporting do analyzer pelo compilador:

```bash
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

O sinal útil do compilador é o resumo de execução do analyzer emitido para:

```text
ComplexityAnalysis.Analyzers.ComplexityAnalyzer
```

`ReportAnalyzer=true` é um caminho local de reporting do compilador/MSBuild, não telemetria no analyzer distribuído.

## Comportamento no CI

O job de performance no CI bloqueia com base em testes estruturais e verifica se o reporting do compilador inclui `ComplexityAnalysis.Analyzers.ComplexityAnalyzer`.

Os dados de timing do relatório do compilador são enviados como artifact para inspeção e comparação de tendência.

O CI não deve ser tornado verde enfraquecendo budgets, desabilitando testes, adicionando `continue-on-error` ou substituindo gates estruturais por thresholds frágeis em milissegundos.

## Política de regressão material

Um PR deve tratar performance como materialmente regredida quando fizer qualquer uma das ações abaixo sem justificativa explícita e evidência de validação:

- aumentar um budget estrutural público ou interno;
- contornar ou remover checks de traversal limitado;
- introduzir scans obrigatórios da compilation inteira ou da solution inteira em callbacks do analyzer;
- aumentar lifetime, escopo, cardinalidade de chave ou risco de retenção de cache;
- introduzir estado static mutável cross-compilation;
- adicionar I/O de filesystem, I/O de rede, execução de processo, telemetria ou reflection pesada aos caminhos de execução do analyzer;
- aumentar materialmente o tempo do analyzer reportado pelo compilador em workloads representativos após execuções locais repetidas;
- aumentar materialmente alocações em hot paths quando houver evidência de profiling;
- enfraquecer a responsividade a cancellation;
- trocar fallback `Unknown` por uma estimativa conhecida não comprovada apenas para melhorar benchmark.

Variações intencionais devem ser documentadas no PR com o workload afetado, motivo, evidência before/after e justificativa de por que o comportamento do analyzer permanece seguro.

## Validação da baseline

Para a issue #32, execute a partir da raiz do repositório:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Ao registrar timing before/after, inclua SDK, sistema operacional, configuração, comando, número de execuções e variação observada. Não faça afirmações absolutas com base em uma única execução de elapsed time.

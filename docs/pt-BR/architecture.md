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
análise de executable member
    (constructs executáveis C# suportados)
    |
    +-- configuração do analyzer
    +-- resolução de tamanho de entrada
    +-- operações básicas
    +-- limites de loop
    +-- operações BCL/LINQ conhecidas
    +-- chamadas seguras a métodos-fonte
    +-- extração de recursão direta
    +-- resolução de recorrências
    +-- métricas estruturais de fluxo de controle
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
    +-- BIG2001 threshold ciclomático excedido
    +-- BIG2002 threshold de maximum nesting excedido
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

O SDK de build do repositório é uma preocupação separada. `global.json` seleciona o SDK `10.0.400` para restore, build, testes e pack do repositório, enquanto os hosts de compilador suportados são validados instalando o `.nupkg` produzido em projetos consumidores temporários.

## Contratos de compatibilidade

| Contrato | Valor atual |
| --- | --- |
| SDK de build do repositório | `.NET SDK 10.0.400` a partir de `global.json`. |
| Versão de linguagem C# do repositório | `12.0`. |
| Target framework do analyzer | `netstandard2.0`, preservado para compatibilidade com hosts de compilador/IDE. |
| Baseline de API do compilador Roslyn | `Microsoft.CodeAnalysis.CSharp` `4.8.0`, resolvendo `Microsoft.CodeAnalysis.Common` `4.8.0`. |
| Regras de autoria de analyzer | `Microsoft.CodeAnalysis.Analyzers` `3.11.0`. |
| Matriz de hosts SDK suportados | Builds consumidores com `.NET 8`, `.NET 9` e `.NET 10` no CI. |
| Path do analyzer no pacote | `analyzers/dotnet/cs/ComplexityAnalysis.Analyzers.dll`. |
| Assets runtime do pacote | Sem asset `lib/` do analyzer e sem grupo de dependências transitivas de Roslyn. |

Compilar contra packages Roslyn mais novos pode introduzir referências a APIs que hosts de compilador ou IDE mais antigos não conseguem carregar. Upgrades de Roslyn são, portanto, conservadores: propostas de atualização precisam manter assets Roslyn privados, inspecionar o pacote gerado, executar testes de contrato de package/consumer e validar todos os hosts SDK suportados antes do merge.

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

Cyclomatic Complexity e Maximum Control-Flow Nesting Depth são métricas
estruturais inteiras separadas. Elas são calculadas a partir do fluxo de controle
do executable member e não são combinadas com o modelo de complexidade Big-O nem
entre si.

## Análise com Roslyn

A principal camada de análise fica em:

```text
src/ComplexityAnalysis.Analyzers/Analysis/
```

A análise parte de uma abstração interna de executable member e avalia informações sintáticas e semânticas suportadas. O analyzer normaliza métodos ordinários, construtores, accessors de propriedades/eventos, operadores, operadores de conversão, local functions, lambdas, anonymous methods e formas expression-bodied suportadas para o mesmo pipeline quando identidade de símbolo, ownership de body e localização de diagnóstico estão disponíveis. As responsabilidades incluem extração de membro/body, resolução de tamanho de entrada, classificação de operações básicas, análise de limites de loop, mapeamento de operações conhecidas, propagação de chamadas a métodos-fonte, tratamento de recursão direta e métricas estruturais de fluxo de controle como Cyclomatic Complexity e Maximum Control-Flow Nesting Depth.

Alguns componentes representativos são:

- `ExecutableMember` para identidade, body, nome de exibição e localização de diagnóstico do membro analisado;
- `ExecutableMemberSyntax` para fronteiras de body executável que impedem local functions, lambdas e anonymous methods aninhados de inflarem o pai lexical;
- `MethodComplexityExtractor` para composição do método e de seu corpo;
- `MethodAnalysisContext` para contexto semântico e dimensões de entrada;
- `InputSizeResolver` para dimensões canônicas como `n`, `m`, `k`, `p` e variáveis posteriores;
- `BasicOperationAnalyzer` para trabalho básico comprovado;
- `LoopBoundAnalyzer` para limites de loop suportados;
- `KnownOperationComplexityAnalyzer` para custos BCL/LINQ suportados.
- `CyclomaticComplexityAnalyzer` para pontuação estrutural de complexidade de caminhos, independente do Big-O.
- `MaximumNestingDepthAnalyzer` para pontuação de profundidade máxima de nesting de fluxo de controle, independente de Big-O e Cyclomatic Complexity.

O analyzer não exige um call graph completo da compilation ou da solution.
Constructs executáveis aninhados são analisados como raízes próprias. Um membro
pai não atravessa automaticamente o body de uma local function, lambda ou
anonymous method, salvo quando esse executável aninhado é alcançado por um
caminho de chamada suportado.

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

O dispatch suportado inclui métodos static, private, métodos ordinários não virtuais, dispatch sealed quando o alvo de runtime pode ser comprovado e invocações diretas de local functions. Dispatch virtual/interface inseguro, dynamic dispatch, delegates, reflection, métodos externos disponíveis apenas em metadata, construtores, acesso a propriedades, acesso a eventos, operadores, conversões, lambdas e anonymous methods como callees permanecem fora do escopo interprocedural suportado.

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
- `complexity_analyzers.maximum_complexity`;
- `complexity_analyzers.maximum_cyclomatic_complexity`;
- `complexity_analyzers.cyclomatic_complexity_mode`;
- `complexity_analyzers.maximum_nesting_depth`.

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
- `BIG2001` para executable members suportados acima de um threshold configurado de Cyclomatic Complexity;
- `BIG2002` para executable members suportados acima de um threshold configurado de Maximum Control-Flow Nesting Depth;
- `BIG9000` como probe de execução opt-in.

A análise de código gerado é desabilitada e a execução concorrente do analyzer é habilitada.

Veja o [Catálogo de Analyzers](analyzers.md) para o comportamento de cada regra.

## Performance e validação do pacote

O analyzer foi projetado para execução dentro do compilador/IDE e mantém hot paths livres de acesso de rede, execução de processos, telemetria e scans obrigatórios da solution inteira.

A validação de performance usa workloads sintéticos determinísticos e invariantes estruturais em vez de thresholds estreitos dependentes da máquina. O caminho `ReportAnalyzer=true` do compilador é usado para verificar o reporting de execução do analyzer.

Budgets de performance fazem parte do contrato do analyzer. O traversal de métodos-fonte usa por padrão profundidade de chamada `5` e `32` expansões não cacheadas de métodos-fonte por raiz, com hard maximums públicos de `16` e `128`. A resolução de recorrências permanece limitada pelas formas suportadas e por limites numéricos fixos no solver restrito de Akra-Bazzi. Nas fronteiras de budget, o analyzer mantém os resultados afetados conservadores, normalmente `Unknown`.

O CI trata checks estruturais determinísticos como gates bloqueantes: traversal limitado, ownership de cache, comportamento de cancellation, exclusão de código gerado, layout do pacote e compatibilidade de consumo. Tempo decorrido e timing de analyzer reportado pelo compilador são sinais informativos de tendência, a menos que evidência repetida mostre uma regressão material. A política detalhada de regressão e os comandos locais estão documentados em [`performance/README.md`](../../performance/README.md).

A validação do contrato do pacote garante que `ComplexityAnalysis.Analyzers.dll` seja empacotado em `analyzers/dotnet/cs/`, e não em `lib/`, e que as dependências de autoria não se tornem dependências de runtime do consumidor.

O CI também valida o consumo do pacote com hosts SDK .NET 8, .NET 9 e .NET 10. Cada job de compatibilidade restaura e compila um consumidor temporário usando o `.nupkg` real, habilita `BIG9000` como probe de carregamento do pacote, habilita `BIG1006` em um método quadrático conhecido, rejeita falhas de carregamento do analyzer e verifica que o pacote contribui assets de analyzer em vez de assets de compile/runtime.

## Por que não há dependência de Workspaces

`Microsoft.CodeAnalysis.Workspaces` está intencionalmente ausente. O projeto não implementa `CodeFixProvider`, carregamento de solution, análise baseada em workspace de projeto inteiro ou outros recursos de IDE que exijam essa dependência.

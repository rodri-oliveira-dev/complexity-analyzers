# Catálogo de Analyzers

[English](../en/analyzers.md) | Português (Brasil)

Esta página documenta os diagnósticos públicos expostos por
`ComplexityAnalysis.Analyzers` e a evidência necessária para cada regra reportar.

O analyzer é intencionalmente conservador. Ele reporta apenas fatos e
estimativas conhecidas sustentados pela sintaxe atual, pelo modelo semântico do
Roslyn, pelo registro de operações conhecidas, pela análise limitada de
métodos-fonte e pelo solver de recursão direta suportado. Comportamentos não
suportados, inseguros, cíclicos, limitados por budget, numericamente
inconclusivos ou não resolvidos permanecem `Unknown` em vez de serem estimados.

## Resumo

| ID | Título | Categoria | Severidade padrão | Habilitado por padrão |
| --- | --- | --- | --- | --- |
| `BIG0001` | Estimated algorithmic complexity | `Complexity` | `Info` | `false` |
| `BIG1001` | Linear lookup inside iteration | `Complexity` | `Info` | `true` |
| `BIG1002` | Materialization inside iteration | `Complexity` | `Info` | `true` |
| `BIG1003` | Ordering inside iteration | `Complexity` | `Info` | `true` |
| `BIG1004` | Input-dependent method call inside iteration | `Complexity` | `Info` | `true` |
| `BIG1005` | Exponential recursive growth | `Complexity` | `Info` | `true` |
| `BIG1006` | Method complexity exceeds configured threshold | `Complexity` | `Info` | `true` |
| `BIG2001` | Cyclomatic complexity exceeds configured threshold | `Complexity` | `Info` | `true` |
| `BIG2002` | Maximum nesting depth exceeds configured threshold | `Complexity` | `Info` | `true` |
| `BIG9000` | Analyzer execution probe | `Infrastructure` | `Info` | `false` |

## Executable Members Suportados

O analyzer normaliza constructs executáveis C# suportados para um único pipeline
interno de executable member. Cada raiz suportada precisa ter identidade de
símbolo Roslyn, body explícito, localização estável de diagnóstico e ownership
isolado do body.

| Executable member | Análise como raiz | Análise interprocedural como callee | Recursão direta |
| --- | --- | --- | --- |
| Método ordinário | Sim | Sim, quando o dispatch de fonte é seguro | Sim, para recorrências suportadas |
| Construtor/static constructor | Sim | Deferred | Não aplicável |
| Destructor/finalizer | Não | Não | Não |
| Getter/setter/init accessor de propriedade | Sim, com body explícito | Deferred | Conservadora; reporta apenas se a prova de recorrência existente tiver sucesso |
| Accessor add/remove de evento | Sim, com body explícito | Deferred | Conservadora; reporta apenas se a prova de recorrência existente tiver sucesso |
| Operador | Sim | Deferred | Conservadora; reporta apenas se a prova de recorrência existente tiver sucesso |
| Operador de conversão | Sim | Deferred | Conservadora; reporta apenas se a prova de recorrência existente tiver sucesso |
| Local function | Sim | Sim, para chamadas diretas de local function | Sim, para recorrências suportadas |
| Lambda simples/parenthesized | Sim | Deferred | Deferred |
| Anonymous method | Sim | Deferred | Deferred |
| Propriedade expression-bodied | Sim, como getter | Deferred | Deferred |
| Membro method-like expression-bodied | Sim, conforme o tipo de membro | Igual ao tipo de membro | Igual ao tipo de membro |

Bodies executáveis aninhados não são contados como parte do pai lexical apenas
por aparecerem dentro dele. Por exemplo, uma local function ou lambda declarada
dentro de um método é analisada separadamente; o pai só a inclui quando um
caminho de invocação suportado comprova execução. Variáveis capturadas não são
tratadas como parâmetros independentes de tamanho de entrada.

## Convenção de Explicabilidade

Diagnósticos acionáveis seguem esta convenção quando há evidência disponível:

| Dimensão | Significado |
| --- | --- |
| WHAT | O que foi detectado. |
| WHERE | Qual operação, invocação, método ou construct causou o diagnóstico. |
| WHY | Por que o padrão importa. |
| COST | O custo conhecido de operação ou callee usado pelo analyzer. |
| CONTEXT | O contexto de execução, como uma iteração analisável. |
| THRESHOLD | O máximo configurado ultrapassado por uma estimativa conhecida. |
| GUIDANCE | Direção condicional de melhoria, não uma correção obrigatória. |
| LIMIT | O que o analyzer não está afirmando. |

Mensagens de diagnóstico permanecem curtas para IDE/build. Raciocínio detalhado,
exemplos, guidance e limitações ficam neste catálogo. A ausência de um
diagnóstico não prova que o código seja eficiente; também pode significar que o
analyzer não conseguiu comprovar os fatos necessários com segurança.

## Propriedades de Diagnóstico

Diagnósticos podem incluir propriedades estruturadas estáveis para tooling
futuro. Essas propriedades são strings determinísticas e não devem ser tratadas
como um trace interno completo.

| Propriedade | Significado |
| --- | --- |
| `complexity` | Estimativa conhecida emitida por `BIG0001`, `BIG1005` ou `BIG1006`. |
| `threshold` | Threshold configurado emitido por `BIG1006`, `BIG2001` ou `BIG2002`. |
| `cyclomaticComplexity` | Cyclomatic Complexity real emitida por `BIG2001`. |
| `cyclomaticComplexityMode` | Modo de contabilização de `switch` emitido por `BIG2001`: `standard` ou `modified_mccabe`. |
| `maximumNestingDepth` | Maximum Control-Flow Nesting Depth real emitida por `BIG2002`. |
| `operation` | Nome estável da operação ou método responsável por um diagnóstico acionável. |
| `operationComplexity` | Custo conhecido da operação ou callee no local do diagnóstico. |
| `iterationComplexity` | Complexidade conhecida da iteração envolvente. |
| `combinedComplexity` | Contribuição aninhada conhecida composta para o diagnóstico. |
| `recurrenceClass` | Classe estável do resultado de recorrência, atualmente `exponential`. |
| `diagnosticRole` | Papel estável de infraestrutura para `BIG9000`, atualmente `execution-probe`. |

## BIG0001 - Complexidade Algorítmica Estimada

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `false` |
| Localização | Localização estável do executable member |
| Mensagem | `Estimated algorithmic complexity for '{method}' is {complexity}` |
| Diagnostic properties | `complexity` |

### O Que Detecta

`BIG0001` reporta a estimativa conhecida do analyzer para um executable member
suportado, como `O(1)`, `O(log n)`, `O(n)`, `O(n log n)`, `O(n^2)`,
`O(n^1.585)` ou `O(1.618^n)`.

### Por Que Importa

O diagnóstico expõe o resultado por membro que serve de base para diagnostics
acionáveis e de threshold. Ele é desabilitado por padrão porque pode gerar ruído
em builds normais.

### Exemplo Que Dispara

```csharp
public void M(int[] values)
{
    foreach (var value in values)
    {
        _ = value + 1;
    }
}
```

Quando habilitado, `M` reporta
`Estimated algorithmic complexity for 'M' is O(n)`.

### Exemplo Que Não Dispara

```csharp
public void M(Service service)
{
    service.Process();
}
```

Se a chamada não puder ser resolvida como operação conhecida suportada ou
método-fonte seguro, a estimativa do método permanece `Unknown` e `BIG0001` não
reporta.

### Raciocínio de Complexidade

A estimativa pode incluir limites de loop suportados, operações BCL/LINQ
conhecidas, callees de métodos-fonte ou local functions seguros na mesma
compilation e formatos selecionados de recursão direta resolvida.

### Orientação

Use `BIG0001` quando quiser visibilidade das estimativas conhecidas ao ajustar
thresholds ou revisar o comportamento da análise.

### Limitações

`Unknown` não é convertido em uma classe de complexidade especulativa.
Comportamentos não suportados, não resolvidos, inseguros, limitados por budget,
cancelados, baseados apenas em variável capturada ou incomparáveis podem
suprimir o diagnóstico.

Habilite com:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

## BIG1001 - Busca Linear Dentro de Iteração

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação da busca |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### O Que Detecta

`BIG1001` reporta uma busca suportada cujo custo conhecido é não constante e cuja
invocação está dentro de uma iteração analisável.

### Por Que Importa

Buscas lineares repetidas podem multiplicar o custo do loop envolvente.

### Exemplo Que Dispara

```csharp
foreach (var customer in customers)
{
    if (blockedCustomers.Contains(customer))
    {
    }
}
```

Quando `blockedCustomers` é um `List<T>` suportado, o analyzer pode reportar que
`List<T>.Contains` tem custo linear conhecido dentro do loop.

### Exemplo Que Não Dispara

```csharp
foreach (var customer in customers)
{
    if (blockedCustomers.Contains(customer))
    {
    }
}
```

Se `blockedCustomers` é um `HashSet<T>` suportado, a busca é registrada como
`O(1)` médio e esta regra não reporta.

### Raciocínio de Complexidade

O analyzer usa identidade do símbolo resolvido para a operação, resolve a
dimensão do receiver/input, comprova uma iteração envolvente e compõe o custo da
operação com o custo da iteração. `combinedComplexity` só é reportada quando essa
composição é conhecida.

### Orientação

Considere uma busca indexada ou uma estrutura com semântica de conjunto quando a
busca repetida por pertinência for necessária e semântica, ordenação, duplicatas,
mutabilidade e custo de memória forem apropriados.

### Limitações

O analyzer não afirma que `HashSet<T>` ou qualquer coleção alternativa seja
sempre semanticamente correta. Métodos customizados chamados `Contains` não são
classificados como operações de framework apenas pelo nome.

## BIG1002 - Materialização Dentro de Iteração

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação de materialização |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### O Que Detecta

`BIG1002` reporta materializadores LINQ suportados, como `ToList`, `ToArray`,
`ToDictionary` e `ToHashSet`, quando executam dentro de uma iteração analisável.

### Por Que Importa

Materialização pode enumerar a fonte e alocar um resultado em cada iteração.

### Exemplo Que Dispara

```csharp
foreach (var customer in customers)
{
    var copy = items.ToList();
}
```

### Exemplo Que Não Dispara

```csharp
var copy = items.ToList();

foreach (var customer in customers)
{
    _ = copy.Count;
}
```

### Raciocínio de Complexidade

O analyzer reporta apenas quando o materializador é uma operação conhecida
suportada, o tamanho da fonte é conhecido, a iteração envolvente é analisável e a
contribuição aninhada pode ser composta.

### Orientação

Considere mover a materialização para fora do loop quando o resultado
materializado não depender da iteração atual e a alocação repetida não for
necessária.

### Limitações

O analyzer não comprova que a materialização seja desnecessária. Materialização
repetida pode ser necessária quando a fonte ou o snapshot desejado dependem da
iteração atual.

## BIG1003 - Ordenação Dentro de Iteração

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação de ordenação deferred |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### O Que Detecta

`BIG1003` reporta operações de ordenação suportadas, como `OrderBy`,
`OrderByDescending`, `ThenBy` e `ThenByDescending`, quando o analyzer consegue
comprovar que a ordenação deferred é consumida dentro de uma iteração analisável.

### Por Que Importa

Ordenar em cada iteração pode dominar o custo do corpo do loop.

### Exemplo Que Dispara

```csharp
foreach (var customer in customers)
{
    var sorted = items.OrderBy(item => item).ToList();
}
```

### Exemplo Que Não Dispara

```csharp
var query = items.OrderBy(item => item);

foreach (var customer in customers)
{
    _ = customer;
}

var sorted = query.ToList();
```

### Raciocínio de Complexidade

A criação da ordenação deferred isoladamente é tratada como trabalho de setup. O
diagnóstico só é emitido quando um consumidor imediato suportado, como `ToList`,
consome a sequência ordenada dentro do loop. O custo reportado da operação é o
custo conhecido da ordenação consumida.

### Orientação

Considere ordenar uma vez fora do loop quando a ordenação e a fonte não dependem
da iteração atual.

### Limitações

O analyzer não move a ordenação automaticamente e não afirma que ordenar fora do
loop preserva semântica. Ele reporta apenas ordenação consumida cujo custo pode
ser comprovado.

## BIG1004 - Chamada Dependente de Entrada Dentro de Iteração

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Invocação do método-fonte |
| Diagnostic properties | `operation`, `operationComplexity`, `iterationComplexity`, `combinedComplexity` |

### O Que Detecta

`BIG1004` reporta uma chamada a método-fonte suportado com complexidade conhecida
não constante e dependente da entrada quando essa chamada executa dentro de uma
iteração analisável.

### Por Que Importa

O trabalho dependente de entrada do callee pode ser repetido uma vez por iteração
no caller.

### Exemplo Que Dispara

```csharp
foreach (var customer in customers)
{
    CheckAgainstBlacklist(customer, blocked);
}

private static void CheckAgainstBlacklist(int customer, int[] blocked)
{
    foreach (var value in blocked)
    {
        _ = value + customer;
    }
}
```

### Exemplo Que Não Dispara

```csharp
foreach (var customer in customers)
{
    Check(customer);
}

private static int Check(int value) => value + 1;
```

Callees constantes não reportam.

### Raciocínio de Complexidade

O analyzer resolve um alvo de método-fonte seguro, deriva ou reutiliza um
template limitado do callee, substitui argumentos do call site e compõe o custo
conhecido do callee com o custo da iteração envolvente. Detalhes de template e
cache não são expostos.

### Orientação

Considere pré-computação, cache, memoization ou outro formato de dados quando o
resultado do callee puder ser reutilizado semanticamente entre iterações.

### Limitações

O analyzer não afirma que chamadas repetidas sejam redundantes. Ele evita
dispatch virtual/interface inseguro, métodos externos disponíveis apenas em
metadata, chamadas não resolvidas, ciclos e estimativas em fronteiras de budget.

## BIG1005 - Crescimento Recursivo Exponencial

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Identificador do método recursivo |
| Mensagem | `Recursive method '{method}' exhibits exponential growth with estimated complexity {complexity}` |
| Diagnostic properties | `complexity`, `recurrenceClass` |

### O Que Detecta

`BIG1005` reporta um método de recursão direta suportado cuja recorrência
extraída é resolvida como crescimento exponencial.

### Por Que Importa

Crescimento recursivo exponencial pode se tornar impraticável mesmo para entradas
moderadas.

### Exemplo Que Dispara

```csharp
int Fibonacci(int n)
{
    if (n <= 1)
    {
        return n;
    }

    return Fibonacci(n - 1) + Fibonacci(n - 2);
}
```

### Exemplo Que Não Dispara

```csharp
int CountDown(int n)
{
    if (n <= 1)
    {
        return 1;
    }

    return CountDown(n - 1) + 1;
}
```

Essa recorrência suportada é linear, não exponencial, então `BIG1005` não
reporta.

### Raciocínio de Complexidade

O analyzer precisa comprovar recursão direta semântica, evidência compatível de
base case, argumentos recursivos redutores, trabalho local conhecido e uma
recorrência resolvida por um solver suportado. `T(n)=T(n-1)+T(n-2)+O(1)`, no
estilo Fibonacci, é documentada como forma representativa suportada.

### Orientação

Considere memoization ou uma abordagem iterativa quando subproblemas recursivos
repetidos forem semanticamente equivalentes.

### Limitações

O diagnóstico não inclui a equação completa de recorrência porque o pipeline
atual do diagnóstico carrega a estimativa exponencial resolvida, não um contrato
público estável de texto de recorrência. Recursão não suportada permanece
`Unknown`.

## BIG1006 - Complexidade Acima do Threshold Configurado

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Localização estável do executable member |
| Mensagem | `Method '{method}' has estimated complexity {actual}, exceeding configured maximum {threshold}` |
| Diagnostic properties | `complexity`, `threshold` |

### O Que Detecta

`BIG1006` reporta um método cuja complexidade estimada conhecida e comparável é
maior que `complexity_analyzers.maximum_complexity`.

### Por Que Importa

A regra permite que um projeto aplique uma política explícita de complexidade
máxima.

### Exemplo Que Dispara

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

```csharp
void M(int[] values)
{
    foreach (var outer in values)
    {
        foreach (var inner in values)
        {
            _ = outer + inner;
        }
    }
}
```

Uma estimativa conhecida `O(n^2)` excede `O(n log n)`.

### Exemplo Que Não Dispara

```csharp
void M(int[] values)
{
    foreach (var value in values)
    {
        _ = value + 1;
    }
}
```

Com `maximum_complexity = n`, igualdade não reporta. Apenas estimativas
conhecidas estritamente maiores reportam.

### Raciocínio de Complexidade

O analyzer reporta apenas quando a estimativa do método é conhecida, o threshold
configurado é concreto, a estimativa e o threshold são comparáveis e a comparação
retorna maior.

### Orientação

Considere reduzir o trabalho dominante comprovado do método, separar
responsabilidades quando isso melhorar clareza, ou ajustar o threshold
configurado quando o projeto aceitar intencionalmente o custo.

### Limitações

`Unknown` e expressões multivariadas incomparáveis não produzem diagnóstico de
threshold. `BIG1006` é um sinal prático de análise estática, não uma prova
matemática universal.

## BIG2001 - Cyclomatic Complexity Acima do Threshold Configurado

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Localização estável do executable member |
| Mensagem | `Member '{member}' has cyclomatic complexity {actual}, exceeding configured maximum {threshold} ({mode} mode)` |
| Diagnostic properties | `cyclomaticComplexity`, `threshold`, `cyclomaticComplexityMode` |

### O Que Detecta

`BIG2001` reporta um executable member suportado cuja Cyclomatic Complexity
estrutural é estritamente maior que
`complexity_analyzers.maximum_cyclomatic_complexity`.

### Por Que Importa

Cyclomatic Complexity mede complexidade de caminhos de controle. Ela é
independente da complexidade algorítmica de tempo: um membro pode ser `O(n)` e,
ao mesmo tempo, possuir muitos branches, guards e caminhos independentes.

### Exemplo Que Dispara

```ini
[*.cs]

complexity_analyzers.maximum_cyclomatic_complexity = 2
dotnet_diagnostic.BIG2001.severity = warning
```

```csharp
int M(int value)
{
    if (value > 0)
    {
        return 1;
    }
    else if (value < 0)
    {
        return -1;
    }

    return 0;
}
```

O método tem baseline `1`, mais uma decisão para `if` e uma para `else if`,
então o valor real é `3`.

### Exemplo Que Não Dispara

```csharp
int M(int value)
{
    if (value > 0)
    {
        return 1;
    }

    return 0;
}
```

Com `maximum_cyclomatic_complexity = 2`, igualdade não reporta.

### Raciocínio de Complexidade

A convenção standard conta `1 + decision points`. Decision points incluem `if`,
loops, `catch`, filtros de catch, expressões condicionais, short-circuit
`&&`/`||`, cases de switch não default, arms de switch expression não discard,
arms discard com guarda em switch expression, guards `when` e patterns `or`.
`else`, `try`, `finally`, blocos simples,
initializers, `??`, `?.`, pattern `and` e pattern `not` não adicionam pontos por
si só.

No modo `modified_mccabe`, cada switch statement ou switch expression contribui
uma decisão para a família de `switch`, em vez de uma por case não default, arm
não discard ou arm discard com guarda. Guards e patterns `or` continuam
adicionando pontos.

### Orientação

Considere dividir ou simplificar o fluxo de controle quando um membro acumula
muitos caminhos independentes e o threshold do projeto representa a política de
manutenibilidade do time.

### Limitações

Este diagnóstico não é uma estimativa Big-O e não afirma que o membro seja lento.
Local functions, lambdas e anonymous methods aninhados são analisados como
executable members próprios em vez de inflarem o pai lexical.

## BIG2002 - Maximum Nesting Depth Acima do Threshold Configurado

| Propriedade | Valor |
| --- | --- |
| Categoria | `Complexity` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `true` |
| Localização | Localização estável do executable member |
| Mensagem | `Member '{member}' has maximum control-flow nesting depth {actual}, exceeding configured maximum {threshold}` |
| Diagnostic properties | `maximumNestingDepth`, `threshold` |

### O Que Detecta

`BIG2002` reporta um executable member suportado cuja Maximum Control-Flow
Nesting Depth é estritamente maior que
`complexity_analyzers.maximum_nesting_depth`.

### Por Que Importa

Maximum nesting depth mede quão profundamente estruturas de fluxo de controle
estão aninhadas, não quantos caminhos independentes existem. A métrica é
independente de Big-O e Cyclomatic Complexity. Muitas decisões planas podem ter
nesting baixo; poucas decisões profundamente aninhadas podem ter nesting alto.

### Exemplo Que Dispara

```ini
[*.cs]

complexity_analyzers.maximum_nesting_depth = 2
dotnet_diagnostic.BIG2002.severity = warning
```

```csharp
void M(int[] values, bool flag)
{
    if (flag)
    {
        foreach (var value in values)
        {
            if (value > 0)
            {
            }
        }
    }
}
```

A cadeia mais profunda é `if` -> `foreach` -> `if`, então o depth real é `3`.

### Exemplo Que Não Dispara

```csharp
void M(bool a, bool b, bool c)
{
    if (a)
    {
    }

    if (b)
    {
    }

    if (c)
    {
    }
}
```

Os três `if` são branches irmãos, então o depth máximo é `1`, não `3`.

### Regras de Nesting

| Construct | Adiciona nível de nesting? |
| --- | --- |
| `if` | Sim |
| `else if` na mesma cadeia | Não adiciona nesting artificial da cadeia; o `else if` é avaliado no depth da cadeia |
| `else` | Não |
| `for`, `foreach`, `while`, `do` | Sim |
| statement `switch` | Sim |
| seção/case/default de switch | Não |
| switch expression | Sim |
| arm de switch expression | Não |
| `try` | Sim |
| `catch` e `finally` | Não adicionam nível próprio; são branches irmãos do `try` |
| expressão condicional `?:` | Sim |
| `&&`, `||`, patterns e guards `when` | Não |
| `lock`, `using`, `fixed`, `checked`, `unchecked` | Não |
| bloco léxico simples | Não |
| initializers de object, collection, array, property e anonymous object | Não |
| body de local function, lambda ou anonymous method aninhado no pai | Não |

### Comportamento de Threshold

O threshold é opt-in. Configuração ausente ou inválida não produz `BIG2002`.
Valores válidos são inteiros decimais não negativos. Igualdade não reporta;
apenas valor real estritamente maior reporta.

### Orientação

Considere achatar ou extrair branches profundamente aninhados quando o threshold
do projeto representar uma política de manutenibilidade e a refatoração preservar
o fluxo de controle pretendido.

### Limitações

Este diagnóstico não é métrica de quantidade de caminhos, estimativa Big-O,
contagem de linhas, contagem de statements, contagem de tokens nem Cognitive
Complexity. Ele não conta nesting sintático simples e não infere execução por
bodies executáveis aninhados apenas porque foram declarados dentro do membro pai.

## BIG9000 - Probe de Execução do Analyzer

| Propriedade | Valor |
| --- | --- |
| Categoria | `Infrastructure` |
| Severidade padrão | `Info` |
| Habilitado por padrão | `false` |
| Localização | Início de um arquivo-fonte quando disponível; caso contrário, sem localização |
| Mensagem | `ComplexityAnalysis.Analyzers execution probe is active` |
| Diagnostic properties | `diagnosticRole` |

### O Que Detecta

`BIG9000` comprova que o pacote do analyzer foi carregado, inicializado e
executado.

### Por Que Importa

Ele é útil para smoke tests de consumo do pacote e validação de compatibilidade.

### Exemplo Que Dispara

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

### Exemplo Que Não Dispara

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

### Raciocínio de Complexidade

Nenhum. Este é um probe de infraestrutura, não uma regra de análise de
complexidade.

### Orientação

Habilite temporariamente apenas ao validar carregamento do analyzer e depois
desabilite novamente.

### Limitações

`BIG9000` não é recomendação de performance e não indica que algum código-fonte
seja custoso. Quando habilitado, reporta no máximo uma vez por compilation.

## Subconjunto de Operações Conhecidas

O analyzer documenta deliberadamente um conjunto limitado de operações
conhecidas.

Exemplos BCL incluem operações selecionadas de:

- `List<T>`;
- `Dictionary<TKey,TValue>`;
- `HashSet<T>`;
- arrays;
- strings.

Operações LINQ imediatas/terminais suportadas incluem:

- `Any`, `All`, `Contains`, `Count`, `LongCount`;
- `ToList`, `ToArray`, `ToDictionary`, `ToHashSet`;
- `Sum`, `Min`, `Max`, `Aggregate`.

Operações deferred suportadas incluem:

- `Where`, `Select`, `SelectMany`;
- `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`;
- `Distinct`, `GroupBy`.

Mappings são baseados nos símbolos resolvidos. Métodos do usuário com o mesmo
nome não são mapeados automaticamente.

A criação de uma pipeline deferred é tratada como trabalho de setup. O custo de
enumeração ou ordenação só é cobrado quando consumo suportado é comprovado.

## Escopo de Chamadas-Fonte Suportadas

A análise interprocedural é limitada a métodos-fonte ordinários e local functions
diretas na mesma Roslyn `Compilation` quando o dispatch é seguro.

Formas suportadas incluem:

- métodos static;
- métodos private;
- métodos ordinários não virtuais;
- dispatch sealed quando o alvo de runtime pode ser comprovado.
- invocações diretas de local functions.

O traversal é sob demanda e limitado. A profundidade máxima padrão é `5`,
configurável até `16`. O máximo padrão de expansões de métodos-fonte por raiz é
`32`, configurável até `128`.

Ficam fora do escopo de callee suportado dispatch virtual/interface inseguro,
dynamic dispatch, assemblies externos, construtores, accessors de propriedade,
accessors de evento, operadores, conversões, lambdas, anonymous methods, call
graphs de compilation inteira e análise de solution inteira.

Ciclos são detectados de forma conservadora. Recursão direta pode ser delegada ao
pipeline de recorrências; recursão mútua continua sem suporte para resolução.

## Escopo de Recursão Direta Suportada

Uma recorrência só pode ser resolvida quando o analyzer consegue comprovar
recursão direta semântica, evidência compatível de base case, argumento
recursivo redutor e trabalho local conhecido.

As famílias suportadas incluem:

- formas de soma/decremento como `T(n)=T(n-c)+f(n)` para tolls suportados;
- um subconjunto exponencial simples e limitado, incluindo formatos no estilo
  Fibonacci;
- formas do Master Theorem;
- um subconjunto restrito/limitado de Akra-Bazzi com termos recursivos por escala
  suportados.

Resultados representativos:

```text
T(n)=T(n-1)+1               => O(n)
T(n)=T(n-1)+n               => O(n^2)
T(n)=T(n-1)+log n           => O(n log n)
2T(n-1)+1                   => O(2^n)
T(n-1)+T(n-2)+1             => O(1.618^n)
T(n/2)+1                    => O(log n)
2T(n/2)+n                   => O(n log n)
3T(n/2)+n                   => O(n^1.585)
T(n/3)+T(2n/3)+n            => O(n log n)
```

O analyzer não implementa Akra-Bazzi completo, resolução arbitrária por
polinômio característico, parsing simbólico geral de recorrências, integração
numérica geral ou integração com solvers externos MathNet/SymPy.

Casos não suportados permanecem `Unknown`.

## Configuração

Use a configuração padrão de severidade do Roslyn:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Opções comportamentais, como budgets de análise e threshold máximo de
complexidade, estão documentadas em [Configuração](configuration.md).

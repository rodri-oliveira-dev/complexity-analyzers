# Ferramentas Em Nível De Projeto

[English](../en/project-tooling.md) | Português (Brasil)

`ComplexityAnalysis.Tool` fornece o comando instalável `complexity-analysis`
para relatórios determinísticos de projetos e solutions C#, snapshots,
comparação de regressões, detecção de duplicação e quality gates de CI.

Escolha o pacote Roslyn `ComplexityAnalysis.Analyzers` para diagnósticos durante
a compilação, esta CLI para execuções explícitas em projetos ou solutions e o
host MCP local separado para integração somente leitura com clientes MCP.
Nenhum deles instala ou exige os demais.

## Pré-Requisitos E Instalação

A ferramenta targeteia `net10.0` e exige o runtime .NET 10. Depois que a versão
`1.0.0` for publicada no NuGet.org, instale-a globalmente:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.0
complexity-analysis --version
```

Para uma instalação fixada no repositório:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.0
dotnet tool restore
dotnet tool run complexity-analysis --version
```

Mantenha uma instalação global com `dotnet tool update --global
ComplexityAnalysis.Tool --version <version>` e `dotnet tool uninstall --global
ComplexityAnalysis.Tool`. Para um manifest local, omita `--global`; execute com
`dotnet tool run complexity-analysis` ou `dotnet complexity-analysis`.

Esses comandos consomem um pacote publicado. Para um pacote ainda não publicado
e compilado deste repositório, use uma fonte explícita e uma versão somente local:

```bash
dotnet pack src/ComplexityAnalysis.Tool/ComplexityAnalysis.Tool.csproj \
  --configuration Release \
  -p:Version=0.0.0-local \
  -p:PackageVersion=0.0.0-local \
  --output artifacts/local-packages
dotnet tool install ComplexityAnalysis.Tool \
  --version 0.0.0-local \
  --add-source artifacts/local-packages \
  --tool-path artifacts/local-tools
export PATH="$PWD/artifacts/local-tools:$PATH"
complexity-analysis --version
```

## Executar A CLI

Analise um projeto ou uma solution com o comando instalado:

```bash
complexity-analysis analyze MySolution.slnx
complexity-analysis analyze MyProject.csproj --format json
complexity-analysis analyze MyProject.csproj --format sarif
complexity-analysis analyze MyProject.csproj --max-cyclomatic-complexity 10
```

Entradas suportadas:

- `.csproj`;
- `.slnx`;
- `.sln`.

Arquivos de solution são lidos de forma determinística e cada projeto C#
referenciado é analisado independentemente. O carregamento é intencionalmente
local ao repositório e não adiciona `Microsoft.CodeAnalysis.Workspaces` ao
pacote do analyzer.

Use `complexity-analysis --help`, `complexity-analysis analyze --help` ou
`complexity-analysis compare --help` para a ajuda embutida na versão instalada.

## Referência Da CLI

`analyze <path>` aceita exatamente um path `.csproj`, `.slnx` ou `.sln`.

| Opção | Valor/default | Regras |
| --- | --- | --- |
| `--format` | `console` (padrão), `json`, `sarif` | Case-insensitive. |
| `--output` | Path; stdout por padrão | O diretório pai deve existir. |
| `--include-evidence` | Desabilitado | Exige JSON ou SARIF. |
| `--snapshot` | Desabilitado | Exige JSON e implica evidência. |
| `--include-generated` | Desabilitado | Inclui código-fonte gerado. |
| `--include-build-output` | Desabilitado | Inclui diretórios de output normalmente excluídos. |
| `--max-complexity` | Sem gate | `constant`, `log_n`, `linear`/`n`, `n_log_n`, `quadratic`/`n2`, `cubic`/`n3`, `exponential` ou `factorial`. |
| `--max-cyclomatic-complexity` | Sem gate | Inteiro positivo. |
| `--max-nesting-depth` | Sem gate | Inteiro não negativo. |
| `--max-method-nloc` | Sem gate | Inteiro positivo. |
| `--max-statement-count` | Sem gate | Inteiro positivo. |
| `--max-token-count` | Sem gate | Inteiro positivo. |
| `--max-parameters` | Sem gate | Inteiro não negativo. |
| `--max-cognitive-complexity` | Sem gate | Inteiro não negativo. |
| `--detect-duplicates` | Desabilitado | Habilita detecção normalizada de duplicação. |
| `--min-duplicate-tokens` | `40` | Inteiro positivo; também habilita detecção. |
| `--min-duplicate-lines` | Não definido | Inteiro positivo; também habilita detecção. |
| `--max-duplicate-rate` | Sem gate | Número não negativo em cultura invariável; também habilita detecção. |
| `--max-duplicate-tokens` | Sem gate | Inteiro positivo; também habilita detecção. |

`compare <baseline.json> <current.json>` exige dois arquivos
`complexity-snapshot/1.0`. Ele aceita `--format console|json|sarif` (console por
padrão), `--output <path>` e o gate opt-in `--fail-on-regression`. Opções
desconhecidas ou valores ausentes retornam o exit code `1`.

## Saída

A saída console é o padrão:

```bash
complexity-analysis analyze samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj
```

JSON está disponível para automação:

```bash
complexity-analysis analyze samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj --format json --output artifacts/project-report.json
```

Para criar um baseline de comparação, adicione `--snapshot`. Esse modo emite o
contrato independente `complexity-snapshot/1.0`, inclui evidências determinísticas
e registra origem e configuração normalizadas. Os membros usam IDs de documentação
do Roslyn, portanto overloads permanecem distintos e mudanças de linha ou arquivo
não alteram a identidade. Renames aparecem como remoção e adição; identidades
duplicadas são ambíguas e nunca são associadas por heurística.

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj --format json --snapshot --output artifacts/baseline.json
```

O contrato JSON padrão permanece no schema `1.0`. Evidências determinísticas
de complexidade são opt-in e usam o schema `1.1`:

```bash
complexity-analysis analyze samples/ComplexityAnalysis.Sample/ComplexityAnalysis.Sample.csproj --format json --include-evidence
```

`--include-evidence` é válido com `--format json` ou `--format sarif`. Cada membro passa a
incluir o trace limitado produzido pelo core de análise: identidade estável do
passo, operação e origem, intervalo relativo no código-fonte, dimensões de
entrada, garantia e hipóteses da estimativa, composição, estado de truncamento e
um motivo explícito quando o resultado final é `Unknown`. As localizações das
evidências são relativas ao membro analisado e nunca contêm um path absoluto do
filesystem.

Os dois formatos são ordenados por path de projeto, path de arquivo, localização
do membro e localização de clone. O JSON mantém métricas `Unknown` explícitas em
vez de transformar ausência de informação em `0` ou `O(1)`.

## Métricas Agregadas

O relatório agregado inclui identidade de projeto, arquivo, membro, símbolo e
source range. Para cada executable member suportado ele reporta:

- Big-O, incluindo `Unknown` explícito;
- Cyclomatic Complexity;
- Maximum Control-Flow Nesting Depth;
- NLOC;
- statement count;
- token count;
- parameter count;
- Cognitive Complexity;
- métricas Halstead primitivas e derivadas.

A CLI reutiliza os calculadores de métricas do analyzer. Ela não reimplementa
fórmulas em uma pilha paralela de reporting.

## Exclusões

Por padrão, a ferramenta exclui código gerado e diretórios comuns de saída:

- nomes como `.g.cs`, `.g.i.cs`, `.designer.cs` e `.generated.cs`;
- arquivos com marcador inicial `<auto-generated`;
- `bin`, `obj`, `artifacts`, `TestResults`, `packages`;
- pastas de infraestrutura como `.git`, `.vs`, `.idea`, `.vscode` e
  `node_modules`.

Use `--include-generated` ou `--include-build-output` apenas quando o relatório
em nível de projeto realmente precisar desses arquivos.

## Quality Gates

Quality gates são opt-in. Nenhum threshold padrão falha um build.

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj \
  --max-cyclomatic-complexity 10 \
  --max-nesting-depth 3 \
  --max-method-nloc 40 \
  --max-statement-count 25 \
  --max-token-count 300 \
  --max-parameters 5 \
  --max-cognitive-complexity 15
```

Thresholds de Big-O aceitam `constant`, `log_n`, `linear`/`n`, `n_log_n`,
`quadratic`/`n2`, `cubic`/`n3`, `exponential` e `factorial`.

Exit codes:

- `0`: análise concluída e gates configurados passaram;
- `1`: erro de linha de comando, carregamento de projeto, parsing ou escrita de
  relatório;
- `2`: pelo menos um quality gate explicitamente configurado falhou;
- `130`: cancelamento solicitado.

## Comparar Snapshots

Compare dois snapshots gerados anteriormente sem invocar Git ou serviço externo:

```bash
complexity-analysis compare artifacts/base.json artifacts/head.json --format json
```

Use `--format console` (padrão) ou `--format json`, além de `--output <path>`
quando necessário. Os findings distinguem regressões, melhorias, equivalência,
incomparabilidade e membros adicionados, removidos ou ambíguos.
`--fail-on-regression` é opt-in e retorna o exit code `2` somente para regressões
comprovadas. `Unknown` e garantias, hipóteses, configurações ou dimensões de
entrada incompatíveis não falham o gate.

### Check opt-in de regressão no CI

O workflow `Complexity Analyzers CI` expõe a entrada booleana
`run_complexity_regression` para execuções manuais. Quando habilitada, ela gera
um snapshot novo do sample executável, compara com o snapshot revisado em
`performance/baselines`, falha somente para regressões comprovadas, envia os
arquivos JSON de baseline/atual/comparação no artefato
`complexity-regression` e escreve as contagens por classificação no resumo do
job.

Esse job é intencionalmente opt-in e somente leitura. Ele não comenta em pull
requests, não altera o repositório, não publica pacotes e não solicita permissão
além de `contents: read`. Atualize o baseline revisado somente quando uma
mudança intencional da análise tiver sido validada e explicada no pull request.

## SARIF 2.1.0

Tanto `analyze` quanto `compare` aceitam `--format sarif`. A saída segue SARIF
2.1.0 e usa URIs de artefatos relativas e percent-encoded, partial fingerprints
SHA-256 estáveis, regras declaradas, regiões de código-fonte e ordem determinística.

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj --format sarif --include-evidence --output artifacts/complexity.sarif
complexity-analysis compare artifacts/base.json artifacts/head.json --format sarif --output artifacts/comparison.sarif
```

Estimativas conhecidas de membros são notes. Um membro com complexidade `Unknown`
só é emitido quando a evidência contém um motivo explícito legítimo. Regressões
comprovadas da comparação são warnings; os demais findings são notes. Selecionar
SARIF não habilita quality gate nem altera exit codes: `--fail-on-regression`
continua sendo uma opção separada e opt-in.

## Detecção De Duplicação

A detecção de duplicação também é opt-in:

```bash
complexity-analysis analyze src/MyProject/MyProject.csproj \
  --detect-duplicates \
  --min-duplicate-tokens 40 \
  --max-duplicate-rate 5
```

O detector reporta clone groups no mesmo arquivo e entre arquivos, com source
ranges, token count normalizado, occurrence count, duplicate token count e taxa
de duplicação. `--min-duplicate-lines` adiciona um filtro por linhas de código
depois do matching por tokens.

A normalização é C#-aware:

- parâmetros, locals, constantes locais e pattern variables são normalizados por
  papel/ordem estável;
- literais numéricos, string e char são normalizados por categoria;
- booleanos e `null` continuam distintos;
- identificadores de membros, tipos e namespaces preservam papéis sintáticos em
  vez de colapsar em um único token de identificador;
- tokens unários, binários, assignment, member access, generics, patterns,
  interpolation e control flow preservam identidades distintas.

Trocar `+` por `-`, `&&` por `||`, `if` por `while`, member access por element
access ou forma genérica por não genérica altera a sequência normalizada.

O algoritmo indexa janelas fixas de tokens normalizados com fingerprint FNV-1a e
depois verifica cada candidato de hash comparando a sequência normalizada. Um
match de hash sozinho nunca é suficiente para reportar clone. Candidatos
verificados são estendidos, e findings sobrepostos ou contidos são suprimidos
deterministicamente mantendo primeiro o maior grupo e depois a ordem de fonte
mais antiga.

A complexidade esperada é limitada por quantidade de tokens, tamanho da janela e
verificação de candidatos com janelas repetidas. A implementação evita comparar
todos os pares de todas as substrings.

## Fronteira Arquitetural

Carregamento de projetos, traversal de filesystem, renderização JSON/console,
quality gates e detecção de duplicação vivem em `ComplexityAnalysis.Tool`.
`ComplexityAnalyzer` não escaneia projetos, solutions ou arquivos em
`Initialize` nem em callbacks de analyzer.

O pacote do analyzer permanece um asset Roslyn `netstandard2.0` em
`analyzers/dotnet/cs/`. A CLI pode usar dependências de tooling do repositório,
mas essas dependências não são expostas aos consumidores do pacote do analyzer.

## Integração com GitHub Actions

Para integrar a análise ao CI do GitHub sem manter scripts próprios, use a
[Complexity Analysis Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action#quick-start).
Sua [release estável `v1.0.0`](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases/tag/v1.0.0)
foi publicada em 30/10/2026 e instala, por padrão, a versão exata publicada
`1.0.1` de `ComplexityAnalysis.Tool`.

Use a CLI diretamente para execução local ou orquestração personalizada no CI;
prefira a Action para workflows reutilizáveis do GitHub Actions com relatórios
JSON/SARIF, quality gates opcionais e comparação de regressões em pull requests.
Ambas usam o mesmo motor de análise, preservando as métricas e a semântica dos
snapshots. O versionamento da Action é independente da CLI, do Analyzer e do MCP.

Para maior reprodutibilidade, fixe a Action no commit revisado da `v1.0.0`,
`2d807ef29ca03c685d224543a135d8c30dfe2c67`; a tag publicada `@v1.0.0`
também está disponível. A referência móvel `@v1` ainda não foi criada. Consulte
o [guia da GitHub Action](github-action.md) e os
[exemplos publicados](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/v1.0.0/examples) para
informações sobre inputs e segurança.

# GitHub Action: análise de complexidade C# no CI

[English](../en/github-action.md) · [Repositório da Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action)

A `Complexity Analysis Action` disponibiliza a [ComplexityAnalysis.Tool](project-tooling.md) em workflows do GitHub Actions. Ela **não** implementa outro motor de análise. Use o pacote Roslyn para diagnósticos na IDE/compilação, a CLI para execução local ou automações próprias, o MCP para ferramentas de IA e a GitHub Action para verificações reutilizáveis em CI e feedback nos pull requests.

A Action fica em um **repositório separado** e possui ciclo de releases independente. A primeira versão estável planejada é `v1.0.0`; **na data desta atualização ela ainda não foi publicada**, portanto `@v1` e `@v1.0.0` não devem ser tratados como tags existentes. Consulte o [repositório da Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action) para confirmar o estado atual.

## O que ela oferece

- Análise de projetos `.csproj`, `.sln` e `.slnx` em runners Ubuntu compatíveis.
- Relatórios JSON determinísticos, SARIF 2.1.0 e GitHub Job Summary.
- Quality gates opcionais para Big-O, complexidade ciclomática/cognitiva, aninhamento, NLOC por método e duplicação.
- Snapshots da base e do PR para identificar regressões de complexidade **comprovadas**.
- Upload opcional de SARIF no GitHub Code Scanning e comentário resumido no PR, atualizado sem gerar duplicações.

Uma estimativa de complexidade **não** equivale a benchmark de desempenho nem comprova latência real. Evidências desconhecidas, ambíguas ou incomparáveis não são regressões confirmadas.

## Requisitos e versionamento

A candidata atual suporta Ubuntu com Bash, Python 3 e acesso ao NuGet.org. Ela instala o SDK .NET 10 e a versão **exata** da `ComplexityAnalysis.Tool` configurada em `tool-version` (atualmente `1.0.1` por padrão). CLI, Action, Analyzer e MCP possuem **versões independentes**.

Até a primeira release estável, utilize no `uses:` um **SHA completo de 40 caracteres, revisado**, do repositório da Action. O marcador do exemplo abaixo não é executável sem substituição. Após a publicação, será possível utilizar uma tag específica, como `@v1.0.0`, ou o SHA completo. `@v1` só ficará disponível quando a referência da versão principal for criada.

## Exemplo: analisar um pull request

Crie `.github/workflows/complexity.yml` no repositório **consumidor**, substituindo `REVIEWED_FULL_40_CHARACTER_SHA` e `MySolution.slnx`:

```yaml
name: Complexity analysis

on:
  pull_request:

permissions:
  contents: read

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd
        with:
          persist-credentials: false
      - id: complexity
        uses: rodri-oliveira-dev/complexity-analysis-action@REVIEWED_FULL_40_CHARACTER_SHA
        with:
          path: MySolution.slnx
          max-cyclomatic-complexity: "15"
          max-cognitive-complexity: "20"
          fail-on-quality-gate: "true"
          compare-with-base: "true"
          fail-on-regression: "true"
```

Esse exemplo funciona com **permissões de leitura**: não publica comentários, não envia SARIF e não expõe um token de escrita. Falhas de comparação são apresentadas separadamente das regressões comprovadas. Para guardar os relatórios após o job, acrescente um passo `actions/upload-artifact` utilizando os caminhos retornados pela Action.

## Entradas e saídas

O parâmetro `path` é obrigatório. As entradas principais são:

| Entrada | Padrão | Finalidade |
| --- | --- | --- |
| `tool-version` | `1.0.1` | Versão publicada e exata da CLI |
| `output-directory` | `complexity-analysis-reports` | Pasta de saída |
| `detect-duplicates` | `false` | Habilita detecção de duplicação |
| `max-complexity` | não definido | Limite de classe Big-O suportada |
| `max-cyclomatic-complexity` | não definido | Limite de complexidade ciclomática |
| `max-cognitive-complexity` | não definido | Limite de complexidade cognitiva |
| `max-nesting-depth` | não definido | Limite de aninhamento |
| `max-method-nloc` | não definido | Limite de NLOC por método |
| `max-duplicate-rate` | não definido | Limite de duplicação |
| `fail-on-quality-gate` | `true` | Falha o job quando um limite configurado é violado |
| `compare-with-base` | `false` | Compara o PR com a revisão-base |
| `base-ref` / `baseline-path` | não definidos | Define referência Git ou snapshot existente |
| `fail-on-regression` | `false` | Falha apenas para regressões comprovadas |
| `upload-sarif` | `false` | Envio opcional para Code Scanning |
| `comment-on-pr` | `false` | Comentário opcional, exige permissão adicional |

Outras entradas incluem `include-generated` e `comparison-output`. Para detalhes completos e restrições, consulte o [contrato de inputs da Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action#inputs).

As saídas incluem `report-path`, `sarif-path`, `analysis-status`, `quality-gate-status` e `tool-version`. Comparações também disponibilizam `baseline-report-path`, `current-report-path`, `comparison-report-path`, `regression-count`, `comparison-status` e `pr-report-path`. Os caminhos apontam para arquivos gerados durante o job, **não para downloads permanentes**.

## Permissões e feedback seguro

| Modo | Permissões relevantes mínimas |
| --- | --- |
| Análise, quality gates e regressões | `contents: read` |
| Upload de SARIF | `contents: read`, `security-events: write` |
| Comentário direto em PR confiável do mesmo repositório | `contents: read`, `pull-requests: write` |
| Comentário em PR de fork | Análise somente leitura e publicação privilegiada separada via `workflow_run` |

Nunca analise código de PR não confiável usando `pull_request_target`, nem exponha segredos ou credenciais de escrita ao job que carrega projetos externos. A avaliação de projetos MSBuild pode executar lógica definida no repositório; a Action **não é um sandbox**. Para PRs de forks, mantenha a análise somente leitura e avalie o [exemplo de publicador isolado](https://github.com/rodri-oliveira-dev/complexity-analysis-action/blob/feature/release-readiness/examples/secure-pr-publish.yml) antes de copiar o workflow. A validação da procedência do artefato é obrigatória.

## Solução de problemas

- **Action não encontrada em `@v1` ou `@v1.0.0`:** a release e as tags podem não existir. Confira as [releases](https://github.com/rodri-oliveira-dev/complexity-analysis-action/releases) e, na avaliação, use um SHA completo revisado.
- **Runner não suportado:** a candidata documenta apenas Ubuntu; não há validação end-to-end equivalente para Windows/macOS.
- **Projeto ausente ou comparação indisponível:** confira o caminho e se o projeto existe nas revisões-base e atual. Baseline ausente **não** significa ausência de regressões.
- **Falha de instalação do NuGet:** confira o acesso à rede, o requisito de .NET 10 e a versão publicada exata de `tool-version`.
- **Upload SARIF ou comentário negado:** confira permissões, disponibilidade do Code Scanning, restrições de forks e isolamento entre análise e publicação.
- **Quality gate falhou:** violar um limite absoluto é diferente de introduzir uma regressão. Consulte `quality-gate-status`, `comparison-status` e os relatórios JSON.

Os exemplos completos estão no [repositório da Action](https://github.com/rodri-oliveira-dev/complexity-analysis-action/tree/feature/release-readiness/examples). Para executar a CLI diretamente, consulte [Ferramentas em Nível de Projeto](project-tooling.md).

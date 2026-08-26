# Governança de Qualidade de Release

[English](../../en/development/quality-gates.md) | [Português (Brasil)](quality-gates.md)

Esta é a Definition of Done canônica de `ComplexityAnalysis.Analyzers`.
Specifications por issue, orientação de contribuição, agentes e pull requests
devem apontar para este documento em vez de redefinir a mesma política.

## Princípio

A validação deve ser proporcional ao risco.

Um typo no README não precisa do harness de performance. Uma mudança de
dependência Roslyn precisa de evidência de compatibilidade de host e pacote. Uma
mudança de comportamento de diagnóstico precisa de testes triggering e
non-triggering, documentação e release tracking quando o contrato público do
analyzer muda.

Comece toda mudança respondendo:

1. Que tipo de mudança é esta?
2. Qual risco público, de pacote, performance, compatibilidade, documentação,
   segurança ou release ela possui?
3. Que evidência prova que esses riscos foram tratados?
4. Quais validações intencionalmente não se aplicam?

## Matriz Por Tipo De Mudança

| Tipo de mudança | Exemplos | Evidência obrigatória | Normalmente não exige |
| --- | --- | --- | --- |
| Somente documentação | README, docs, typo, links, exemplos sem alterar comportamento. | Revisão Markdown, links coerentes, alinhamento EN/PT-BR quando conteúdo público equivalente muda. | Harness de performance do analyzer, matriz de consumer package, release tracking. |
| Somente testes | Caracterização, testes de regressão, fixtures. | Restore/build/test; provar que não houve mudança funcional inesperada. | Validação de performance salvo quando harness, workload ou infraestrutura de performance mudam. |
| Comportamento do analyzer | Nova análise, mudança de heurística, composição de complexidade, recursão ou interprocedural. | Testes triggering e non-triggering; #31 continua verde ou before/after intencional e documentado; revisão de falso positivo; `Unknown` conservador; revisão de cancellation/concurrency; avaliação de performance; docs públicas quando visível ao usuário. | Validação de pacote além dos gates normais salvo se dependências, target framework, packaging ou carregamento por consumidor mudarem. |
| Novo diagnóstico | Novo rule ID, descriptor, mensagem, categoria, severidade, habilitação, reporting. | ID único; contrato do descriptor; testes triggering/non-triggering; UX conforme #34; catálogo EN/PT-BR; docs de config se configurável; `AnalyzerReleases.Unshipped.md`; impacto de performance; compatibilidade de package preservada. | Publicação de release ou automação de version bump. |
| Mensagem ou UX de diagnóstico | Texto, propriedades, localização, guidance. | Convenção #34; testes determinísticos de mensagem/propriedades; release tracking quando metadata pública muda; docs quando guidance público muda; provar que triggering não mudou sem intenção. | Harness de performance salvo se a geração de explicação tocar hot paths materialmente. |
| Configuração | Nova chave, default, valores válidos, fallback, boundaries. | Chave documentada; default e valores permitidos documentados; fallback inválido testado; testes de boundary; docs EN/PT-BR; release tracking; revisão de backwards compatibility. | Matriz de consumer package salvo se package ou host mudarem. |
| Mudança sensível a performance | Traversal, caches, recurrence solver, resolução semântica, interprocedural, known operations. | Gates estruturais #32; testes/harness de performance relevantes; boundedness; cancellation; revisão de cache ownership; justificativa para regressão intencional. | Thresholds frágeis de wall-clock como única prova. |
| Roslyn ou dependência | `Microsoft.CodeAnalysis*`, packages de autoria do analyzer, dependências de teste/build. | Motivo; matriz de compatibilidade #33; contrato de package; evidência de hosts suportados; análise de dependency leakage; revisão de `PrivateAssets`; sem Workspaces sem decisão explícita de arquitetura; política do Dependabot respeitada. | Docs públicas salvo se política de suporte ou comportamento de consumidor mudar. |
| Packaging | Configuração de pack no `.csproj`, layout `.nupkg`, metadata, carregamento por consumidor. | Pack local; inspeção do `.nupkg`; validar path de analyzer; sem regressão para `lib/`; sem dependency leakage; teste de consumidor; revisão de compatibilidade. | Caracterização diagnóstica além dos testes normais salvo se comportamento mudou. |
| CI ou workflow | GitHub Actions, permissões, required checks, nomes de workflows/jobs. | Menor privilégio; revisão de eventos/permissões; revisão de pin/versão; compatibilidade com branch protection/ruleset; gates não enfraquecidos; sem novo `continue-on-error` em validação crítica; side effects de release avaliados. | Testes de comportamento do analyzer salvo se a seleção de testes ou a semântica de validação mudar. |
| Release | Workflow de release, tags, NuGet, GitHub Packages, GitHub Release, versionamento. | Intenção explícita do maintainer; comportamento em `main` e semver; retry/idempotência; permissões Trusted Publishing/OIDC; imutabilidade de tag; destinos de publicação. | Executar release de produção durante desenvolvimento normal. |
| Futuro CLI ou nível de projeto | Project scanning, relatórios agregados, carregamento de filesystem/projetos, saída JSON/console. | Manter trabalho project-level fora dos hot paths do `DiagnosticAnalyzer`; testar filesystem/project loading separadamente; provar que o runtime do package do analyzer permanece limpo. | Tratar I/O de CLI como aceitável em hot path do analyzer. |

## Definition Of Done

Use as dimensões que se aplicam à mudança.

### Correctness

- O comportamento esperado é especificado antes da implementação quando o
  comportamento muda.
- Testes triggering, non-triggering e de regressão são adicionados ou atualizados
  quando comportamento do analyzer muda.
- A caracterização #31 continua verde, salvo quando uma mudança deliberada
  registra `Before`, `After` e `Reason`.
- Casos não suportados ou não comprovados permanecem `Unknown`.
- Nenhum falso positivo conhecido de alta confiança é introduzido.

### Performance

- O trabalho do analyzer permanece limitado, sob demanda, cancellation-aware e
  seguro para execução concorrente.
- Hot paths não adicionam I/O de filesystem, rede, execução de processo,
  telemetria, scans obrigatórios de solution inteira ou call graph obrigatório
  da compilation inteira.
- Mudanças em traversal, recursão, caching, resolução semântica ou known
  operations satisfazem os gates estruturais #32 e registram qualquer variação
  material.

### Compatibilidade E Packaging

- O target framework do analyzer permanece `netstandard2.0`, salvo decisão
  explícita de arquitetura alterando a política de suporte.
- Compatibilidade Roslyn segue a baseline #33.
- O pacote mantém o assembly do analyzer em `analyzers/dotnet/cs/` e não regride
  para assets runtime em `lib/`.
- Dependências de autoria Roslyn permanecem privadas; consumidores não recebem
  dependências compile/runtime/transitivas desnecessárias.
- Evidência de hosts SDK suportados é fornecida para mudanças de Roslyn,
  dependência, packaging ou carregamento por consumidor.

### Experiência De Diagnóstico

- Diagnósticos públicos explicam apenas evidência que o analyzer consegue provar.
- Mensagens e propriedades são determinísticas, concisas e estáveis.
- Guidance é condicional quando a adequação semântica não pode ser comprovada.
- Localizações de diagnóstico apontam para código de usuário útil.
- Diagnósticos novos ou alterados seguem a convenção #34.

### Documentação

- Mudanças de comportamento público atualizam catálogo de analyzers, docs de
  configuração, arquitetura/primeiros passos ou README apenas quando relevante.
- Conteúdo público equivalente em `docs/en` e `docs/pt-BR` permanece
  semanticamente alinhado.
- Notas internas de arquitetura e detalhes somente de CI não exigem tradução
  duplicada, salvo quando viram orientação pública de usuário/contribuidor.

### Release Tracking E Versionamento

- Atualize `src/ComplexityAnalysis.Analyzers/AnalyzerReleases.Unshipped.md` para
  diagnósticos novos, alterados ou removidos, configuração pública ou
  comportamento público relevante para release notes.
- Mudanças públicas consideram impacto semântico: fixes normalmente são patch,
  capacidades backward-compatible normalmente são minor, e quebras de diagnóstico,
  configuração ou contrato de pacote podem ser major.
- Não automatize version bumps como parte de mudanças comuns do analyzer sem uma
  issue separada de release.

### Segurança E Dependências

- Hot paths do analyzer não acessam secrets, rede, filesystem, telemetria ou
  processos.
- Mudanças de dependência documentam propósito, impacto de segurança,
  compatibilidade e impacto no pacote.
- Mudanças de workflow e release revisam `GITHUB_TOKEN`, OIDC, environments,
  registries de pacote e permissões.
- Não commite secrets, packages locais, saídas de coverage ou artefatos de build.

## Required Checks E Mapeamento De CI

A ruleset ativa de `main` exige estes status checks pelo nome exato:

| Dimensão de qualidade | Check obrigatório | O que cobre |
| --- | --- | --- |
| Correctness e sinal de coverage | `Validate analyzer` | Restore, build Release, testes com verificação OpenCover, artifact de coverage. |
| Qualidade e análise de segurança | `SonarQube Cloud` | Análise Sonar e quality gate depois da validação. |
| Layout do pacote analyzer | `Pack analyzer` | Pack e inspeção do analyzer em `analyzers/dotnet/cs/`; rejeita asset runtime `lib/` do analyzer. |
| Validação central da solution | `Quality` | Restore, build Release, suite ampla excluindo suites focadas de package/performance. |
| Contrato de package | `Package` | Pack, package contract tests e upload do `.nupkg` local. |
| Performance | `Performance` | Suite estrutural de performance e caminho `ReportAnalyzer=true` do compiler. |
| Compatibilidade de host | `Compatibility (8.0.x)` | Smoke de consumer real com package no host SDK .NET 8. |
| Compatibilidade de host | `Compatibility (9.0.x)` | Smoke de consumer real com package no host SDK .NET 9. |
| Compatibilidade de host | `Compatibility (10.0.x)` | Smoke de consumer real com package no host SDK .NET 10. |

Regras adicionais de branch exigem pull requests, histórico linear, resolução de
threads, limites de code scanning e tags de release protegidas em `v*`.

Não renomeie workflows ou jobs exigidos casualmente. Se um nome de check precisar
mudar, trate como mudança explícita de governança e documente o impacto na ruleset.

## Menu De Validação Local

O caminho amplo comum de validação é:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

Adicione checks focados conforme o risco:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerCharacterizationBaselineTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPerformanceBudgetContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerPackageConsumerContractTests
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter AnalyzerHostCompatibilityContractTests
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Mudanças somente de documentação podem registrar uma validação mais leve, como
revisão Markdown, revisão de links, `git diff --check` e ausência de impacto no
comportamento do analyzer.

## Fluxo De Branch, PR E Issue

Use o fluxo simples:

```text
issue
  -> branch dedicada
  -> implementação
  -> validação local proporcional
  -> pull request
  -> required checks
  -> review
  -> merge
  -> release separada quando solicitada
```

Use Conventional Commits:

- `feat:` para capacidade pública backward-compatible;
- `fix:` para defeitos;
- `perf:` para mudanças de performance;
- `test:` para mudanças somente de teste;
- `docs:` para documentação;
- `refactor:` para estrutura de código sem mudança comportamental;
- `build:` para build system ou construção de package;
- `ci:` para workflows;
- `chore:` para manutenção que não se encaixa nas categorias acima.

Issues futuras de feature idealmente devem incluir contexto, objetivo, escopo,
não objetivos, dependências, critérios de aceite e Definition of Done. Bugs
triviais e pequenas correções de docs podem ser mais leves quando um template
completo não adicionaria clareza.

## Segurança De Release

Validação normal não é release de produção.

Não crie tags, publique pacotes NuGet, publique GitHub Packages, crie GitHub
Releases, mova tags de release ou dispare workflows de release de produção como
parte do desenvolvimento comum. Essas ações exigem intenção explícita do
maintainer.

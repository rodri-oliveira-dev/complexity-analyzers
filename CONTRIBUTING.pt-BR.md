# Contribuindo

[English](CONTRIBUTING.md) | Português (Brasil)

Obrigado por contribuir com o `ComplexityAnalysis.Analyzers`. Mantenha as mudanças focadas, revisáveis e alinhadas aos diagnósticos públicos do analyzer, às garantias de compatibilidade e ao modelo conservador de análise.

> Este documento é a versão em português de `CONTRIBUTING.md`. Em caso de divergência, a versão em inglês é a referência canônica.

## Código de Conduta

Ao participar deste projeto, você concorda em seguir o [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Pré-requisitos

Instale:

- .NET SDK 10, ou um SDK compatível selecionado pelo `global.json`;
- Git.

Nenhuma ferramenta .NET instalada globalmente deve ser necessária para o fluxo padrão de desenvolvimento.

## Preparar o repositório

A partir da raiz do repositório:

```bash
dotnet tool restore
dotnet restore ComplexityAnalysis.Analyzers.slnx
```

## Definition of Done

Use a política canônica de qualidade de release para classificar a mudança e decidir quais evidências se aplicam:

- [Release Quality Governance](docs/en/development/quality-gates.md)
- [Governança de Qualidade de Release](docs/pt-BR/development/quality-gates.md)

A validação é proporcional ao risco. Mudanças somente de documentação podem permanecer leves; alterações de comportamento do analyzer, sensíveis a performance, Roslyn/dependências, packaging, CI e release precisam das evidências específicas listadas na política.

## Validar uma mudança

Antes de abrir um pull request, execute os checks exigidos pelo tipo de mudança. Para mudanças amplas no analyzer, build, package ou repositório, o caminho principal comum é:

```bash
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

Para mudanças somente de documentação, registre a validação mais leve que se aplica, como revisão Markdown, revisão de links, `git diff --check` e ausência de impacto no comportamento do analyzer.

Quando uma mudança afetar formatação ou regras de estilo do analyzer, execute também os checks de formatação configurados pelo projeto.

## Mantenha as mudanças focadas

Uma contribuição deve tratar de uma única preocupação coesa. Evite misturar refactors não relacionados, mudanças de formatação, atualizações de dependência, reescritas de documentação e implementação de features, a menos que sejam exigidos pela mesma mudança.

Prefira a menor alteração que resolva o problema preservando correctness do analyzer, compatibilidade do package, performance de build e o comportamento conservador de `Unknown` para casos não suportados.

## Comportamento do analyzer e diagnósticos

Mudanças que afetem a estimativa de complexidade ou os diagnósticos devem preservar os princípios de análise semântica do projeto:

- usar sintaxe, símbolos e informações semânticas do Roslyn em vez de matching baseado apenas em texto;
- preferir `Unknown` a uma estimativa de complexidade insegura ou não comprovada;
- manter traversal de métodos-fonte e resolução de recorrências limitados;
- evitar I/O, acesso à rede, execução de processos ou reflection pesada nos hot paths do analyzer;
- preservar compatibilidade com os compiler/SDK hosts suportados.

Ao adicionar ou alterar um diagnóstico público:

- adicione ou atualize testes para casos triggering e non-triggering;
- atualize `src/ComplexityAnalysis.Analyzers/AnalyzerReleases.Unshipped.md` quando exigido pelo release tracking do analyzer;
- atualize o catálogo de analyzers em `docs/en/analyzers.md` e `docs/pt-BR/analyzers.md`;
- documente qualquer nova opção de `.editorconfig`/analyzer config nos dois guias de configuração.

## Testes

Mudanças de comportamento devem ser cobertas por testes. Correções de bugs devem incluir um teste de regressão quando viável. Regras do analyzer normalmente devem testar casos positivos e negativos para reduzir falsos positivos.

Mudanças em análise interprocedural, resolução de recorrências, caching, budgets ou carregamento de package também devem considerar as suites existentes de compatibilidade e performance descritas pela DoD canônica.

Não enfraqueça validações existentes apenas para fazer uma mudança passar.

## Performance

Roslyn analyzers executam dentro dos compiler e IDE hosts, portanto regressões de performance podem afetar cada build dos consumidores.

Para mudanças em hot paths de análise, caching, recursão, traversal interprocedural ou resolução de known operations, execute os testes de performance relevantes e revise implicações de alocação/complexidade. Evite scans amplos quando uma análise sob demanda for suficiente.

## Compatibilidade e dependências

`Microsoft.CodeAnalysis.CSharp` é intencionalmente fixado à baseline de compatibilidade Roslyn selecionada pelo repositório. Não atualize o package da API do compilador Roslyn como uma atualização rotineira de dependência; essas mudanças exigem avaliação explícita de compatibilidade nos hosts suportados.

Não introduza `Microsoft.CodeAnalysis.Workspaces` ou dependências de runtime, a menos que a feature realmente exija e o impacto arquitetural/de package tenha sido revisado.

## Releases

Releases de produção do analyzer são criadas manualmente pelo workflow `Release`
do GitHub Actions em `.github/workflows/release.yml`; CLI e MCP usam os workflows
independentes documentados abaixo.

Pull requests que alteram o workflow de release ou arquivos relevantes ao package executam somente o caminho `build-and-pack` com uma versão sintética de CI. Esse dry run exercita validação do package e geração da SBOM sem criar tags, publicar packages, gerar attestations ou criar GitHub Release.

Execute o workflow a partir da branch `main` e informe apenas a versão semântica do package, sem o prefixo `v`:

```text
1.0.0
```

O workflow deriva automaticamente a Git tag:

```text
1.0.0 -> v1.0.0
```

Versões de prerelease também são suportadas, por exemplo:

```text
1.1.0-beta.1 -> v1.1.0-beta.1
```

O pipeline de release:

1. valida a versão semântica e exige execução a partir da `main`;
2. restaura, compila, testa, empacota e valida o `ComplexityAnalysis.Analyzers`;
3. gera e valida uma SBOM SPDX 2.3 a partir do `.nupkg` final exato;
4. cria a Git tag `v<version>` correspondente, ou a verifica ao repetir com segurança a mesma release;
5. publica o mesmo `.nupkg` no NuGet.org usando Trusted Publishing e GitHub OIDC;
6. publica o mesmo `.nupkg` no GitHub Packages usando o `GITHUB_TOKEN` do workflow;
7. cria attestations de build provenance e SBOM do GitHub para o package de release;
8. cria ou atualiza a GitHub Release e anexa o package e sua SBOM SPDX.

A política de Trusted Publishing do NuGet.org deve corresponder exatamente à identidade do workflow:

```text
Repository owner: rodri-oliveira-dev
Repository: complexity-analyzers
Workflow file: release.yml
Environment: release
Package: ComplexityAnalysis.Analyzers
```

O job de publicação no NuGet utiliza o environment do GitHub chamado `release` e `id-token: write`; nenhuma NuGet API key de longa duração deve ser armazenada nos secrets do repositório.

A provenance da release e a SBOM podem ser verificadas localmente com GitHub CLI. Veja [Baseline de Segurança do Repositório](docs/pt-BR/development/security-baseline.md#provenance-de-release-e-sbom).

Não mova, reutilize ou recrie manualmente uma release tag existente para outro commit. Release tags devem ser imutáveis.

### Releases Do ComplexityAnalysis.Tool

A CLI possui uma linha de release independente.
`.github/workflows/release-tool.yml` publica somente `ComplexityAnalysis.Tool`,
recebe a versão de produção como input manual e deriva tags `tool-v<version>`.
Ele não altera nem publica o pacote do analyzer. Pull requests executam apenas
build, instalação do pacote, smoke tests e SBOM; jobs de publicação executam
somente por dispatch manual a partir da `main`.

Antes da primeira release `1.0.0`, conclua estas configurações externas:

1. No Trusted Publishing do NuGet.org, crie uma policy GitHub pertencente a
   `rodri-oliveira-dev` para o repositório `complexity-analyzers`, workflow
   `release-tool.yml` e environment `release`. Restrinja o escopo a publicar
   pacotes novos e versões do padrão exato `ComplexityAnalysis.Tool`.
2. No GitHub, garanta que o environment `release` aceite deployments somente da
   `main` e configure os required reviewers desejados. Não adicione API key do
   NuGet; o job troca OIDC do GitHub por uma credencial temporária.
3. Estenda o ruleset de tags para proteger `tool-v*`. Impeça exclusão, force
   update e recriação, permitindo que o job com `contents: write` crie uma tag
   nova somente depois da validação.
4. Confirme que GitHub Packages está habilitado e que Actions pode escrever
   packages com o `GITHUB_TOKEN` do workflow.
5. Verifique novamente se `ComplexityAnalysis.Tool` e a versão pretendida ainda
   estão disponíveis no NuGet.org. A disponibilidade antes da primeira
   publicação não reserva o ID.

Depois da aprovação e do merge, selecione **Release ComplexityAnalysis.Tool**
no GitHub Actions, escolha `main`, informe `1.0.0` e revise o deployment no
environment `release`. Não crie a tag antecipadamente. Uma repetição segura
aceita `tool-v1.0.0` existente somente quando ela aponta para o mesmo commit
validado; os pushes usam skip-duplicate e assets da GitHub Release nunca são
substituídos.

### Releases do ComplexityAnalysis.Mcp

O servidor MCP tem uma terceira linha independente.
`.github/workflows/release-mcp.yml` publica somente `ComplexityAnalysis.Mcp`,
aceita versão semântica manual e deriva tags `mcp-v<versão>`. Pull requests
executam build, testes, instalação do pacote, contrato com cliente MCP real e
SBOM sem publicar. Jobs de produção exigem dispatch manual a partir de `main`.
As GitHub Releases MCP não são marcadas como a release genérica mais recente,
preservando a identificação da release do analyzer.

Antes da primeira release `1.0.0`, conclua:

1. No NuGet.org Trusted Publishing, crie uma policy GitHub com owner
   `rodri-oliveira-dev`, repositório `complexity-analyzers`, workflow
   `release-mcp.yml` e environment `release`. Autorize exatamente o PackageId
   `ComplexityAnalysis.Mcp` com escopo **Push new packages and package
   versions**.
2. Garanta que o environment GitHub `release` permita deployments somente da
   `main` e use os reviewers desejados. Não adicione API key NuGet duradoura; o
   workflow troca OIDC por credencial temporária.
3. Proteja `mcp-v*` no ruleset de tags contra exclusão, force update e
   recriação, permitindo criação pelo workflow depois da validação.
4. Confirme GitHub Packages e permissão de escrita por `GITHUB_TOKEN`.
5. Verifique a disponibilidade de `ComplexityAnalysis.Mcp` e `1.0.0` no
   NuGet.org imediatamente antes da primeira publicação.

Depois de aprovação e merge, selecione **Release ComplexityAnalysis.Mcp**,
escolha `main`, informe `1.0.0` e revise o deployment protegido. Não crie a tag
antes. O workflow publica o artefato exato já validado e instalado; ele não gera
notas comparando namespaces de tags do analyzer, CLI e MCP.

## Pull requests

Pull requests devem:

- explicar o que mudou e por quê;
- vincular a issue relevante quando existir;
- identificar o tipo de mudança conforme a DoD canônica;
- descrever como a mudança foi validada;
- destacar impactos em diagnósticos, compatibilidade, package, performance ou segurança;
- atualizar a documentação em inglês e português em conjunto quando houver mudança de comportamento público;
- manter arquivos gerados ou não relacionados fora do diff.

Reviewers podem solicitar mudanças menores, testes adicionais, documentação mais clara ou evidências de compatibilidade antes da aprovação.

## Expectativas de review

Trate o feedback de review com commits adicionais enquanto o pull request estiver aberto. Resolva discussões somente depois que a preocupação tiver sido tratada ou houver concordância sobre a resolução.

Um pull request está pronto para merge quando o comportamento pretendido estiver claro, os checks obrigatórios passarem, a documentação corresponder à implementação e nenhum problema conhecido de carregamento do analyzer, compatibilidade, segurança ou performance permanecer sem explicação.

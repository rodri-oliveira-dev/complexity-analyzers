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

Releases de produção são criadas manualmente pelo workflow `Release` do GitHub Actions em `.github/workflows/release.yml`.

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
3. cria a Git tag `v<version>` correspondente, ou a verifica ao repetir com segurança a mesma release;
4. publica o `.nupkg` no NuGet.org usando Trusted Publishing e GitHub OIDC;
5. publica o mesmo `.nupkg` no GitHub Packages usando o `GITHUB_TOKEN` do workflow;
6. cria uma GitHub Release para a tag gerada e anexa o package como artifact.

A política de Trusted Publishing do NuGet.org deve corresponder exatamente à identidade do workflow:

```text
Repository owner: rodri-oliveira-dev
Repository: complexity-analyzers
Workflow file: release.yml
Environment: release
Package: ComplexityAnalysis.Analyzers
```

O job de publicação no NuGet utiliza o environment do GitHub chamado `release` e `id-token: write`; nenhuma NuGet API key de longa duração deve ser armazenada nos secrets do repositório.

Não mova, reutilize ou recrie manualmente uma release tag existente para outro commit. Release tags devem ser imutáveis.

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

# Baseline de Segurança do Repositório

O repositório usa controles em camadas para que segurança do código-fonte, risco de dependências, qualidade de código e compatibilidade do pacote sejam avaliados de forma independente.

## CodeQL

O workflow versionado `.github/workflows/codeql.yml` passa a ser a configuração autoritativa pretendida do CodeQL. Ele analisa pull requests direcionados à `main`, pushes para `main`, agendamento semanal e execuções manuais, usando o SDK selecionado por `global.json` e o contrato explícito de build Release de `ComplexityAnalysis.Analyzers.slnx`.

Os resultados SARIF são publicados em **Security > Code scanning**, onde o ruleset existente da main aplica seus limites de severidade de segurança e de alertas. O artifact SARIF é mantido por sete dias para diagnóstico e evidência de auditoria. O workflow solicita apenas `contents: read` e `security-events: write`. Pull requests externos utilizam o evento `pull_request` (não `pull_request_target`); o GitHub restringe tokens de forks e permite uploads de Code Scanning em execuções de pull request. Não são necessários secrets do repositório nem permissões elevadas para tokens de forks.

**Migração do Default Setup:** um administrador do repositório deve desativar o CodeQL Default Setup antes de validar esta configuração, pois o GitHub rejeita uploads de configurações avançadas enquanto o Default Setup está ativo. Manter o ruleset habilitado durante toda a migração. Validar a publicação no Code Scanning no PR de migração antes do merge, atualizar as branches dos PRs abertos e confirmar a aprovação do gate. Se a publicação falhar, diagnosticar a configuração antes do merge, sem contornar o ruleset.

Suites mais amplas podem ser avaliadas depois que os findings iniciais forem compreendidos; novos ruídos não devem ser escondidos apenas para manter a automação verde.

## Dependency Review

O Dependency Review é um gate bloqueante de pull request. Ele rejeita novas dependências com vulnerabilidades de severidade `high` ou superior e, intencionalmente, não usa `continue-on-error`.

Esse controle avalia o delta de dependências do pull request. Ele complementa, e não substitui, NuGet Audit, Dependabot, SonarQube Cloud, testes do analyzer/pacote e CodeQL.

## Dependabot

O Dependabot mantém:

- dependências npm do portal de documentação em `website/`, agrupando atualizações minor e patch compatíveis e mantendo atualizações major separadas para revisão de compatibilidade;
- dependências NuGet, incluindo as restrições explícitas de compatibilidade Roslyn do repositório;
- o .NET SDK declarado em `global.json`;
- referências de GitHub Actions.

Pull requests de atualização automática devem passar pelos mesmos gates aplicáveis de qualidade, compatibilidade, pacote, performance e segurança das mudanças manuais.

## Política de supply chain dos workflows

Actions alteradas em trabalhos sensíveis de segurança devem usar SHA completo com comentário legível da versão. Checkouts somente leitura devem desabilitar a persistência de credenciais.

O hardening de workflows não deve renomear jobs obrigatórios sem necessidade. O ruleset ativo depende dos nomes estáveis de checks documentados em `quality-gates.md`.


## Provenance de release e SBOM

O workflow `Release` do analyzer e os workflows independentes `Release
ComplexityAnalysis.Tool` e `Release ComplexityAnalysis.Mcp` geram uma SBOM SPDX
2.3 a partir do `.nupkg` exato produzido por seus jobs de build. Cada par
package/SBOM é enviado como um artifact de workflow, e o mesmo `.nupkg` baixado
é reutilizado para NuGet.org, GitHub Packages, attestations e GitHub Release.

O job `github-release` usa GitHub OIDC com permissões mínimas de attestation para criar:

- uma attestation de build provenance para o `.nupkg` publicado e sua SBOM SPDX independente;
- uma SBOM attestation SPDX cujo subject é o `.nupkg` publicado.

O arquivo independente `ComplexityAnalysis.Analyzers.<version>.sbom.spdx.json` também é anexado à GitHub Release.

Para verificação byte a byte, use o `.nupkg` anexado à GitHub Release. Um registry pode adicionar metadados de repository signing após a publicação, alterando legitimamente os bytes do package baixado.

Verifique a build provenance do package da GitHub Release com GitHub CLI:

```bash
gh attestation verify ComplexityAnalysis.Analyzers.<version>.nupkg \
  --repo rodri-oliveira-dev/complexity-analyzers \
  --signer-workflow rodri-oliveira-dev/complexity-analyzers/.github/workflows/release.yml
```

Verifique explicitamente a SBOM attestation SPDX 2.3 com:

```bash
gh attestation verify ComplexityAnalysis.Analyzers.<version>.nupkg \
  --repo rodri-oliveira-dev/complexity-analyzers \
  --signer-workflow rodri-oliveira-dev/complexity-analyzers/.github/workflows/release.yml \
  --predicate-type https://spdx.dev/Document/v2.3
```

As attestations comprovam a procedência dos artifacts e vinculam a SBOM ao digest do package. Elas complementam, e não substituem, testes de package, CodeQL, Dependency Review, NuGet auditing, Trusted Publishing e review humano.

Para a CLI, substitua por `ComplexityAnalysis.Tool.<version>.nupkg` e pelo
signer workflow
`rodri-oliveira-dev/complexity-analyzers/.github/workflows/release-tool.yml` nos
comandos de verificação. As tags da CLI usam o namespace independente
`tool-v<version>`.

Para o MCP, substitua por `ComplexityAnalysis.Mcp.<version>.nupkg` e pelo signer
workflow
`rodri-oliveira-dev/complexity-analyzers/.github/workflows/release-mcp.yml`.
As tags MCP usam `mcp-v<version>`, e suas GitHub Releases não são marcadas como a
release genérica mais recente do repositório.

# Baseline de Segurança do Repositório

O repositório usa controles em camadas para que segurança do código-fonte, risco de dependências, qualidade de código e compatibilidade do pacote sejam avaliados de forma independente.

## CodeQL

O GitHub CodeQL Default Setup é a fonte autoritativa de alerts em **Security > Code scanning** para este repositório.

`.github/workflows/codeql.yml` complementa o Default Setup validando a análise semântica de C# contra o contrato explícito de build do repositório em pull requests direcionados à `main`, pushes para `main`, agendamento semanal e execuções manuais. O workflow usa build manual, o SDK selecionado por `global.json` e o mesmo contrato de build Release de `ComplexityAnalysis.Analyzers.slnx` usado pelo CI.

Como o GitHub não processa análises CodeQL de configurações avançadas enquanto o Default Setup está habilitado, o workflow versionado intencionalmente não envia seu SARIF para o Code Scanning. Ele executa a suite padrão de alta precisão, armazena o SARIF gerado como artifact temporário do workflow para diagnóstico/evidência de auditoria e mantém permissões somente leitura com apenas `contents: read`.

Suites mais amplas podem ser avaliadas depois que os findings iniciais forem compreendidos; novos ruídos não devem ser escondidos apenas para manter a automação verde.

## Dependency Review

O Dependency Review é um gate bloqueante de pull request. Ele rejeita novas dependências com vulnerabilidades de severidade `high` ou superior e, intencionalmente, não usa `continue-on-error`.

Esse controle avalia o delta de dependências do pull request. Ele complementa, e não substitui, NuGet Audit, Dependabot, SonarQube Cloud, testes do analyzer/pacote e CodeQL.

## Dependabot

O Dependabot mantém:

- dependências NuGet, incluindo as restrições explícitas de compatibilidade Roslyn do repositório;
- o .NET SDK declarado em `global.json`;
- referências de GitHub Actions.

Pull requests de atualização automática devem passar pelos mesmos gates aplicáveis de qualidade, compatibilidade, pacote, performance e segurança das mudanças manuais.

## Política de supply chain dos workflows

Actions alteradas em trabalhos sensíveis de segurança devem usar SHA completo com comentário legível da versão. Checkouts somente leitura devem desabilitar a persistência de credenciais.

O hardening de workflows não deve renomear jobs obrigatórios sem necessidade. O ruleset ativo depende dos nomes estáveis de checks documentados em `quality-gates.md`.

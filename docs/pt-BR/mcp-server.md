# Servidor MCP Local

[English](../en/mcp-server.md) | Português (Brasil)

`ComplexityAnalysis.Mcp` é a superfície stdio local, com versão independente,
para a análise de projetos do repositório. Ele fornece ferramentas a agentes
externos compatíveis; não é um agente autônomo. Compartilha relatórios,
evidências, StableId e comparação com `ComplexityAnalysis.Tool` e não adiciona
dependências MCP aos consumidores do analyzer.

Consulte o [README do pacote](../../src/ComplexityAnalysis.Mcp/README.pt-BR.md)
para instalação global/local, atualização, desinstalação, configurações de
clientes, exemplos, orientação de PATH por sistema e limitações conhecidas.

## Distribuição e inicialização

O pacote independente é `ComplexityAnalysis.Mcp`; a versão `1.0.0` instala o
comando .NET 10 `complexity-mcp`:

```bash
dotnet tool install --global ComplexityAnalysis.Mcp --version 1.0.0
complexity-mcp --root /caminho/absoluto/para/projeto
```

Para instalação por manifest local:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Mcp --version 1.0.0
dotnet tool run complexity-mcp --root /caminho/absoluto/para/projeto
```

Comandos informativos encerram antes do protocolo:

```bash
complexity-mcp --help
complexity-mcp --version
```

O servidor aceita `--root` (diretório atual), `--timeout-seconds` (`30`, faixa
`1..300`), `--max-input-bytes` (50 MiB, faixa `1..1073741824`) e `--max-files`
(`2000`, faixa `1..100000`). Configuração inválida retorna código `2` e mensagem
sanitizada no stderr.

## Contrato de protocolo

As três ferramentas anunciam anotação MCP de somente leitura, conteúdo
estruturado, schemas de entrada descritos e schemas de saída.

| Ferramenta | Entradas | Resultado |
| --- | --- | --- |
| `analyze_project` | Path de projeto/solution; `responseMode` e `maxMembers` opcionais | Snapshot determinístico completo ou resumo limitado explícito, mais `analysisId` |
| `query_member_evidence` | Path, `StableId` exato e `analysisId` opcional | Membro/evidência correspondente ou `found: false` |
| `compare_snapshots` | JSON baseline/current | Relatório determinístico de comparação |

O padrão de `analyze_project` permanece compatível: `responseMode` é `full` e
`report` contém o `complexity-snapshot/1.0` completo. O modo summary retorna até
100 entradas por padrão (configurável de 1 a 500) e informa `isTruncated`, total
e quantidade retornada. Ele não omite evidência silenciosamente alegando um
relatório completo.

Cada análise produz um `analysisId` pelo hash do snapshot serializado exato. O
processo retém até quatro snapshots e 16 MiB. Passar o ID a
`query_member_evidence` evita repetir a análise e identifica a origem como
`cachedSnapshot`. Isso reutiliza um snapshot, não um cache dos arquivos atuais:
o servidor nunca trata o ID como prova de que o filesystem não mudou. Omitir o
ID preserva a análise fresca original. IDs ausentes ou removidos falham com
orientação para executar `analyze_project` novamente.

`Unknown` continua sendo resultado conservador explícito. Resumos usam
`complexityStatus: "unknown"` e `complexity: null`; evidências preservam um
`UnknownReason` tipado quando disponível. Comparação classifica evidência
Unknown como incomparável, nunca como regressão comprovada.

## Limite de segurança

O host é local, somente stdio e somente leitura. Não oferece ferramenta de
escrita, shell, rede, telemetria, transporte remoto ou execução arbitrária.

- Toda entrada deve estar abaixo de `--root` e ter extensão suportada.
- Referências de projeto/solution e fontes descobertos são validados antes da
  leitura.
- Traversal por links simbólicos e reparse points é rejeitado ou ignorado na
  enumeração.
- Contagem de arquivos e bytes é isolada por requisição.
- Cancelamento e timeout propagam por loading, Roslyn, comparação e serialização.
- Paths do relatório são relativos a `--root`; erros não repetem paths absolutos.
- stdout fica reservado ao JSON-RPC após o startup; logs vão para stderr.

O loader interpreta os arquivos suportados diretamente; não executa MSBuild,
restore, build nem tasks arbitrárias. Os controles reduzem exposição acidental,
mas não tornam confiável um repositório local não confiável.

## Configurações de clientes

VS Code / GitHub Copilot usa `.vscode/mcp.json` com `servers`:

```json
{
  "servers": {
    "complexity-analysis": {
      "type": "stdio",
      "command": "complexity-mcp",
      "args": ["--root", "${workspaceFolder}"]
    }
  }
}
```

Codex CLI:

```bash
codex mcp add complexity-analysis -- complexity-mcp --root /caminho/absoluto/para/projeto
codex mcp list
```

Ou em `~/.codex/config.toml` / `.codex/config.toml` de projeto confiável:

```toml
[mcp_servers.complexity-analysis]
command = "complexity-mcp"
args = ["--root", "/caminho/absoluto/para/projeto"]
```

Claude Desktop usa `claude_desktop_config.json` com `mcpServers`:

```json
{
  "mcpServers": {
    "complexity-analysis": {
      "command": "complexity-mcp",
      "args": ["--root", "/caminho/absoluto/para/projeto"]
    }
  }
}
```

Os formatos seguem a
[referência oficial do VS Code](https://code.visualstudio.com/docs/agents/reference/mcp-configuration),
a [documentação MCP oficial do Codex](https://developers.openai.com/codex/extend/mcp)
e o [guia de hosts do SDK MCP](https://py.sdk.modelcontextprotocol.io/get-started/real-host/).

## Distribuição e publicação no Registry

O NuGet.org é a origem de distribuição executável da ferramenta .NET
`ComplexityAnalysis.Mcp`. O GitHub Packages recebe o mesmo pacote validado como
feed alternativo. O Official MCP Registry contém somente metadados de descoberta
sob `io.github.rodri-oliveira-dev/complexity-analysis` e resolve o pacote
`nuget` pelo NuGet.org; ele não substitui instalação nem inicialização stdio no
VS Code/Copilot, Codex ou Claude Desktop.

`.mcp/server.json` é a base versionada. Em cada release manual da `main`,
`release-mcp.yml` substitui os dois campos de versão do manifesto pela versão
validada no workflow, comprovando igualdade com `mcp-v<versão>` e o pacote
NuGet. Depois da publicação NuGet e conclusão da GitHub Release, o job do
Registry aguarda o README e o marcador exato de propriedade `mcp-name`, verifica
o artefato fixado do `mcp-publisher` por Sigstore, valida o manifesto, autentica
por GitHub OIDC, publica e verifica o resultado na API. Pull requests executam
somente validação.

Antes da primeira release, configure o Trusted Publishing do NuGet conforme o
guia de contribuição, proteja o environment GitHub `release` e garanta que o
repositório possa autenticar seu namespace `io.github` no Registry por OIDC.
Não é necessário PAT nem secret persistente do Registry. Procure pelo nome exato
no [Official Registry](https://registry.modelcontextprotocol.io/) para verificar
a publicação.

Em caso de falha, valide localmente com
`mcp-publisher validate .mcp/server.json`. Pacote indisponível normalmente
significa que a indexação do README no NuGet ainda não terminou; repita o
workflow depois. Falhas OIDC exigem conferir `main`, aprovação do environment,
identidade do repositório e `id-token: write`. Nunca substitua versão imutável
conflitante no NuGet ou Registry: corrija a origem e publique nova versão.
Consulte a [especificação de pacotes](https://github.com/modelcontextprotocol/registry/blob/main/docs/modelcontextprotocol-io/package-types.mdx),
o [guia de Actions](https://github.com/modelcontextprotocol/registry/blob/main/docs/modelcontextprotocol-io/github-actions.mdx) e o
[guia .NET](https://learn.microsoft.com/pt-br/dotnet/ai/quickstarts/publish-mcp-registry).

## Reproduzir e validar

As operações equivalentes da CLI são:

```bash
dotnet run --project ./src/ComplexityAnalysis.Tool -- analyze <projeto-ou-solution> --format json --snapshot
dotnet run --project ./src/ComplexityAnalysis.Tool -- compare <baseline.json> <current.json> --format json
```

O workflow de release empacota um único artefato MCP, instala em tool path
isolado e executa o teste de contrato com `ModelContextProtocol.Client` real
contra o comando instalado. O teste faz inicialização, discovery e chamadas às
três ferramentas. Também valida metadata, help/version, SBOM SPDX 2.3 e
isolamento do pacote antes de qualquer job manual de publicação.

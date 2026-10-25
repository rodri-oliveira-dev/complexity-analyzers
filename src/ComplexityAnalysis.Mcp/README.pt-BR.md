# ComplexityAnalysis.Mcp

[English](README.md) | Português (Brasil)

`ComplexityAnalysis.Mcp` é um servidor MCP local e somente leitura que permite a
agentes compatíveis analisar complexidade de projetos C#, consultar evidências
de um membro e comparar snapshots determinísticos. Ele oferece ferramentas a um
agente externo; não é um agente autônomo e nunca edita código-fonte.

## Escolha o produto correto

- `ComplexityAnalysis.Analyzers` executa em builds Roslyn e análise da IDE.
- `ComplexityAnalysis.Tool` é a CLI `complexity-analysis` para relatórios,
  snapshots, quality gates e automação.
- `ComplexityAnalysis.Mcp` é o servidor stdio `complexity-mcp` para discovery e
  invocação por agentes. Ele reutiliza a implementação de análise da CLI.

O pacote MCP é instalado de forma independente e inclui tudo que precisa para
executar; não exige checkout do repositório nem instalação prévia da CLI.

## Requisitos e instalação

O runtime .NET 10 é obrigatório. O SDK .NET 10 é recomendado para manifests
locais.

Depois da publicação de `1.0.0`, instale globalmente:

```bash
dotnet tool install --global ComplexityAnalysis.Mcp --version 1.0.0
complexity-mcp --version
```

Ou fixe a versão em um manifest local:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Mcp --version 1.0.0
dotnet tool restore
dotnet tool run complexity-mcp --version
```

Atualize ou desinstale uma instalação global com:

```bash
dotnet tool update --global ComplexityAnalysis.Mcp --version <versão>
dotnet tool uninstall --global ComplexityAnalysis.Mcp
```

Para manifest local, omita `--global` e execute com `dotnet tool run
complexity-mcp`. Um pacote compilado do fonte não é uma release publicada: use
fonte explícita e versão somente local para testá-lo.

## Iniciar o servidor

Clientes MCP normalmente iniciam o processo. O comando é:

```bash
complexity-mcp --root /caminho/absoluto/para/projeto
```

`--root` é o limite de leitura, não apenas uma indicação de diretório de
trabalho. Use caminho absoluto para a configuração funcionar em qualquer `cwd`.

| Opção de inicialização | Padrão | Valores permitidos |
| --- | --- | --- |
| `--root <diretório>` | Diretório atual | Diretório existente |
| `--timeout-seconds <n>` | `30` | `1` a `300` |
| `--max-input-bytes <n>` | `52428800` (50 MiB) | `1` a `1073741824` |
| `--max-files <n>` | `2000` | `1` a `100000` |

Use `complexity-mcp --help` para as opções da versão instalada. `--help` e
`--version` imprimem e encerram antes de iniciar o protocolo stdio.

## Ferramentas MCP

Todas são marcadas como somente leitura e retornam conteúdo estruturado.

### `analyze_project`

Analisa `.csproj`, `.sln` ou `.slnx` abaixo de `--root`.

- `path` é obrigatório.
- `responseMode` é `full` por padrão e preserva o contrato v1 completo em
  `report`.
- `responseMode: "summary"` retorna um índice compacto em `summary`.
- `maxMembers` vale apenas no modo summary, usa `100` por padrão e aceita
  `1..500`.

Truncamento nunca é silencioso: `isTruncated`, `totalMemberCount` e
`returnedMemberCount` informam o que foi omitido. A resposta traz um
`analysisId` determinístico. Um cache limitado retém até quatro snapshots
exatos e no máximo 16 MiB; `reusable` informa se o snapshot foi retido.

### `query_member_evidence`

Consulta um `StableId` exato. Sem `analysisId`, reanalisa os arquivos atuais
para preservar compatibilidade. Com o `analysisId` de `analyze_project`, lê o
snapshot imutável em cache e evita repetir a análise Roslyn. Se o snapshot não
foi retido ou foi removido, falha explicitamente; nunca substitui por resultado
atual potencialmente diferente.

### `compare_snapshots`

Compara snapshots `.json` baseline e current abaixo de `--root`. Resultados são
determinísticos. Uma estimativa `Unknown` é incomparável e não se torna uma
regressão comprovada.

## Fluxo de exemplo para agentes

Pergunte ao agente:

- "Resuma os hotspots de complexidade de `src/App/App.csproj`."
- "Por que `M:App.Search(System.Int32[])` foi classificado como quadrático?"
- "Compare `baseline.json` e `current.json`; liste só regressões comprovadas."
- "Quais resultados são `Unknown` e qual evidência explica cada um?"

Para projetos grandes:

1. chame `analyze_project` com `responseMode: "summary"`;
2. confira `isTruncated` e o índice retornado;
3. chame `query_member_evidence` com o `analysisId` e um `StableId`.

`Unknown` significa que o analyzer limitado não comprovou uma estimativa segura.
Não significa `O(1)`, erro ou evidência de eficiência.

Uma resposta resumida abreviada tem esta forma:

```json
{
  "schemaVersion": "complexity-mcp-analysis/1.0",
  "analysisId": "sha256:<digest>",
  "responseMode": "summary",
  "reusable": true,
  "isTruncated": false,
  "totalMemberCount": 1,
  "returnedMemberCount": 1,
  "report": null,
  "summary": {
    "unknownMemberCount": 1,
    "members": [
      {
        "stableId": "M:Sample.Work",
        "complexityStatus": "unknown",
        "complexity": null
      }
    ]
  }
}
```

## Configuração dos clientes

Os valores comuns de execução são:

```json
{
  "command": "complexity-mcp",
  "args": ["--root", "/caminho/absoluto/para/projeto"]
}
```

### VS Code / GitHub Copilot

Crie `.vscode/mcp.json`:

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

Execute **MCP: List Servers**. Consulte a
[referência oficial do VS Code](https://code.visualstudio.com/docs/agents/reference/mcp-configuration).

### Codex CLI e extensão de IDE

```bash
codex mcp add complexity-analysis -- complexity-mcp --root /caminho/absoluto/para/projeto
codex mcp list
```

O equivalente em `~/.codex/config.toml`:

```toml
[mcp_servers.complexity-analysis]
command = "complexity-mcp"
args = ["--root", "/caminho/absoluto/para/projeto"]
```

CLI e extensão compartilham a configuração. Consulte a
[documentação oficial do Codex](https://developers.openai.com/codex/extend/mcp).

### Claude Desktop

Para servidor local definido pelo desenvolvedor, adicione a
`claude_desktop_config.json` e reinicie completamente o Claude Desktop:

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

No Windows, o arquivo fica em `%APPDATA%\Claude`; no macOS, em
`~/Library/Application Support/Claude`. Consulte o
[guia oficial do SDK MCP](https://py.sdk.modelcontextprotocol.io/get-started/real-host/).

## Notas para Windows, macOS e Linux

- Garanta que o diretório de ferramentas globais .NET esteja no `PATH` herdado
  pelo cliente. Se necessário, use o executável absoluto:
  `%USERPROFILE%\.dotnet\tools\complexity-mcp.exe` no Windows ou
  `$HOME/.dotnet/tools/complexity-mcp` no macOS/Linux.
- Em JSON, caminhos Windows exigem barras invertidas escapadas; barras normais
  também funcionam com .NET.
- Clientes gráficos podem herdar `PATH` diferente do terminal. Reinicie o
  cliente após instalar ou configure o executável absoluto.
- `--root` deve ser absoluto; o `cwd` não amplia o limite de leitura.

## Segurança e limitações

O servidor é local, somente stdio e somente leitura. Não possui transporte HTTP,
rede, telemetria, shell, mutação de fonte nem ferramentas de escrita. Rejeita
paths fora de `--root`, extensões não suportadas, traversal por links
simbólicos/reparse points e requisições acima dos limites de arquivos, bytes ou
timeout. Requisições concorrentes têm escopos independentes. Paths nos
relatórios são relativos a `--root`; erros não repetem paths absolutos. stdout é
reservado ao JSON-RPC MCP e logs vão para stderr.

A análise é conservadora e limitada. Ela não compila o projeto, avalia MSBuild,
executa código nem resolve todo programa C# válido. A configuração lida limita-se
aos projetos e fontes suportados pelo loader compartilhado com a CLI.

## Links do projeto

- [Guia técnico MCP](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/docs/pt-BR/mcp-server.md)
- [Repositório](https://github.com/rodri-oliveira-dev/complexity-analyzers)
- [Como contribuir](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CONTRIBUTING.pt-BR.md)
- [Licença MIT](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/LICENSE)

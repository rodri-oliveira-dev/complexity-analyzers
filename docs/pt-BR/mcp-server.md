# Servidor MCP Local

`ComplexityAnalysis.Mcp` é um host MCP separado e somente local que expõe a
análise de complexidade de projetos por entrada/saída padrão. Ele não altera o
pacote do Roslyn analyzer nem adiciona dependências MCP aos consumidores do
analyzer.

## Compilação e configuração

Compile o repositório antes de iniciar o servidor:

```bash
dotnet build ./ComplexityAnalysis.Analyzers.slnx --configuration Release
```

Use o assembly resultante como subprocesso do cliente MCP. Clientes MCP podem
iniciá-lo a partir de outro diretório de trabalho; portanto, configure o
assembly e a raiz permitida com caminhos absolutos. Substitua os placeholders
abaixo pelo caminho absoluto do repositório na sua máquina:

```json
{
  "command": "dotnet",
  "args": [
    "/caminho/absoluto/complexity-analyzers/src/ComplexityAnalysis.Mcp/bin/Release/net10.0/ComplexityAnalysis.Mcp.dll",
    "--root",
    "/caminho/absoluto/complexity-analyzers"
  ]
}
```

No Windows, use os caminhos absolutos equivalentes com barras invertidas
escapadas ou barras normais no JSON.

O host aceita estes limites na inicialização:

- `--root <diretório>`: somente arquivos abaixo desse diretório podem ser
  lidos; o padrão é o diretório atual.
- `--timeout-seconds <1..300>`: timeout por requisição; o padrão é `30`.
- `--max-input-bytes <inteiro positivo>`: limite agregado de tamanho dos
  arquivos por requisição; o padrão é 50 MiB.
- `--max-files <inteiro positivo>`: limite de arquivos por requisição; o
  padrão é `2000`.

## Ferramentas somente leitura

| Ferramenta | Entrada | Resultado |
| --- | --- | --- |
| `analyze_project` | Caminho `.csproj`, `.sln` ou `.slnx` | Resposta tipada cujo `report` é um `complexity-snapshot/1.0` determinístico com evidências |
| `query_member_evidence` | Caminho do projeto e `StableId` exato | Membro e evidências correspondentes, ou `Found: false` |
| `compare_snapshots` | Caminhos dos snapshots `.json` baseline e atual | Resposta tipada cujo `comparison` é um relatório `complexity-comparison/1.0` determinístico |

Todas as ferramentas anunciam a anotação MCP de somente leitura. O servidor
não oferece ferramenta que escreva arquivos, execute comandos arbitrários,
acesse a rede ou altere o código-fonte. As requisições rejeitam caminhos fora
da raiz permitida, extensões não suportadas, traversal por links simbólicos,
arquivos ausentes e violações dos limites configurados. Mensagens de erro não
repetem caminhos absolutos.

O cancelamento do cliente MCP e o timeout por requisição são propagados para a
análise do projeto e a leitura de snapshots. Complexidade inconclusiva continua
como `Unknown`; a camada MCP não reinterpreta nem eleva o resultado da CLI.

## Reproduzir resultados com a CLI

As operações MCP usam as mesmas implementações de relatório, evidência,
identidade e comparação da .NET tool. Reproduza-as com:

```bash
dotnet run --project ./src/ComplexityAnalysis.Tool -- analyze <projeto-ou-solution> --format json --snapshot
dotnet run --project ./src/ComplexityAnalysis.Tool -- compare <baseline.json> <current.json> --format json
```

Use o `StableId` do primeiro relatório JSON para reproduzir uma consulta de
evidência de membro. O membro retornado pelo MCP é o objeto correspondente
desse snapshot.

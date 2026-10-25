# ComplexityAnalysis.Mcp 1.0.0

A primeira versão estável do `ComplexityAnalysis.Mcp` disponibiliza a análise
determinística de complexidade C# do repositório para agentes locais compatíveis
como uma ferramenta .NET 10 instalada de forma independente.

## Destaques

- Instale `ComplexityAnalysis.Mcp` e execute o comando `complexity-mcp` sem um
  checkout do repositório nem instalação separada da CLI.
- Analise entradas `.csproj`, `.sln` e `.slnx`, consulte evidências exatas de
  membros por StableId e compare snapshots determinísticos de complexidade por
  meio de três ferramentas MCP somente leitura.
- Escolha a resposta completa compatível com versões anteriores ou uma resposta
  resumida e explicitamente limitada para reduzir o consumo de contexto.
- Reutilize um snapshot exato analisado por meio de `analysisId`, evitando nova
  análise sem tratar dados em cache como estado atual do sistema de arquivos.
- Preserve resultados conservadores `Unknown` e evidências tipadas; comparações
  nunca convertem estimativas inconclusivas em regressões comprovadas.
- Aplique uma raiz permitida, verificações de extensão, proteção contra links
  simbólicos/reparse points, limites de arquivos e bytes por requisição,
  timeout, cancelamento e separação rigorosa entre stdout e stderr.
- Configure o comando instalado no VS Code / GitHub Copilot, Codex e Claude
  Desktop usando os formatos stdio locais documentados.
- Descubra a versão como
  `io.github.rodri-oliveira-dev/complexity-analysis` no Official MCP Registry,
  usando o mesmo pacote instalável disponível no NuGet.org.

## Instalação

```bash
dotnet tool install --global ComplexityAnalysis.Mcp --version 1.0.0
complexity-mcp --root /caminho/absoluto/para/projeto
```

Esta versão requer o runtime .NET 10. O servidor é local, somente stdio e
somente leitura. Ele não oferece transporte HTTP, acesso à rede, telemetria,
execução de shell nem alteração de código-fonte.

Os artefatos da release incluem o pacote NuGet e sua SBOM SPDX 2.3. Attestations
de proveniência de build e SBOM do GitHub são geradas pelo workflow de release
protegido. Depois que esses artefatos estiverem disponíveis, o workflow publica
o manifesto do Registry com versão sincronizada usando GitHub OIDC e verifica a
entrada resultante.

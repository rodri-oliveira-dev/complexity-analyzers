# AGENTS.md

## Objetivo

Desenvolver um Roslyn Analyzer independente para estimativa e diagnostico de complexidade algoritmica em codigo C#, tomando o projeto original `complexity-hints` apenas como referencia conceitual quando necessario, sem criar dependencia binaria, de projeto ou de pacote local com ele.

O trabalho neste repositorio deve ser pequeno, verificavel, deterministico e adequado a um analyzer distribuivel futuramente como pacote NuGet. Responda em portugues, salvo pedido explicito em outro idioma.

## Escopo do repositorio

O repositorio inteiro representa o produto `ComplexityAnalysis.Analyzers`. Codigo, configuracao, documentacao, testes, ferramentas e artefatos de desenvolvimento ficam organizados diretamente a partir da raiz.

Nao existe mais uma fronteira de workspace em `analyzer/`. Caminhos e instrucoes devem considerar a raiz do repositorio como base canonica.

## Fontes de verdade

Consulte apenas os arquivos relevantes para a tarefa atual:

1. `AGENTS.md`
2. `README.md`
3. `README.pt-BR.md`
4. `Directory.Packages.props`
5. `Directory.Build.props`
6. `Directory.Build.targets`
7. `.editorconfig`
8. `global.json`
9. `ComplexityAnalysis.Analyzers.slnx`
10. `docs/en/development/quality-gates.md`
11. `docs/pt-BR/development/quality-gates.md`
12. Skills em `.agents/skills/`
13. Projetos em `src/`, testes em `tests/` e harnesses em `performance/`

Nao carregue indiscriminadamente o repositorio. Localize primeiro o contexto diretamente relacionado a tarefa.

## Regras obrigatorias

- Nao criar `ProjectReference`, dependencia binaria ou pacote local para o antigo `complexity-hints`.
- Ao portar logica de uma referencia externa, copie apenas o minimo necessario.
- Registre o arquivo/classe de origem quando uma implementacao for portada.
- Preserve comportamento comprovado por testes.
- Nao portar dependencias pesadas automaticamente.
- Nao adicionar `MathNet.Numerics` sem decisao explicita.
- Nao adicionar `Microsoft.CodeAnalysis.Workspaces` enquanto nao houver necessidade comprovada.
- Dependencias de Roslyn usadas apenas para desenvolvimento devem possuir `PrivateAssets="all"`.
- O assembly do analyzer deve permanecer autocontido para o consumidor.
- Nao introduzir dependencias transitivas desnecessarias no pacote NuGet.
- Build e testes devem ser deterministicos.
- Performance do analyzer e requisito funcional.
- A Definition of Done canonica fica em `docs/en/development/quality-gates.md` com equivalente em `docs/pt-BR/development/quality-gates.md`.
- Nao executar publish, release ou push sem solicitacao explicita.
- Usar Conventional Commits.
- Sempre revisar o diff antes do commit.
- Executar validacao proporcional antes de concluir.

## Codigo original como referencia

O projeto original `complexity-hints` pode ser consultado externamente para entender algoritmos ou comportamento historico, mas nao faz parte deste repositorio.

Ele nunca deve ser referenciado pelo analyzer por `ProjectReference`, pacote local, copia binaria ou dependencia transitiva. Qualquer logica portada deve ser copiada e adaptada para este repositorio, com testes de caracterizacao e registro claro da origem quando aplicavel.

## Estrutura

- `src/ComplexityAnalysis.Analyzers/`: projeto principal do analyzer.
- `tests/ComplexityAnalysis.Analyzers.Tests/`: testes automatizados.
- `performance/`: harnesses e validacoes de performance.
- `docs/`: documentacao do produto.
- `.agents/skills/`: skills especificas para governanca, MSBuild, testes, cobertura e desenvolvimento Roslyn.
- `.github/workflows/`: workflows de CI do analyzer.
- `artifacts/`: saidas locais de build, pack, coverage e validacao; nao versionar.

## MSBuild e NuGet

- Este repositorio usa Central Package Management em `Directory.Packages.props`.
- `PackageReference` nos projetos nao deve conter `Version=` quando a versao estiver centralizada.
- Configuracoes globais do analyzer ficam em `Directory.Build.props` e `Directory.Build.targets`.
- O projeto do analyzer deve targetear `netstandard2.0`, salvo decisao explicita de arquitetura em contrario.
- O pacote deve ser estruturado como Roslyn Analyzer package, com o assembly em `analyzers/dotnet/cs/`.
- Evite `lib/netstandard2.0/` para o assembly do analyzer.

## Roslyn

- Use apenas APIs necessarias de `Microsoft.CodeAnalysis`.
- Evite `Microsoft.CodeAnalysis.Workspaces` enquanto nao houver CodeFix ou necessidade comprovada.
- Analyzer nao deve fazer chamadas de rede.
- Analyzer nao deve fazer I/O em hot paths.
- Analyzer nao deve depender de banco, infraestrutura externa ou configuracao de ambiente.
- Evite estado estatico mutavel.
- Respeite `CancellationToken`.
- Habilite analise concorrente quando seguro.
- Trate generated code explicitamente.
- A analise deve ser deterministica.
- Resultados inconclusivos devem ser representados explicitamente.
- Prefira nao reportar diagnostico a produzir falso positivo de alta severidade.

## Validacao

Execute validacoes proporcionais ao impacto. Consulte a matriz de tipo de mudanca e risco em `docs/en/development/quality-gates.md` antes de decidir quais validacoes focadas tambem se aplicam. Para mudancas de bootstrap, MSBuild, NuGet ou empacotamento:

```bash
dotnet restore ./ComplexityAnalysis.Analyzers.slnx
dotnet build ./ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ./ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
dotnet pack ./src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output ./artifacts/local-packages
```

Quando a tarefa afetar CI, cobertura, empacotamento ou performance, valide tambem os arquivos e harnesses diretamente relacionados em `.github/workflows/`, `tests/` e `performance/`.

## Git

- Nunca publique branch, push, GitHub Release, NuGet publish ou `dotnet nuget push` sem pedido explicito.
- Use commits semanticos.
- Revise `git diff --check` e `git diff` antes de commitar.
- Nao inclua artifacts locais, packages, coverage ou saidas de build no commit.

# AGENTS.md

## Objetivo

Desenvolver um Roslyn Analyzer independente para estimativa e diagnostico de complexidade algoritmica em codigo C#, tomando `complexity-hints` como reference implementation, sem criar dependencia binaria ou de projeto com o codigo herdado.

O trabalho neste workspace deve ser pequeno, verificavel, deterministico e adequado a um analyzer distribuivel futuramente como pacote NuGet. Responda em portugues, salvo pedido explicito em outro idioma.

## Escopo do workspace

Todo codigo, configuracao e documentacao especificos do novo analyzer devem permanecer sob `analyzer/`.

Arquivos fora de `analyzer/` pertencem ao projeto original herdado e sao read-only, salvo instrucao explicita do usuario.

## Fontes de verdade

Consulte apenas os arquivos relevantes para a tarefa atual:

1. `analyzer/AGENTS.md`
2. `analyzer/README.md`
3. `analyzer/Directory.Packages.props`
4. `analyzer/Directory.Build.props`
5. `analyzer/Directory.Build.targets`
6. `analyzer/.editorconfig`
7. `analyzer/global.json`
8. `analyzer/ComplexityAnalysis.Analyzers.sln`
9. Skills em `analyzer/.agents/skills/`
10. Projetos e testes dentro de `analyzer/src/` e `analyzer/tests/`

Nao carregue indiscriminadamente o projeto herdado. Localize primeiro o contexto diretamente relacionado a tarefa.

## Regras obrigatorias

- Codigo herdado fora de `analyzer/` e read-only, salvo instrucao explicita.
- Nao criar `ProjectReference` para projetos do `complexity-hints`.
- Nao modificar projetos herdados para facilitar a implementacao.
- Ao portar logica existente, copie apenas o minimo necessario.
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
- Nao executar publish, release ou push sem solicitacao explicita.
- Usar Conventional Commits.
- Sempre revisar o diff antes do commit.
- Executar validacao proporcional antes de concluir.

## Codigo original como referencia

Os projetos abaixo podem ser consultados para entender e portar algoritmos:

- `src/ComplexityAnalysis.Core`
- `src/ComplexityAnalysis.Roslyn`
- `src/ComplexityAnalysis.Solver`
- `src/ComplexityAnalysis.Engine`

Eles nunca devem ser referenciados pelo novo analyzer por `ProjectReference`, pacote local, copia binaria ou dependencia transitiva. Qualquer logica portada deve ser copiada e adaptada para o workspace `analyzer/`, com testes de caracterizacao e registro claro da origem.

## Estrutura

- `analyzer/src/ComplexityAnalysis.Analyzers/`: projeto principal do analyzer.
- `analyzer/tests/`: testes futuros do workspace isolado.
- `analyzer/.agents/skills/`: skills especificas para governanca, MSBuild, testes, cobertura e desenvolvimento Roslyn.
- `analyzer/artifacts/`: saidas locais de build, pack, coverage e validacao.

## MSBuild e NuGet

- Este workspace usa Central Package Management em `analyzer/Directory.Packages.props`.
- `PackageReference` em projetos dentro de `analyzer/` nao deve conter `Version=`.
- Configuracoes globais do analyzer ficam apenas em `analyzer/Directory.Build.props` e `analyzer/Directory.Build.targets`.
- Nao adicionar configuracoes equivalentes na raiz do repositorio.
- O projeto do analyzer deve targetear `netstandard2.0`.
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

Execute validacoes proporcionais ao impacto. Para mudancas de bootstrap, MSBuild, NuGet ou empacotamento:

```bash
dotnet restore ./ComplexityAnalysis.Analyzers.sln
dotnet build ./ComplexityAnalysis.Analyzers.sln --configuration Release --no-restore
dotnet pack ./src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local
```

Quando a tarefa puder afetar o isolamento, valide tambem a solution herdada na raiz do repositorio.

## Git

- Nunca publique branch, push, GitHub Release, NuGet publish ou `dotnet nuget push` sem pedido explicito.
- Use commits semanticos.
- Revise `git diff --check` e `git diff` antes de commitar.
- Nao inclua artifacts locais, packages, coverage ou saidas de build no commit.

# ComplexityAnalysis.Analyzers

Workspace isolado para bootstrap do futuro Roslyn Analyzer de complexidade algoritmica em C#.

Este diretorio convive com o projeto original herdado de `complexity-hints`, que permanece como reference implementation. O novo analyzer nao deve criar `ProjectReference`, dependencia binaria ou pacote local para os projetos originais.

## Estado atual

Bootstrap de infraestrutura, configuracao e governanca. Nenhum diagnostic, regra Roslyn ou analise Big-O foi implementado nesta fase.

## Decisoes iniciais

- Analyzer target: `netstandard2.0`.
- SDK de desenvolvimento: .NET 10 via `global.json`.
- Packages centralizados por CPM em `Directory.Packages.props`.
- Roslyn fixado inicialmente em `Microsoft.CodeAnalysis.CSharp` 4.8.0 para alinhar com a implementation de referencia e reduzir incompatibilidades de host.
- O pacote e preparado como Roslyn Analyzer package, com o assembly em `analyzers/dotnet/cs/`.

## Comandos

```bash
dotnet restore ComplexityAnalysis.Analyzers.sln
dotnet build ComplexityAnalysis.Analyzers.sln --configuration Release --no-restore
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local
```

## Regra de isolamento

Arquivos fora de `analyzer/` pertencem ao projeto original e devem permanecer intactos, salvo instrucao explicita. Ao portar logica futuramente, copie apenas o minimo necessario, registre a origem e cubra o comportamento com testes de caracterizacao.

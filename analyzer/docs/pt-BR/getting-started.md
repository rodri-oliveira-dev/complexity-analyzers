# Primeiros Passos

[English](../en/getting-started.md) | Português (Brasil)

Esta pagina explica como compilar, testar, empacotar e consumir o workspace isolado do analyzer ate a Phase 3.

## Pre-Requisitos

- .NET SDK `10.0.100`, ou um SDK compativel selecionado por `analyzer/global.json`.
- Git.
- Um shell capaz de executar comandos `dotnet`.

O projeto do analyzer targeteia `netstandard2.0` porque Roslyn analyzers sao carregados por hosts de compilador e IDE, nao pelo runtime da aplicacao analisada. O projeto de testes targeteia `net10.0`.

## Clone e Build

A partir da raiz do repositorio:

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.sln
dotnet build ComplexityAnalysis.Analyzers.sln --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.sln --configuration Release --no-build
```

A solution e isolada dentro de `analyzer/`. Arquivos fora desse diretorio pertencem a implementacao herdada e sao apenas referencia para este workspace do analyzer.

## Pack

Crie um pacote analyzer local:

```bash
cd analyzer
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.3.0-docs-local
```

O pacote e um pacote Roslyn analyzer. O assembly do analyzer e empacotado em:

```text
analyzers/dotnet/cs/
```

O projeto define `PackageReadmeFile` como `README.md`, e o README empacotado e o `analyzer/README.md` em ingles.

## Consumo Local

O pacote atualmente e consumido a partir de uma build/fonte de pacote local. Nao trate esta documentacao como evidencia de publicacao no NuGet.org.

Um fluxo local possivel e:

```bash
cd analyzer
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.3.0-docs-local --output artifacts/local-packages
dotnet new console -o artifacts/tmp/AnalyzerConsumer
cd artifacts/tmp/AnalyzerConsumer
dotnet nuget add source ../../local-packages --name complexity-analysis-local
dotnet add package ComplexityAnalysis.Analyzers --version 0.3.0-docs-local --source complexity-analysis-local
```

O diretorio exato de saida do pacote pode variar conforme SDK e configuracao do repositorio. Se necessario, aponte a fonte NuGet local para o diretorio que contem o `.nupkg` gerado.

## PackageReference

Pacotes analyzer normalmente sao referenciados com `PrivateAssets="all"` para afetar a compilacao sem virar dependencia transitiva dos projetos consumidores:

```xml
<PackageReference
    Include="ComplexityAnalysis.Analyzers"
    Version="<local-or-published-version>"
    PrivateAssets="all" />
```

O analyzer nao e uma biblioteca de runtime. O codigo da aplicacao nao chama seus tipos.

```text
aplicacao
    |
    | compilada por
    v
compilador Roslyn
    |
    | carrega
    v
ComplexityAnalysis.Analyzers
```

## Smoke Test Com BIG9000

`BIG9000` e desabilitado por padrao. Para provar que o analyzer foi carregado por um projeto consumidor, crie ou edite `.editorconfig` no consumidor:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Depois compile o projeto consumidor. Se `BIG9000` aparecer, isso nao significa que seu codigo tem um problema. Significa que o probe de execucao foi habilitado explicitamente e o analyzer executou com sucesso.

Depois do smoke test, desabilite novamente:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Veja [Configuracao](configuration.md) para detalhes.

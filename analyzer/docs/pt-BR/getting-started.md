# Primeiros Passos

[English](../en/getting-started.md) | Portugues (Brasil)

Esta pagina explica como compilar, testar, empacotar e consumir o workspace isolado do analyzer ate a Phase 6.

## Pre-Requisitos

- .NET SDK `10.0.100`, ou um SDK compativel selecionado por `analyzer/global.json`.
- Git.
- Um shell capaz de executar comandos `dotnet`.

O projeto do analyzer targeteia `netstandard2.0` porque Roslyn analyzers sao carregados por hosts de compilador e IDE, nao pelo runtime da aplicacao analisada. O projeto de testes targeteia `net10.0`.

## Clone e Build

A partir da raiz do repositorio:

```bash
cd analyzer
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

A solution e isolada dentro de `analyzer/`. Arquivos fora desse diretorio pertencem a implementacao herdada e sao apenas referencia para este workspace do analyzer.

## Pack

Crie um pacote analyzer local:

```bash
cd analyzer
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.6.0-phase6-local
```

O pacote e um pacote Roslyn analyzer. O assembly do analyzer e empacotado em:

```text
analyzers/dotnet/cs/
```

O projeto define `PackageReadmeFile` como `README.md`, e o README empacotado e o `analyzer/README.md` em ingles.

## Consumo Local

O pacote atualmente e consumido a partir de uma fonte de pacote local. Nao trate esta documentacao como evidencia de publicacao no NuGet.org.

Um fluxo local possivel e:

```bash
cd analyzer
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.6.0-phase6-local --output artifacts/local-packages
dotnet new console -o artifacts/tmp/AnalyzerConsumer
cd artifacts/tmp/AnalyzerConsumer
dotnet nuget add source ../../local-packages --name complexity-analysis-local
dotnet add package ComplexityAnalysis.Analyzers --version 0.6.0-phase6-local --source complexity-analysis-local
```

Se necessario, aponte a fonte NuGet local para o diretorio que contem o `.nupkg` gerado.

## PackageReference

Pacotes analyzer normalmente sao referenciados com `PrivateAssets="all"` para afetar a compilacao sem virar dependencia transitiva dos projetos consumidores:

```xml
<PackageReference
    Include="ComplexityAnalysis.Analyzers"
    Version="<local-or-published-version>"
    PrivateAssets="all" />
```

O analyzer nao e uma biblioteca de runtime. O codigo da aplicacao nao chama seus tipos.

## Smoke Tests de Diagnostics

`BIG1001`, `BIG1002`, `BIG1003`, `BIG1004` e `BIG1005` sao habilitados por padrao como diagnostics `Info`. A visibilidade no build depende do projeto consumidor e das configuracoes do SDK. Voce pode promover uma regra localmente:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

Promova o diagnostic de chamada fonte em loop da Phase 5:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

Promova o diagnostic de recursao exponencial da Phase 6:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

`BIG0001` e desabilitado por padrao. Habilite quando quiser estimativas de complexidade por metodo, incluindo custos de metodos fonte suportados e recursao direta resolvida:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

`BIG9000` e desabilitado por padrao. Para provar que o analyzer carregou, habilite temporariamente:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Desabilite o probe depois do smoke test:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Veja [Configuracao](configuration.md) para detalhes.

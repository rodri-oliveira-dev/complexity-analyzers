# Primeiros Passos

[English](../en/getting-started.md) | Portugues (Brasil)

Esta pagina explica como compilar, testar, empacotar e consumir o workspace isolado do analyzer ate a Phase 7.

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
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
```

O pacote e um pacote Roslyn analyzer. O assembly do analyzer e empacotado em:

```text
analyzers/dotnet/cs/
```

O projeto define `PackageReadmeFile` como `README.md`, e o README empacotado e o `analyzer/README.md` em ingles.
O pacote usa a URL do repositorio como metadata de projeto, declara o tipo de repositorio como `git` e usa a declaracao de licenca MIT do repositorio como expressao de licenca NuGet.

O pacote intencionalmente nao tem asset runtime em `lib/` e nao expoe dependencia Roslyn transitiva. Testes de contrato inspecionam o `.nupkg` gerado como arquivo ZIP.

A geracao de `.snupkg` nao esta habilitada para o layout atual do analyzer porque o pacote mantem a DLL fora do build output convencional e dentro de `analyzers/dotnet/cs/`. O Source Link build tooling e fornecido pelo SDK .NET atual, entao nenhuma referencia de pacote Source Link e necessaria.

## Consumo Local

O pacote atualmente e consumido a partir de uma fonte de pacote local. Nao trate esta documentacao como evidencia de publicacao no NuGet.org.

A partir da raiz do repositorio, um fluxo local em PowerShell e:

```powershell
cd analyzer
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local --output artifacts/local-packages
$packageSource = (Resolve-Path "artifacts/local-packages").Path
$consumer = Join-Path ([System.IO.Path]::GetTempPath()) ("AnalyzerConsumer-" + [System.Guid]::NewGuid().ToString("N"))
dotnet new console -o $consumer
cd $consumer
dotnet new nugetconfig
dotnet nuget add source $packageSource --name complexity-analysis-local --configfile NuGet.config
dotnet add package ComplexityAnalysis.Analyzers --version 0.0.0-local --source $packageSource
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

`BIG1001`, `BIG1002`, `BIG1003`, `BIG1004`, `BIG1005` e `BIG1006` sao habilitados por padrao como diagnostics `Info`. `BIG1006` ainda precisa de um threshold `maximum_complexity` configurado antes de reportar. A visibilidade no build depende do projeto consumidor e das configuracoes do SDK. Voce pode promover uma regra localmente:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

Promova o diagnostic de chamada fonte em loop da Phase 5:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

Promova o diagnostic de recursao exponencial:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

Configure e promova o diagnostic de threshold de complexidade da Phase 7:

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
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

## Matriz de Compatibilidade

O CI valida consumo do pacote analyzer nos hosts SDK suportados:

| Host SDK | Target framework do consumidor |
| --- | --- |
| .NET 8 LTS | `net8.0` |
| .NET 9 STS | `net9.0` |
| .NET 10 LTS | `net10.0` |

O assembly do analyzer targeteia `netstandard2.0`, mas o check de compatibilidade verifica hosts de compilador carregando o pacote do analyzer, nao apenas o target framework do consumidor.

## Validacao de Performance

A partir de `analyzer/`, execute o harness estrutural de performance:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build --filter PerformanceSyntheticCorpusTests
```

Depois do restore, obtenha o report de execucao do analyzer pelo compilador:

```bash
dotnet build ./performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj --configuration Release --no-restore -t:Rebuild -p:ReportAnalyzer=true -p:UseSharedCompilation=false -v:detailed
```

Tempo varia por hardware e runner. O gate util e que o workload sintetico completa, as invariantes estruturais passam e o report do compilador inclui `ComplexityAnalysis.Analyzers.ComplexityAnalyzer`.

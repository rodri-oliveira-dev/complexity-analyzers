# Primeiros Passos

[English](../en/getting-started.md) | Português (Brasil)

Este guia explica como compilar, testar, empacotar e validar o `ComplexityAnalysis.Analyzers` usando o layout atual do repositório.

## Pré-requisitos

- .NET SDK `10.0.400`, ou um SDK compatível selecionado pelo `global.json` da raiz.
- Git.
- Um shell capaz de executar comandos `dotnet`.

O projeto do analyzer targeteia `netstandard2.0` porque Roslyn Analyzers são carregados por hosts de compilador e IDE, e não pelo runtime da aplicação analisada. Os testes e as ferramentas do repositório usam o SDK selecionado pelo `global.json`.

O SDK de build do repositório não é a versão mínima do host consumidor. Build e testes usam atualmente o SDK `10.0.400`; a compatibilidade do pacote é validada separadamente instalando o `.nupkg` gerado em projetos consumidores compilados pelos hosts SDK suportados.

## Clonar e compilar

A partir da raiz do repositório:

```bash
dotnet restore ComplexityAnalysis.Analyzers.slnx
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release --no-restore
dotnet test ComplexityAnalysis.Analyzers.slnx --configuration Release --no-build
```

O analyzer é o produto representado pela raiz do repositório. O código de produção fica em `src/`, os testes em `tests/`, a validação de performance em `performance/` e a documentação em `docs/`.

## Criar um pacote local

Compile primeiro e depois gere um pacote NuGet local:

```bash
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj \
  --configuration Release \
  --no-build \
  -p:PackageVersion=0.0.0-local \
  --output artifacts/local-packages
```

O pacote gerado é um pacote Roslyn Analyzer. O assembly `ComplexityAnalysis.Analyzers.dll` é empacotado em:

```text
analyzers/dotnet/cs/
```

Ele não é empacotado como uma biblioteca de runtime normal em `lib/`. As dependências usadas para autoria do analyzer são assets privados e não devem se tornar dependências transitivas dos projetos consumidores.

O projeto usa `README.md` como README do pacote e declara a URL do repositório, o tipo do repositório e a licença MIT nos metadados do pacote.

O contrato do pacote também espera ausência de `.deps.json`, ausência de DLL duplicada do analyzer, ausência de asset runtime `lib/` para o assembly do analyzer e ausência de grupo de dependências transitivas de Roslyn no `.nuspec` gerado.

## Consumir o pacote local

O repositório documenta a criação e o consumo local do pacote. Não trate este guia como evidência de que já existe uma versão publicada no NuGet.org.

Um fluxo possível em PowerShell é:

```powershell
dotnet build ComplexityAnalysis.Analyzers.slnx --configuration Release
dotnet pack src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj `
  --configuration Release `
  --no-build `
  -p:PackageVersion=0.0.0-local `
  --output artifacts/local-packages

$packageSource = (Resolve-Path "artifacts/local-packages").Path
$consumer = Join-Path ([System.IO.Path]::GetTempPath()) ("AnalyzerConsumer-" + [System.Guid]::NewGuid().ToString("N"))

dotnet new console -o $consumer
Set-Location $consumer
dotnet new nugetconfig
dotnet nuget add source $packageSource --name complexity-analysis-local --configfile NuGet.config
dotnet add package ComplexityAnalysis.Analyzers --version 0.0.0-local --source $packageSource
```

Se necessário, aponte a fonte NuGet local diretamente para o diretório que contém o `.nupkg` gerado.

## PackageReference

Pacotes de analyzer normalmente são referenciados com `PrivateAssets="all"` para participarem da compilação sem se tornarem dependências transitivas de runtime:

```xml
<PackageReference
    Include="ComplexityAnalysis.Analyzers"
    Version="<local-or-published-version>"
    PrivateAssets="all" />
```

O código da aplicação consumidora não chama tipos do analyzer em runtime.

## Validar que o analyzer está executando

`BIG9000` é um probe de infraestrutura opt-in. Habilite-o temporariamente quando precisar comprovar que o pacote foi carregado e o analyzer executou:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Depois do smoke test, desabilite-o novamente:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## Experimentar os diagnósticos

`BIG1001` até `BIG1005` são habilitados por padrão com severidade `Info`. `BIG1006`, `BIG2001` e `BIG2002` também são habilitados como descriptors, mas só reportam quando thresholds concretos são configurados. A visibilidade de diagnósticos informativos no build depende do projeto consumidor e das configurações do SDK.

Promova um diagnóstico acionável localmente:

```ini
[*.cs]

dotnet_diagnostic.BIG1001.severity = warning
```

Promova o diagnóstico de chamada a método-fonte dentro de loop:

```ini
[*.cs]

dotnet_diagnostic.BIG1004.severity = warning
```

Promova o diagnóstico de recursão exponencial:

```ini
[*.cs]

dotnet_diagnostic.BIG1005.severity = warning
```

Configure um limite máximo de complexidade:

```ini
[*.cs]

complexity_analyzers.maximum_complexity = n_log_n
dotnet_diagnostic.BIG1006.severity = warning
```

Promova o diagnóstico de threshold de Cyclomatic Complexity:

```ini
[*.cs]

complexity_analyzers.maximum_cyclomatic_complexity = 10
complexity_analyzers.cyclomatic_complexity_mode = standard
dotnet_diagnostic.BIG2001.severity = warning
```

Promova o diagnóstico de threshold de Maximum Control-Flow Nesting Depth:

```ini
[*.cs]

complexity_analyzers.maximum_nesting_depth = 3
dotnet_diagnostic.BIG2002.severity = warning
```

`BIG0001` é desabilitado por padrão. Habilite-o quando quiser estimativas de complexidade por método:

```ini
[*.cs]

dotnet_diagnostic.BIG0001.severity = suggestion
```

Consulte [Configuração](configuration.md) e o [Catálogo de Analyzers](analyzers.md) para o comportamento completo de cada opção e regra.

## Matriz de compatibilidade

O CI valida o consumo local do pacote nos hosts de SDK suportados:

| Host SDK | Target framework do consumidor |
| --- | --- |
| .NET 8 LTS | `net8.0` |
| .NET 9 STS | `net9.0` |
| .NET 10 LTS | `net10.0` |

O assembly do analyzer targeteia `netstandard2.0`. A matriz de compatibilidade verifica se os hosts do compilador conseguem carregar e executar o pacote do analyzer, e não apenas se o projeto consumidor consegue targetear determinado framework.

O analyzer é compilado contra `Microsoft.CodeAnalysis.CSharp` `4.8.0`, que resolve `Microsoft.CodeAnalysis.Common` `4.8.0`. Este é um baseline conservador de host Roslyn: upgrades de dependência exigem inspeção do pacote e execução bem-sucedida em consumidores de toda a matriz suportada antes do merge. `Microsoft.CodeAnalysis.Workspaces` está intencionalmente ausente porque o pacote não fornece code fixes nem features de IDE baseadas em workspace.

## Validação de performance

Execute o harness estrutural de performance a partir da raiz do repositório:

```bash
dotnet test ComplexityAnalysis.Analyzers.slnx \
  --configuration Release \
  --no-build \
  --filter PerformanceSyntheticCorpusTests
```

Depois do restore, solicite o relatório de execução de analyzers do compilador:

```bash
dotnet build performance/ComplexityAnalysis.Analyzers.Performance/ComplexityAnalysis.Analyzers.Performance.csproj \
  --configuration Release \
  --no-restore \
  -t:Rebuild \
  -p:ReportAnalyzer=true \
  -p:UseSharedCompilation=false \
  -v:detailed
```

O tempo varia conforme hardware e runner de CI. A validação reproduzível é que o workload sintético conclua, as invariantes estruturais sejam atendidas e o relatório do compilador contenha `ComplexityAnalysis.Analyzers.ComplexityAnalyzer`.

## Próximos passos

- Leia o [Catálogo de Analyzers](analyzers.md) para entender cada diagnóstico.
- Leia [Configuração](configuration.md) para ajustar budgets de análise e severidades.
- Leia [Arquitetura](architecture.md) para conhecer o pipeline do analyzer e suas fronteiras de design.

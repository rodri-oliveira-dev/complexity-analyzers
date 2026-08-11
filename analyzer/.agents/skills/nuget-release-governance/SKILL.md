---
name: nuget-release-governance
description: Use esta skill para revisar ou ajustar pack, versionamento, metadados NuGet, GitHub Actions, validacao de package, release notes e publicacao segura do analyzer. Nunca use para publicar de fato sem pedido explicito.
---

# NuGet Release Governance

## Objetivo

Orientar mudancas de release do `ComplexityAnalysis.Analyzers` como pacote NuGet de Roslyn Analyzer, preservando rastreabilidade, seguranca e controle explicito de publicacao.

## Quando usar

- Alterar `dotnet pack`, metadados de package, `.nupkg` ou `.snupkg`.
- Revisar versionamento, release notes ou GitHub Actions de release.
- Validar conteudo do pacote antes de publicar.
- Avaliar supply-chain, permissoes, secrets, assinaturas, provenance ou seguranca de pipeline.

## Regras obrigatorias

- Nunca executar `dotnet nuget push` sem solicitacao explicita.
- Nunca criar GitHub Release sem solicitacao explicita.
- Nunca publicar pacote real automaticamente.
- Nunca ampliar permissoes de workflow sem justificativa.
- Nunca introduzir segredo em arquivo versionado.
- O package do analyzer deve carregar o assembly em `analyzers/dotnet/cs/`.
- Roslyn e ferramentas de desenvolvimento devem permanecer privadas quando aplicavel.
- O pacote nao deve expor dependencias transitivas desnecessarias.

## Checklist de package

- `PackageId`, `Description`, tags e repository metadata estao corretos.
- `PackageVersion` foi informado de forma explicita no pack.
- `.nupkg` contem o DLL do analyzer em `analyzers/dotnet/cs/`.
- `.nupkg` nao contem `lib/netstandard2.0/` para o assembly do analyzer.
- `.nupkg` nao contem DLLs do projeto original.
- Roslyn nao aparece como dependencia transitiva no `.nuspec`.
- `.snupkg` so e produzido quando configurado intencionalmente.
- Release notes descrevem mudancas observaveis e breaking changes.

## Validacao

```bash
dotnet restore ./ComplexityAnalysis.Analyzers.sln
dotnet build ./ComplexityAnalysis.Analyzers.sln --configuration Release --no-restore
dotnet pack ./src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local
```

Inspecione o `.nupkg` localmente antes de concluir alteracoes de empacotamento.

---
name: roslyn-analyzer-development
description: Use esta skill para implementar ou revisar codigo do Roslyn Analyzer de complexidade, incluindo diagnostics, analise sintatica, analise semantica, performance, determinismo, empacotamento e isolamento do projeto original.
---

# Roslyn Analyzer Development

## Objetivo

Guiar o desenvolvimento do `ComplexityAnalysis.Analyzers` como analyzer independente, performatico, deterministico e seguro para consumo via NuGet.

## Regras obrigatorias

- O analyzer target e `netstandard2.0`.
- O analyzer nao depende dos projetos originais.
- Nao criar `ProjectReference` para `ComplexityAnalysis.Core`, `ComplexityAnalysis.Roslyn`, `ComplexityAnalysis.Solver`, `ComplexityAnalysis.Engine`, `ComplexityAnalysis.Calibration` ou `ComplexityAnalysis.IDE`.
- Nao fazer chamadas de rede.
- Nao fazer I/O em hot paths.
- Nao depender de banco, containers, filas, cloud ou infraestrutura.
- Evitar estado estatico mutavel.
- Respeitar `CancellationToken`.
- Habilitar analise concorrente quando seguro.
- Tratar generated code explicitamente.
- Evitar `Microsoft.CodeAnalysis.Workspaces` enquanto nao houver CodeFix ou necessidade comprovada.
- Usar apenas APIs necessarias de `Microsoft.CodeAnalysis`.
- Performance do analyzer e requisito funcional.
- A analise deve ser deterministica.
- Resultados inconclusivos devem ser explicitamente representados.
- Preferir nao reportar diagnostico a produzir falso positivo de alta severidade.
- Qualquer logica portada do projeto original deve ter testes de caracterizacao.

## Processo

1. Leia `analyzer/AGENTS.md`, o projeto alvo e os testes relacionados.
2. Confirme que a mudanca pertence ao workspace `analyzer/`.
3. Para diagnostics, defina ID, categoria, severidade, mensagem, localizacao e comportamento negativo antes de implementar.
4. Registre acoes Roslyn no nivel mais especifico suficiente.
5. Evite trabalho repetido por node quando puder compartilhar dados imutaveis e seguros.
6. Use `context.EnableConcurrentExecution()` quando o analyzer for thread-safe.
7. Defina politica de generated code com `ConfigureGeneratedCodeAnalysis`.
8. Passe `CancellationToken` para APIs Roslyn que o aceitam.
9. Evite alocacoes desnecessarias em callbacks muito frequentes.
10. Prefira estruturas imutaveis ou locais a caches globais.
11. Teste caminhos positivos, negativos, inconclusivos e edge cases.

## Validacao

```bash
dotnet restore ./ComplexityAnalysis.Analyzers.sln
dotnet build ./ComplexityAnalysis.Analyzers.sln --configuration Release --no-restore
dotnet test ./ComplexityAnalysis.Analyzers.sln --configuration Release --no-build
```

Para mudancas de empacotamento, valide tambem:

```bash
dotnet pack ./src/ComplexityAnalysis.Analyzers/ComplexityAnalysis.Analyzers.csproj --configuration Release --no-build -p:PackageVersion=0.0.0-local
```

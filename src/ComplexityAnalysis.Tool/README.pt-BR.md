# ComplexityAnalysis.Tool

[English](README.md) | Português (Brasil)

`ComplexityAnalysis.Tool` é uma ferramenta .NET determinística para analisar
complexidade algorítmica e estrutural em projetos e solutions C#. Use-a em
revisões locais, relatórios para automação, comparação de snapshots, detecção de
código duplicado e quality gates de CI.

Os produtos do repositório atendem pontos de integração diferentes:

- `ComplexityAnalysis.Analyzers` executa dentro do Roslyn em builds e IDEs.
- `ComplexityAnalysis.Tool` fornece o comando `complexity-analysis` para
  relatórios e gates em projetos e solutions.
- `ComplexityAnalysis.Mcp` é um host MCP local e somente leitura, separado. Ele
  não é dependência dos pacotes da CLI nem do analyzer.

## Pré-requisitos

O runtime .NET 10 é obrigatório. O SDK .NET 10 é recomendado para manifests de
ferramentas locais e uso em CI.

## Instalar Pelo NuGet.org

Depois da publicação da versão `1.0.0`, instale globalmente:

```bash
dotnet tool install --global ComplexityAnalysis.Tool --version 1.0.0
complexity-analysis --version
```

Ou fixe a versão em um manifest local do repositório:

```bash
dotnet new tool-manifest
dotnet tool install ComplexityAnalysis.Tool --version 1.0.0
dotnet tool restore
dotnet tool run complexity-analysis --version
```

Atualize ou remova uma instalação global com:

```bash
dotnet tool update --global ComplexityAnalysis.Tool --version <version>
dotnet tool uninstall --global ComplexityAnalysis.Tool
```

Para um manifest local, omita `--global` e execute com `dotnet tool run
complexity-analysis` ou `dotnet complexity-analysis`.

Comandos de pacote publicado usam o NuGet.org. Um pacote compilado do código-fonte
não é uma release publicada: instale-o somente com uma fonte e versão locais
explícitas, por exemplo `--add-source ./artifacts/local-packages --version
0.0.0-local`.

## Primeiro Uso

```bash
complexity-analysis analyze MySolution.slnx
complexity-analysis analyze MyProject.csproj --format json
complexity-analysis analyze MyProject.csproj --format sarif
complexity-analysis analyze MyProject.csproj --max-cyclomatic-complexity 10
```

Console é o formato padrão. JSON atende automação e snapshots; SARIF 2.1.0 se
integra a sistemas de code scanning compatíveis. Use `--output <path>` para
gravar qualquer relatório.

## Quality Gates E Exit Codes

Gates são opt-in; nenhum threshold padrão falha um build. Há gates para Big-O,
complexidade ciclomática e cognitiva, profundidade, NLOC, quantidade de
statements, tokens e parâmetros, taxa de duplicação e tokens duplicados.

- `0`: comando concluído e gates configurados aprovados.
- `1`: comando inválido, falha de entrada/carregamento ou escrita do relatório.
- `2`: um quality gate configurado explicitamente falhou.
- `130`: operação cancelada.

## Snapshots E Comparação De Regressões

Crie snapshots JSON determinísticos e compare-os sem Git ou serviço externo:

```bash
complexity-analysis analyze MyProject.csproj --format json --snapshot --output baseline.json
complexity-analysis analyze MyProject.csproj --format json --snapshot --output current.json
complexity-analysis compare baseline.json current.json --format json --fail-on-regression
```

O gate retorna `2` somente para regressões comprovadas. Evidência desconhecida
ou incompatível permanece explicitamente incomparável.

## Detecção De Duplicação

```bash
complexity-analysis analyze MyProject.csproj \
  --detect-duplicates \
  --min-duplicate-tokens 40 \
  --max-duplicate-rate 5
```

A detecção é opt-in e conhece a sintaxe C#. Mínimos de tokens e linhas
selecionam candidatos; máximos de taxa ou tokens duplicados habilitam gates.

## Referência Dos Comandos

Use `complexity-analysis --help`, `complexity-analysis analyze --help` e
`complexity-analysis compare --help` para as opções exatas da versão instalada.
A referência completa, defaults, restrições, contratos de relatório, semântica
de snapshots, normalização de duplicação e exemplos de CI estão no
[guia de tooling](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/docs/pt-BR/project-tooling.md).

## Projeto

- [Repositório](https://github.com/rodri-oliveira-dev/complexity-analyzers)
- [Como contribuir](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/CONTRIBUTING.pt-BR.md)
- [Licença MIT](https://github.com/rodri-oliveira-dev/complexity-analyzers/blob/main/LICENSE)

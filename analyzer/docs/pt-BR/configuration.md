# Configuracao

[English](../en/configuration.md) | Português (Brasil)

`ComplexityAnalysis.Analyzers` usa a configuracao padrao de severidade de diagnostics Roslyn. Nao ha opcoes customizadas do analyzer ate a Phase 3.

## Formato .editorconfig

Use o formato padrao por regra:

```ini
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Valores comuns incluem:

```text
none
silent
suggestion
warning
error
default
```

O comportamento exato no build e controlado pelas convencoes do compilador .NET e do SDK para analyzer diagnostics.

## Padrao do BIG9000

`BIG9000` e desabilitado por padrao em seu descriptor:

```text
Default severity: Info
Enabled by default: false
```

Sem configuracao explicita, os testes confirmam que o analyzer nao reporta `BIG9000`.

## Manter BIG9000 Desabilitado

Use isto em projetos normais:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

## Habilitar BIG9000 Para Smoke Test

Use `suggestion` quando quiser um sinal visivel de baixa severidade:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = suggestion
```

Use `warning` quando quiser que o probe fique obvio em um teste temporario de consumo de pacote ou CI:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

`warning` altera a severidade configurada pelo consumidor. Isso nao significa que `BIG9000` seja originalmente um warning. O analyzer o define como diagnostic `Info`.

## Desabilitar Novamente

Depois do smoke test, remova a configuracao explicita ou defina:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Nao mantenha `BIG9000` habilitado permanentemente, a menos que voce queira intencionalmente um diagnostic de infraestrutura em toda compilation onde o analyzer executa.

## O Que Ainda Nao E Configuravel

Ate a Phase 3, nao ha opcoes publicas para:

- thresholds de Big-O;
- severidade por complexidade de loops;
- comportamento de mappings BCL ou LINQ;
- tratamento de recursao;
- resolucao de chamadas de metodo;
- IDs de diagnostics de produto.

Essas capacidades ainda nao estao expostas como diagnostics.

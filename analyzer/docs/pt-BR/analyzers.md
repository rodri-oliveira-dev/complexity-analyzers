# Catalogo de Analyzers

[English](../en/analyzers.md) | Português (Brasil)

Esta pagina e o catalogo canonico, destinado a usuarios, dos diagnostics atualmente expostos por `ComplexityAnalysis.Analyzers`.

A implementacao foi inventariada a partir de `DiagnosticDescriptor`, `SupportedDiagnostics`, `Diagnostic.Create` e IDs de regra `BIG` no codigo atual. Ate a Phase 3, apenas `BIG9000` existe como diagnostic publico.

As capacidades internas de analise sao documentadas separadamente dos diagnostics. O modelo interno e a extracao Roslyn conseguem derivar varias formas assintoticas, mas diagnostics de produto que exponham esses resultados aos desenvolvedores nao fazem parte da Phase 3.

## BIG9000 - Analyzer Execution Probe

| Propriedade | Valor |
| --- | --- |
| ID | `BIG9000` |
| Titulo | `Analyzer execution probe` |
| Categoria | `Infrastructure` |
| Severidade padrao | `Info` |
| Habilitado por padrao | `false` |
| Mensagem | `ComplexityAnalysis.Analyzers execution probe is active` |
| Descricao | Reporta uma vez por compilation quando habilitado explicitamente para provar que o analyzer executou. |
| Introduzido | Phase 1 - Analyzer Foundation |

## O Que Ele Detecta

`BIG9000` detecta infraestrutura de execucao do analyzer, nao comportamento do codigo da aplicacao.

Ele prova que o pacote do analyzer foi:

- carregado pelo compilador ou host;
- inicializado;
- executado;
- capaz de emitir diagnostics.

O analyzer registra uma compilation action e reporta o probe em uma localizacao de codigo-fonte quando ha codigo-fonte disponivel. Os testes cobrem que ele e emitido no maximo uma vez por compilation quando habilitado explicitamente.

## Por Que Isso Importa

O probe e util para validar empacotamento, consumo local, smoke tests de CI ou integracao com editor/compilador.

Se `BIG9000` aparecer, isso nao significa que seu codigo tem um problema. Significa que o probe de execucao foi habilitado explicitamente e o analyzer executou com sucesso.

`BIG9000` nao:

- identifica codigo ineficiente;
- calcula Big-O;
- inspeciona loops para gerar um warning publico;
- representa uma regra de produto de performance;
- indica bug no projeto consumidor.

## Exemplo

Qualquer compilation C# pode produzir o probe quando ele e habilitado explicitamente:

```csharp
public sealed class Sample
{
    public int M() => 42;
}
```

O diagnostic e independente da complexidade desse metodo. Ele e reportado pela infraestrutura do analyzer em nivel de compilation.

## Configuracao

Mantenha o probe desabilitado:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = none
```

Habilite para um smoke test local:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = suggestion
```

Torne-o bem visivel em um teste temporario de CI ou de consumo de pacote:

```ini
[*.cs]

dotnet_diagnostic.BIG9000.severity = warning
```

Definir `warning` altera a severidade configurada pelo consumidor. O descriptor do analyzer continua definindo `BIG9000` como `Info` e desabilitado por padrao.

Nao mantenha `BIG9000` habilitado permanentemente em projetos normais, a menos que voce queira intencionalmente um sinal recorrente de infraestrutura.

## Planejado / Ainda Nao Disponivel

Diagnostics de produto baseados na complexidade Big-O extraida estao planejados para uma fase posterior. Nenhum ID futuro e documentado como disponivel aqui porque eles nao estao presentes no codigo atual.

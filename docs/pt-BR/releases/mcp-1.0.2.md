# ComplexityAnalysis.Mcp 1.0.2

Esta release patch em preparação inclui uma correção no motor de análise compartilhado e manutenção de dependências.

## Alterações

- Evita falhas na resolução de operações conhecidas em tipos anônimos e
  delegates anônimos. Identidades não suportadas permanecem como `Unknown`
  conservador (#149).
- Atualiza ModelContextProtocol para 2.4.0 e Microsoft.Extensions.Hosting para
  11.0.0.

## Compatibilidade e validação

O pacote continua direcionado ao .NET 10 e disponibiliza o comando
`complexity-mcp`. Não há intenção de alterar os schemas das ferramentas MCP.
Antes da publicação, valide a inicialização da ferramenta instalada, as chamadas
somente leitura, o cancelamento e os contratos de empacotamento existentes.

## Estado da publicação

A versão `1.0.2` está preparada, sem anúncio de publicação. O workflow protegido
valida o pacote, publica os artefatos NuGet e GitHub, cria a tag imutável
`mcp-v1.0.2` e a release, e sincroniza os metadados do Official MCP Registry após
a publicação do pacote. Verifique a publicação e a disponibilidade no registro
antes de atualizar as referências de instalação ou descoberta.

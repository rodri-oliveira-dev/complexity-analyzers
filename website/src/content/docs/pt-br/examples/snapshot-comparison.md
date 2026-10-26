---
title: Comparação de snapshots
description: Produza snapshots determinísticos e falhe somente em regressões de complexidade comprovadas.
sidebar:
  order: 5
---

Crie um baseline e um snapshot atual usando o contrato JSON:

```bash
complexity-analysis analyze App.csproj --format json --snapshot --output baseline.json
complexity-analysis analyze App.csproj --format json --snapshot --output current.json
complexity-analysis compare baseline.json current.json --format json --fail-on-regression
```

Membros são pareados por identidade estável. Um aumento conhecido como `O(n)` para `O(n²)` pode ser classificado como regressão comprovada e retornar código `2` quando `--fail-on-regression` está habilitado.

Uma estimativa `Unknown`, membro ausente ou evidência incompatível não é ordenada silenciosamente contra uma complexidade conhecida. A comparação registra o resultado como incomparável para permitir investigação sem gate falso.

Versione ou retenha baselines somente conforme a política de artefatos e revisão do seu repositório. A comparação é determinística e não depende do histórico Git nem de serviço externo.

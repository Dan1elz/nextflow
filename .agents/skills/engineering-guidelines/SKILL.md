---
name: engineering-guidelines
description: Padrões de código da API Nextflow (.NET 8, Clean Architecture); use antes de criar ou alterar código, revisar um diff ou decidir onde colocar uma regra.
---

# Padrões de engenharia — API

A fonte é a documentação do produto, não esta skill. Leia, nesta ordem e só o necessário:
1. `../nextflow-docs/docs/engineering-guidelines/README.md` (princípios comuns e definição de pronto).
2. `../nextflow-docs/docs/engineering-guidelines/backend.md` (camadas, exceções, convenções, comandos).
3. `../nextflow-docs/docs/engineering-guidelines/testing.md` se for escrever teste.
4. Regras do módulo afetado: `../nextflow-docs/docs/architecture/modules/<módulo>/business-rules.md`.

Checklist ao terminar:
- Regra de negócio nova ou alterada está documentada (ID `RN-*`) e, se for escolha, em uma decisão numerada.
- Dependências entre camadas respeitadas; erros com as exceções de negócio; datas via `BusinessClock`.
- Mudou enum, DTO ou rota? Atualize `docs/PROJECT.md`, `docs/AI_FORECAST_SPEC.md` (se for da previsão) e avise o front.
- `dotnet build nextflow-erp.sln` sem erros nem warnings novos.

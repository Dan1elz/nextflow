# Instruções para agentes de IA (nextflow — API)

Este é o repositório da API do Nextflow (.NET 10). A documentação do produto fica em `../nextflow-docs`.
Leia o `CLAUDE.md` (contexto e regras inegociáveis) e o `../nextflow-docs/AGENTS.md` antes de mudar regra de negócio.

> **Em transição (desde 28/10/2026):** a API está sendo reescrita no padrão do `atrena-api` (ADR 0010). O código atual da
> branch `task-17` ainda usa exceções, DataAnnotations e `SaveChanges` no repositório; ele é **referência de regra**, não de
> padrão. Código novo segue só os guias abaixo. `docs/PROJECT.md`, `docs/FILTERS.md` e `docs/AI_FORECAST_SPEC.md` descrevem
> o código antigo até cada módulo ser reescrito.

## Antes de mexer
1. Leia `docs/dotnet-development-guidelines.md` e `docs/C_SHARP_ARCHITECTURE_RULES.md`. Para entidade nova, `docs/DOMAIN_LEVELS.md`.
2. Identifique o módulo em `../nextflow-docs/docs/architecture/modules/` e leia `overview.md`, `business-rules.md` e `contract.md`.
   O que estiver marcado "rascunho / a validar" ainda não é regra fechada: na dúvida, pergunte.
3. Procure no código uma fatia pronta parecida e copie a forma. Padrão novo só com decisão registrada.
4. `features/` no docs é histórico, não regra atual.

## Regras deste repositório
- Trabalhe no branch atual. Não crie branch com nome próprio sem perguntar.
- Commit, push, pull, merge e qualquer comando em servidor (SSH, deploy na VM) só com aprovação do Daniel.
- Migrations: `dotnet ef migrations add <Nome> --project Nextflow.Infrastructure --startup-project Nextflow`; são aplicadas no startup.
- Enum alterado aqui precisa mudar também em `../nextflow-front/src/types/enums.ts`.
- Nunca copie valores do `.env`/`.env.example` para código, logs ou documentação (`JwtSettings:Key`, `Ai:ApiKey`, `STORAGE_*`).
- Mudou fluxo, contrato ou regra de negócio: atualize o módulo no docs (`business-rules.md`, `contract.md`).
- A chamada à IA (Gemini) custa cota: não gere análises em loop para testar; sem `AI_API_KEY` a previsão roda só com o cálculo.

## Mapa rápido
| Assunto | Onde |
|--|--|
| Regras de arquitetura (inquebráveis) | `docs/C_SHARP_ARCHITECTURE_RULES.md` |
| Como desenvolver, checklist | `docs/dotnet-development-guidelines.md` |
| Ordem das entidades (N1…N6) | `docs/DOMAIN_LEVELS.md` |
| Controllers, middleware | `Nextflow/` |
| Use cases, serviços de aplicação, motor de previsão, filtros | `Nextflow.Application/` |
| Modelos, DTOs, validators, enums, interfaces | `Nextflow.Domain/` |
| Banco, repositórios, UnitOfWork, storage, IA, jobs, seeders | `Nextflow.Infrastructure/` |
| Testes | `Nextflow.Tests/` |
| Subir localmente | `docs/local-setup.md` |

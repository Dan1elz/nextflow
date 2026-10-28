# Nextflow API — instruções para o Claude

API do ERP Nextflow (TCC). Padrão esperado: código de dev pleno em projeto real.
Responda e documente em português; código (nomes, classes, métodos) em inglês, como já está.

## Antes de mexer
- Mapa completo do projeto: `docs/PROJECT.md`. Leia a seção da camada que vai alterar.
- Módulo de previsão de demanda com IA: contrato back↔front em `docs/AI_FORECAST_SPEC.md`.
  Mudou DTO, rota ou regra desse módulo? Atualize a spec e avise que o front (`../nextflow-front`) precisa acompanhar.
- Filtros genéricos na query string: `docs/FILTERS.md`.
- Docker e ambientes: `docs/DOCKER.MD`.

## Stack e arquitetura
.NET 8, ASP.NET Core, EF Core + PostgreSQL (Npgsql), JWT com roles `User`/`Admin`.
Clean Architecture em 4 projetos (`nextflow-erp.sln`):

- `nextflow/` (API): controllers finos, middleware, DTOs de request com `IFormFile`. Sem regra de negócio.
- `nextflow.Application/`: um use case por operação em `UseCases/<Entidade>/`, motor de previsão em `Forecast/`, services.
- `nextflow.Domain/`: models, enums, DTOs, exceptions, interfaces. Não depende de nenhum outro projeto.
- `nextflow.Infrastructure/`: `AppDbContext`, repositórios (`BaseRepository<T>`), seeders, migrations.

Respeite a direção das dependências (API → Application → Domain; Infrastructure → Domain).

## Convenções
- Use cases e repositórios terminados em `UseCase`/`Repository` são registrados automaticamente pelo Scrutor (Scoped). Outros serviços precisam de registro manual em `Program.cs`.
- Erros de negócio: lance `BadRequestException`, `NotFoundException`, `NotAuthorizedException` ou `NextflowValidationException` (422, com erros por campo). O `GlobalExceptionMiddleware` converte em JSON. Não devolva `StatusCode` manual no controller para esses casos.
- Operações que gravam em mais de uma tabela usam `IUnitOfWork` (transação).
- Datas de negócio: `BusinessClock` (fuso `America/Sao_Paulo`). Persistência em UTC.
- Preço de venda: sempre via `IProductPricingService` (preço efetivo com promoção vigente).
- Enums têm espelho no front (`nextflow-front/src/types/enums.ts`): mudou aqui, mude lá.
- Nada de `catch` vazio ou exceção engolida; logue ou propague.
- Nunca logue segredos (`JwtSettings:Key`, `Ai:ApiKey`) nem commite `.env`.

## Comandos
```
dotnet build nextflow-erp.sln
dotnet run --project nextflow/Nextflow.csproj
dotnet ef migrations add <Nome> --project nextflow.Infrastructure --startup-project nextflow
```
As migrations são aplicadas no startup (`Database.Migrate()` em `Program.cs`), seguidas dos seeders.

## Testes
Ainda não existe projeto de testes. Ao criar, seguir: xUnit + NSubstitute para use cases e para o `ForecastCalculator`
(cálculo puro, prioridade), `WebApplicationFactory` + Testcontainers (PostgreSQL real) para integração.
Asserts com Shouldly ou AwesomeAssertions (FluentAssertions v8+ é pago).

## Ao terminar uma tarefa
- `dotnet build` sem erros nem warnings novos.
- Atualize `docs/PROJECT.md` (e a spec de IA, se for o caso) quando mudar estrutura, rota ou regra.

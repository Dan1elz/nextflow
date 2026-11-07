# Nextflow — Backend (API)

> **Última atualização:** 11/10/2026 (módulo Previsão de Demanda com IA — etapa 1: schema, promoções, preço efetivo, seed; etapa 2: motor de previsão + IA Gemini + `ForecastsController`; etapa 4: validação ponta a ponta com o frontend; contrato em `docs/AI_FORECAST_SPEC.md`)
> **Repositório:** `Dan1elz/nextflow`
> **Stack:** .NET 8 · C# · ASP.NET Core · PostgreSQL · Entity Framework Core

---

## 1. Visão Geral

O **Nextflow** é a API backend de um sistema ERP que gerencia clientes, fornecedores, produtos, estoque, pedidos de venda, ordens de compra e vendas. Segue **Clean Architecture** em 4 camadas.

---

## 2. Arquitetura de Camadas

```
nextflow-erp.sln
├── nextflow/                    → API Layer (Controllers, DTOs, Middlewares)
├── nextflow.Application/        → Application Layer (Use Cases, Filters, Utils)
├── nextflow.Domain/             → Domain Layer (Models, Interfaces, Enums, Exceptions)
└── nextflow.Infrastructure/     → Infrastructure Layer (Database, Repositories, Seeders, Migrations)
```

### Dependências entre camadas
- **API** → Application, Infrastructure
- **Application** → Domain, Infrastructure
- **Infrastructure** → Domain
- **Domain** → (nenhuma dependência)

---

## 3. Camada API (`nextflow/`)

### 3.1 Entry Point — `Program.cs`

Configurações registradas no startup:

| Configuração | Detalhe |
|--|--|
| **Database** | `AppDbContext` via `UseNpgsql()` com connection string `DefaultConnection` |
| **CORS** | `AllowAnyOrigin`, `AllowAnyHeader`, `AllowAnyMethod` |
| **Swagger** | OpenAPI v1 com suporte Bearer JWT, Newtonsoft.JSON |
| **Autenticação** | JWT Bearer (`JwtSettings:Key`), sem validação de Issuer/Audience |
| **DI (Scrutor)** | Scan automático de classes `*UseCase` e `*Repository` como `Scoped` |
| **Storage** | `IStorageService` → `S3StorageService` (Infrastructure, Singleton) via `AddStorage()`; seção `Storage`. Ver seção 10 |
| **Preço efetivo** | `IProductPricingService` → `ProductPricingService` (Scoped, registro manual) |
| **Health Check** | `/health` com `AddDbContextCheck<AppDbContext>` |
| **Seeders** | Toda classe `ISeeder` é registrada por Scrutor (Scoped) e o `SeederRunner` executa em ordem (`Order`) após `MigrateAsync()`. Falha de migration ou seeder derruba o startup (sem `catch` silencioso) |
| **Cultura** | `UseRequestLocalization` com cultura **invariante** em todas as requisições: o model binding de form/query (ex.: multipart de produtos com `Price=8.9`) não depende da cultura do servidor (em Windows pt-BR, "8.9" virava 89) |

### 3.2 Controllers (17)

| Controller | Rota base | Observações |
|--|--|--|
| `UsersController` | `/api/users` | Login, CheckAuth, CRUD + reativação |
| `ClientsController` | `/api/clients` | CRUD + reativação |
| `SuppliersController` | `/api/suppliers` | CRUD + reativação |
| `CategoriesController` | `/api/categories` | CRUD com soft delete |
| `ProductsController` | `/api/products` | CRUD + upload de imagem (FormData) |
| `StockMovementsController` | `/api/stock-movements` | Criação e listagem |
| `OrdersController` | `/api/orders` | CRUD + cancel + refund |
| `SalesController` | `/api/sales` | Criação via checkout |
| `PurchaseOrdersController` | `/api/purchaseorders` | CRUD. Ao mudar para `Received`: entrada de estoque, `ReceivedAt` e `Product.CostPrice` = custo do item |
| `PromotionsController` | `/api/promotions` | POST/DELETE (Admin; DELETE = cancelar), GET lista (filtros `productId`, `status`=active\|scheduled\|ended\|canceled, `source`) e GET por id |
| `ForecastsController` | `/api/forecasts` | Previsão de demanda (spec 7.1): `POST generate` (Admin), `GET latest` (`data` null sem análise), `GET` lista paginada, `GET {id}`, `GET products/{productId}?months=24`, `POST recommendations/purchase` (1 pedido Orçamento por fornecedor), `POST recommendations/{id}/promotion` (Admin), `POST recommendations/{id}/dismiss` (body opcional) |
| `DashboardController` | `/api/dashboard` | `GET stats` (alerta de estoque usa `MinStock`, fallback 15) |
| `AddressesController` | `/api/addresses` | CRUD + resolução de CEP |
| `ContactsController` | `/api/contacts` | CRUD |
| `CountriesController` | `/api/countries` | CRUD |
| `StatesController` | `/api/states` | CRUD |
| `CitiesController` | `/api/cities` | CRUD |

### 3.3 Middlewares

| Arquivo | Descrição |
|--|--|
| `GlobalExceptionMiddleware.cs` | Captura exceções e retorna JSON padronizado com `Status`, `Message`, `Errors` |

**Mapeamento de exceções:**
| Exceção | HTTP Status |
|--|--|
| `BadRequestException` | 400 |
| `NotAuthorizedException` | 401 |
| `NotFoundException` | 404 |
| `NextflowValidationException` | 422 (com campo `Errors`) |
| Outras | 500 |

### 3.4 Attributes

| Arquivo | Descrição |
|--|--|
| `RoleAuthorizeAttribute.cs` | Autorização por role do JWT |

### 3.5 Utils (API Layer)

| Arquivo | Descrição |
|--|--|
| `FilterHelper.cs` | Extrai filtros JSON da query string |
| `FormFileAdapter.cs` | Adapter para `IFormFile` → `IFileData` |
| `TokenHelper.cs` | Extrai userId do token JWT via claims |

### 3.6 DTOs (API Layer)

| Arquivo | Descrição |
|--|--|
| `ProductRequestDto.cs` | DTO de request para criação de produto |
| `UpdateProductRequestDto.cs` | DTO de request para atualização de produto (com imagem). Inclui `Brand`, `CostPrice`, `MinStock` |
| `PromotionRequestDto.cs` | Corpo do POST de promoção (`productId`, `discountPercent`, `startDate`, `endDate`, `reason`) — origem/usuário definidos no servidor |

---

## 4. Camada Domain (`nextflow.Domain/`)

### 4.1 Models Base

| Classe | Campos | Descrição |
|--|--|--|
| `BaseModel` | `Id` (Guid, PK), `CreateAt` (DateTime UTC) | Entidade base. Define `Preposition`, `Singular`, `Plural` (virtual) |
| `Person` | `Name`, `LastName`, `CPF`, `BirthDate`, `UpdateAt`, `IsActive` | Herda `BaseModel` + `IDeletable`. Base para User/Client |

### 4.2 Models de Domínio (24)

| Model | Herda | Campos principais |
|--|--|--|
| `User` | `Person` | `Email`, `Password`, `Role` |
| `Client` | `Person` | (campos herdados de Person) |
| `Supplier` | `BaseModel` | `Name`, `CNPJ`, `LeadTimeDays` (1–180, padrão 7), `IsActive` |
| `Category` | `BaseModel` | `Name`, `Description`, `IsActive` |
| `CategoryProduct` | — | `CategoryId`, `ProductId` (N:N) |
| `Product` | `BaseModel` | `Name`, `ProductCode`, `Price`, `Quantity`, `UnitType`, `Image`, `Brand`, `CostPrice` (último custo), `MinStock`, `Promotions`, etc. |
| `StockMovement` | `BaseModel` | `ProductId`, `Quantity`, `Type`, `Reason`, `UserId` |
| `Order` | `BaseModel` | `ClientId`, `UserId`, `Type`, `Status`, `TotalAmount`, `LossReason` |
| `OrderItem` | `BaseModel` | `OrderId`, `ProductId`, `Quantity`, `UnitPrice`, `Discount` |
| `Sale` | `BaseModel` | `OrderId`, `TotalAmount`, `UserId` |
| `Payment` | `BaseModel` | `SaleId`, `PaymentMethod`, `Amount` |
| `PurchaseOrder` | `BaseModel` | `SupplierId`, `Status`, `TotalAmount`, `Note`, `ReceivedAt` (UTC, lead time real) |
| `PurchaseItem` | `BaseModel` | `PurchaseOrderId`, `ProductId`, `Quantity`, `UnitCost` |
| `Address` | `BaseModel` | `Street`, `Number`, `Complement`, `Neighborhood`, `ZipCode`, `CityId`, `OwnerId`, `OwnerType` |
| `Contact` | `BaseModel` | `Type`, `Value`, `OwnerId`, `OwnerType` |
| `Country` | `BaseModel` | `Name`, `Code`, `PhoneCode` |
| `State` | `BaseModel` | `Name`, `Abbreviation`, `CountryId` |
| `City` | `BaseModel` | `Name`, `StateId`, `IbgeCode` |
| `Promotion` | `BaseModel` + `IDeletable` | `ProductId`, `DiscountPercent` (1–90), `StartDate`/`EndDate` (`DateOnly`), `Reason`, `Source`, `ForecastRecommendationId`, `CreatedByUserId`, `IsActive` (false = cancelada) |
| `ProductSalesMonthly` | `BaseModel` | `ProductId`, `Year`, `Month`, `QuantitySold`, `Revenue`, `OrdersCount`, `UpdatedAt` — único `(ProductId, Year, Month)` |
| `ProductSeasonality` | `BaseModel` | `ProductId`, `Month`, `SeasonalIndex`, `MonthsOfHistory`, `UpdatedAt` — único `(ProductId, Month)` |
| `ForecastRun` | `BaseModel` | `RequestedByUserId`, `HorizonMonths`, `Status`, `AiProvider`, `AiModel`, `Summary`, `ErrorMessage`, `InputSnapshotJson`, `DurationMs`, `ProductsAnalyzed` |
| `ForecastRecommendation` | `BaseModel` | `ForecastRunId`, `ProductId`, `Action`, `Priority`, `Confidence`, `Status`, sugestões, snapshot de estoque/demanda, `PurchaseOrderId`/`PromotionId` (FK, SetNull) |
| `ForecastProjection` | `BaseModel` | `ForecastRunId`, `ProductId`, `Year`, `Month`, `ProjectedQuantity` |

### 4.3 Enums (13)

| Enum | Valores |
|--|--|
| `RoleEnum` | `User = 1`, `Admin = 2` |
| `OrderType` | `Budget = 1`, `Sale = 2` |
| `OrderStatus` | `Budget = 1`, `PendingPayment = 2`, `PaymentConfirmed = 3`, `Canceled = 4`, `Refunded = 5` |
| `PaymentMethod` | `Cash = 1`, `CreditCard = 2`, `DebitCard = 3`, `BankTransfer = 4`, `Pix = 5`, `Ticket = 6` |
| `MovementType` | `Entry = 1`, `Exit = 2`, `Adjustment = 3`, `Sales = 4`, `Return = 5` |
| `PurchaseStatus` | `Budget = 1`, `Pending = 2`, `Received = 3`, `Canceled = 4` |
| `UnitType` | `Unit = 1`, `Kilogram = 2`, `Liter = 3`, `Meter = 4` |
| `PromotionSource` | `Manual = 1`, `AiRecommendation = 2` |
| `ForecastRunStatus` | `Completed = 1`, `CompletedWithoutAi = 2`, `Failed = 3` |
| `RecommendationAction` | `Buy = 1`, `Promote = 2`, `Hold = 3` |
| `RecommendationPriority` | `Low = 1`, `Medium = 2`, `High = 3` |
| `ForecastConfidence` | `Low = 1`, `Medium = 2`, `High = 3` |
| `RecommendationStatus` | `Pending = 1`, `Applied = 2`, `Dismissed = 3`, `Expired = 4` |

### 4.4 Exceptions (4)

| Classe | Uso |
|--|--|
| `BadRequestException` | Dados inválidos (400) |
| `NotAuthorizedException` | Acesso negado (401) |
| `NotFoundException` | Recurso não encontrado (404) |
| `NextflowValidationException` | Erro de validação com dicionário de erros por campo (422) |

### 4.5 Attributes de Validação (3)

| Classe | Uso |
|--|--|
| `AnoValidoAttribute` | Valida ano dentro de range aceitável |
| `CpfCnpjAttribute` | Valida CPF ou CNPJ |
| `NotEmptyGuidAttribute` | Valida que o Guid não é `Guid.Empty` |

### 4.6 Interfaces

**Models:** `IDeletable`, `IEntityMetadata`, `IUpdatable`
**Utils:** `IFileData`, `IStorageService`, `IProductPricingService` (preço efetivo com promoção vigente), `IAiClient` (IA generativa: `IsConfigured`, `ProviderName`, `Model`, `GenerateJsonAsync`)
**Repositories:** `IBaseRepository<T>` + 24 interfaces específicas (novas: `IPromotionRepository`, `IProductSalesMonthlyRepository` com `DeleteAllAsync`/`AggregateConfirmedSalesAsync`, `IProductSeasonalityRepository`, `IForecastRunRepository`, `IForecastRecommendationRepository`, `IForecastProjectionRepository`)
**UseCases:** `IBaseUseCase` (em `Base/`) + interfaces específicas (`ICategoryUseCase`, `IOrderUseCase`, `IProductImageUseCase`, `IPurchaseOrderUseCase`, `IResolveAddressFromCepUseCase`, `ISaleUseCase`, `IStockMovementUseCase`, `IUserUseCase`, `IPromotionUseCase` → `ICreatePromotionUseCase`, `IForecastUseCase` → rebuild/generate/latest/product/apply/dismiss). `IUnitOfWork` (transação sobre o contexto do request; impl. `Infrastructure/Database/UnitOfWork.cs`). `IPurchaseOrderRepository` ganhou `GetIncomingStockByProductAsync` e `GetReceivedLeadTimeSamplesAsync`.

### 4.7 DTOs (Domain)

Cada arquivo contém DTOs de Request e Response para a entidade correspondente. Arquivo `SharedDto.cs` contém DTOs compartilhados (`ApiResponseMessage`).
Novos: `PromotionsDto.cs` (`CreatePromotionDto`, `PromotionResponseDto`, `PromotionSummaryDto`) e `ForecastsDto.cs` (`CreateForecastRecommendationDto`, `ForecastRunSummaryDto`/`ForecastRunDto`, `ForecastRecommendationDto`, `ProductForecastDto`, `ApplyPurchase*`, `ApplyPromotionRequestDto`, `GenerateForecastRequestDto` e DTOs de agregação dos repositórios).
`ProductResponseDto` expõe `Brand`, `CostPrice`, `MinStock`, `EffectivePrice` e `ActivePromotion` (calculados a partir das promoções vigentes carregadas; sem elas, `EffectivePrice = Price`). `UserResponseDto` expõe `Role`.

### 4.8 Utils (Domain)

| Arquivo | Descrição |
|--|--|
| `BusinessClock.cs` | `Today`/`Now` no fuso `America/Sao_Paulo`; conversões UTC ↔ data de negócio |
| `CommercialCalendar.cs` | Datas comerciais por ano: Páscoa (Meeus/Jones/Butcher), Dia das Mães, Namorados, Pais, Crianças, Black Friday, Natal; `GetEventsBetween` |

---

## 5. Camada Application (`nextflow.Application/`)

### 5.1 UseCases (18 pastas)

Cada pasta contém use cases CRUD (Create, GetAll, GetById, Update, Delete) + operações específicas.

`Addresses`, `Base`, `Categories`, `Cities`, `Clients`, `Contacts`, `Countries`, `Dashboard`, `Forecasts`, `Orders`, `Products`, `Promotions`, `PurchaseOrders`, `Sales`, `States`, `StockMovements`, `Suppliers`, `Users`

**Promoções:** criação valida período (`EndDate ≥ StartDate`, máx. 90 dias contando início e término, `StartDate ≥ hoje`), sobreposição com outra promoção ativa do produto e produto ativo. "Vigente" = `IsActive && StartDate ≤ hoje ≤ EndDate`.
**Pedidos de venda:** `CreateOrderUseCase`/`UpdateOrderUseCase` usam o preço efetivo (`IProductPricingService`) em `SetPricing`. `GetOrderByIdUseCase` inclui as promoções vigentes dos produtos dos itens (o formulário de edição usa `product.effectivePrice`, o mesmo preço que o update cobra).
**Previsão (`UseCases/Forecasts`):** `GenerateForecastUseCase` (rebuild de `product_sales_monthly` → `ForecastCalculator` → IA → guardrails → persiste run/recomendações/projeções + upsert de `product_seasonality` → expira pendentes anteriores; falha da IA = `CompletedWithoutAi`, falha do cálculo = `Failed`), `RebuildProductSalesMonthlyUseCase` (36 meses, GROUP BY no banco com `AT TIME ZONE 'America/Sao_Paulo'`), leitura (latest, lista, por id, por produto) e ações (compra agrupada por fornecedor via `ICreateUseCase<CreatePurchaseOrderDto,…>`, promoção via `ICreatePromotionUseCase`, descarte), com transação (`IUnitOfWork`).

### 5.1.1 Motor de previsão (`Forecast/`)

| Arquivo | Descrição |
|--|--|
| `ForecastCalculator.cs` | Cálculo puro (spec seção 5 + Regra 5b): índice sazonal produto/categoria, nível dessazonalizado, projeção, cobertura, necessidade de compra, data-limite, promoção/desconto, prioridade, confiança |
| `ForecastModels.cs` | `YearMonth`, `ForecastCalculationContext`, `ForecastProductInput`, `ProductForecastResult`, formatação pt-BR |
| `ForecastReasonTemplates.cs` | Motivos e resumo pt-BR por template (modo sem IA) |
| `ForecastAiPrompt.cs` | System prompt, payload JSON (spec 6.3, máx. 40 produtos), `responseSchema` (OpenAPI-subset do Gemini) e leitura tolerante da resposta |
| `ForecastGuardrails.cs` | Guardrails da spec 6.5 (+ Promover só se o cálculo permitir; Manter não anula compra de prioridade Alta) |

Categoria de referência: primeira categoria ativa do produto em ordem alfabética, ignorando a categoria "guarda-chuva" (contém ≥ 80% dos produtos com vendas, havendo ≥ 5) quando o produto tem outra. Lead time: média `ReceivedAt − CreateAt` dos últimos 5 pedidos recebidos do fornecedor (fallback `Supplier.LeadTimeDays`).

### 5.2 Filters

| Arquivo | Descrição |
|--|--|
| `FilterExpressionBuilder.cs` | Constrói expressões LINQ dinâmicas a partir do JSON de filtros |
| `FilterSet.cs` | Define o conjunto de filtros suportados |
| `FilterValueParsers.cs` | Parsers tolerantes para bool, números, datas |

**Padrão de filtros:** JSON na query string como `?filters={"field":"value"}`. Case-insensitive, aceita múltiplos formatos de data e número.

### 5.3 Utils (Application Layer)

| Arquivo | Descrição |
|--|--|
| `ExpressionExtensions.cs` | Extensions para composição de expressões LINQ |
| `JwtUtils.cs` | Geração e validação de tokens JWT |
| `ImageFileValidator.cs` | Regras de upload de imagem (JPG/PNG/WebP, até 5 MB, confere a assinatura dos primeiros bytes) |
| `ProductQueryExtensions.cs` | `IncludeCurrentPromotions(date)` — inclui só as promoções vigentes do produto |

### 5.4 Services

| Arquivo | Descrição |
|--|--|
| `Services/ProductPricingService.cs` | Implementa `IProductPricingService` (`GetEffectivePrice`, `GetActivePromotion`) |
| `Services/Ai/GeminiAiClient.cs` | Implementa `IAiClient` (REST v1beta `generateContent`, header `x-goog-api-key`, `systemInstruction`, `responseMimeType=application/json` + `responseSchema`, temperatura 0.2, timeout `Ai:TimeoutSeconds`, até 3 tentativas em 429/500/503). Registrado com `AddHttpClient<IAiClient, GeminiAiClient>()`. A chave nunca é logada |

---

## 6. Camada Infrastructure (`nextflow.Infrastructure/`)

### 6.1 Database

**`AppDbContext.cs`** — 24 `DbSet<>` mapeados. Constraints de unicidade:
- `User.Email` (unique)
- `User.CPF` (unique)
- `Client.CPF` (unique)
- `Supplier.CNPJ` (unique)
- `ProductSalesMonthly (ProductId, Year, Month)` (unique)
- `ProductSeasonality (ProductId, Month)` (unique)

Migration do módulo de previsão: `AddAiForecastModule` (colunas novas com default `MinStock = 0`, `LeadTimeDays = 7`; preenche `ReceivedAt` de pedidos já recebidos com `UpdateAt`).

> ⚠️ **Nota dev:** `LogTo(Console.WriteLine, LogLevel.Warning)` e `EnableSensitiveDataLogging()` estão ativados (comentário "tirar depois").

### 6.2 BaseRepository

`BaseRepository<TEntity>` — Genérico com métodos:
`AddAsync`, `ExistsAsync`, `AddRangeAsync`, `GetAllAsync` (com paginação offset/limit + `OrderByDescending(CreateAt)`), `GetAsync`, `GetByIdAsync`, `CountAsync`, `RemoveAsync`, `RemoveRangeAsync`, `UpdateAsync`, `UpdateRangeAsync`, `SaveAsync`

### 6.3 Repositories — 24 + 1 (Base)

Herdam `BaseRepository<T>`. Repositório `UserRepository` tem lógica extra (busca por email); `ProductSalesMonthlyRepository.DeleteAllAsync` usa `ExecuteDeleteAsync` (rebuild da agregação).

### 6.4 Seeders

Implementam `ISeeder` (`Order` + `SeedAsync`), recebem dependências por DI e são idempotentes. Sempre: `UsersSeeder` (10), `CountriesSeeder` (20), `CitiesSeeder` (30, ~770KB de dados). Só com `Seed:Demo:Enabled`: `DemoStoreSeeder` (70) e `ProductImagesSeeder` (80), em `Seeders/Demo/`. Novo seeder = nova classe `ISeeder` com `Order`; não precisa mexer no `Program.cs`.

**Dados de demonstração** (loja de doces e presentes, 48 SKUs, 36 meses, ~23 mil pedidos, estoque = soma das movimentações): ver `docs/AI_FORECAST_SPEC.md`, seção 8. Flags `Seed:Demo:Enabled` (padrão `false`; `true` no Development e nos compose) e `Seed:Demo:AnchorDate` (vazio = hoje).

---

## 7. Configuração

### `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Port=5432;Database=nextflow_db;..."
  },
  "JwtSettings": { "Key": "" },
  "Storage": {
    "Endpoint": "http://localhost:9000", "PublicUrl": "http://localhost:9000",
    "AccessKey": "", "SecretKey": "", "Region": "us-east-1",
    "Buckets": { "Public": "nextflow-public", "Private": "nextflow-private" }
  },
  "Ai": { "Provider": "Gemini", "ApiKey": "", "Model": "gemini-flash-latest", "TimeoutSeconds": 60 }
}
```

### `.env.example`
```
POSTGRES_DB=nextflow_db
POSTGRES_USER=nextflow_user
POSTGRES_PASSWORD=sua_senha_secreta_aqui_123
POSTGRES_PORT=5432
ASPNETCORE_ENVIRONMENT=Development
BACKEND_PORT=8080
JWT_KEY=uma_senha_super_secreta_e_longa_para_o_token_!@#
AI_PROVIDER=Gemini
AI_API_KEY=            # vazio = modo sem IA (CompletedWithoutAi)
AI_MODEL=gemini-flash-latest
AI_TIMEOUT_SECONDS=60
STORAGE_ROOT_USER=nextflow-admin       # admin do storage (painel em :9001)
STORAGE_ROOT_PASSWORD=nextflow-admin-secret
STORAGE_ACCESS_KEY=nextflow            # usuário da API (criado pelo storage/init.sh)
STORAGE_SECRET_KEY=nextflow-secret
STORAGE_BUCKET_PUBLIC=nextflow-public
STORAGE_BUCKET_PRIVATE=nextflow-private
STORAGE_PUBLIC_URL=http://localhost:9000
```

Seção `Ai` (`Ai__Provider`, `Ai__ApiKey`, `Ai__Model`, `Ai__TimeoutSeconds`) é repassada pelos compose files a partir das variáveis `AI_*`.

Seção `Storage` (`Storage__Endpoint`, `Storage__PublicUrl`, `Storage__AccessKey`, `Storage__SecretKey`, `Storage__Buckets__Public`, `Storage__Buckets__Private`) é repassada pelos compose files a partir das variáveis `STORAGE_*`. As credenciais de dev ficam em `appsettings.Development.json` (para `dotnet run` fora do Docker).

---

## 8. Docker

- **`Dockerfile`** — Build de produção/homologação
- **`Dockerfile.development`** — Dev com hot reload
- **`Dockerfile.postgres`** — Imagem do PostgreSQL
- **`compose.development.yaml`** — Backend + Postgres + Storage (Silo) + `storage-initial-setup` em rede `nextflow-development-network`
- **`compose.staging.yaml`** — Backend + Postgres + Storage (Silo) + `storage-initial-setup` em rede `nextflow-staging-network` (storage na `proxy-net` com alias `nextflow-s3`)
- **`storage/init.sh`** — Idempotente: cria os buckets, libera leitura anônima só em `products/` e cria o usuário da API com política restrita aos dois buckets

---

## 9. Git

**Branch principal:** `main`
**Branches remotas:** task-01 a task-13, task-docker
**Total de commits:** ~50+

### Evolução (mais antigo → mais recente)
1. Modelos de domínio fundacionais, interfaces e CRUD base
2. Produtos + movimentação de estoque
3. Pedidos de venda (Order, OrderItem, Sale, Payment)
4. Configuração Docker (Dockerfile, compose dev/staging)
5. Autenticação JWT (Login, CheckAuth)
6. Sistema de filtros genérico (JSON na query string)
7. Validação de clientes, reativação, endereços
8. Upload de imagens de produtos
9. **Mais recente:** Ordens de compra (PurchaseOrder)

---

## 10. Storage de arquivos (S3)

Arquivos ficam num storage compatível com S3, não em disco. Produto: **Silo** (`pgsty/silo` + `pgsty/mc`), fork comunitário do MinIO (o MinIO parou de publicar imagens em out/2025). O serviço se chama `storage` (papel, não produto): a API só fala S3 com `http://storage:9000`, então trocar de produto mexe só nos compose files.

| Peça | Onde |
|--|--|
| Contrato | `Domain/Interfaces/Utils/IStorageService.cs` (`UploadAsync`, `ExistsAsync`, `DeleteAsync`, `GetPublicUrl`, `GetSignedUrl`) |
| Onde cada arquivo mora | `Domain/Utils/StorageFolder.cs` (bucket + prefixo). Hoje: `StorageFolder.ProductImages` = bucket público, prefixo `products/` |
| Buckets | `Domain/Enums/StorageBucket.cs`: `Public` (leitura anônima por prefixo) e `Private` (só URL assinada) |
| Implementação | `Infrastructure/Storage/S3StorageService.cs` (AWSSDK.S3, `ForcePathStyle`). Dois clientes: um interno (`Endpoint`) e um que só assina URLs com o endereço público (`PublicUrl`), porque a assinatura inclui o host |
| Configuração | `Infrastructure/Storage/StorageSettings.cs` (seção `Storage`), validada no startup |
| Buckets, acesso e usuário da API | `storage/init.sh`, executado pelo `storage-initial-setup` a cada `up` |

**Regras:**
- O banco guarda só a **chave** (ex.: `products/3f2a….png`), nunca a URL. A URL é montada na leitura (`GetPublicUrl`), então trocar de domínio não exige migrar dados.
- Validação do arquivo é do use case (`ImageFileValidator`), não do storage. O `Content-Type` gravado vem da extensão validada.
- Consistência banco × storage: na troca de imagem, sobe a nova, grava no banco e só então apaga a antiga; se o banco falhar, a nova é apagada. Na remoção, grava no banco antes de apagar o arquivo. Falha ao apagar arquivo antigo só gera `LogWarning` (sobra um arquivo, a operação do usuário não é desfeita).
- A API usa um usuário próprio (`STORAGE_ACCESS_KEY`) com política só nos dois buckets, não o admin.

**Novo caso de uso** (ex.: anexos de pedido de compra): criar um `StorageFolder` (ex.: `new(StorageBucket.Private, "purchase-orders")`), validar o arquivo no use case e, se for privado, devolver `GetSignedUrl(...)` com validade curta. Prefixo público novo também precisa de uma linha `mc anonymous set download` no `storage/init.sh`.

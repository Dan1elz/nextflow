# Nextflow — Backend (API)

> **Última atualização:** 30/04/2026
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
| **Storage** | `IStorageService` → `LocalStorageService` |
| **Health Check** | `/health` com `AddDbContextCheck<AppDbContext>` |
| **Seeders** | Executam em startup: `UsersSeeder`, `CountriesSeeder`, `CitiesSeeder`, `SuppliersSeeder`, `CategoriesSeeder`, `ProductsSeeder` |
| **Static Files** | Assets servidos em `/assets` (imagens de produtos) |

### 3.2 Controllers (14)

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
| `PurchaseOrdersController` | `/api/purchase-orders` | CRUD |
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
| `UpdateProductRequestDto.cs` | DTO de request para atualização de produto (com imagem) |

---

## 4. Camada Domain (`nextflow.Domain/`)

### 4.1 Models Base

| Classe | Campos | Descrição |
|--|--|--|
| `BaseModel` | `Id` (Guid, PK), `CreateAt` (DateTime UTC) | Entidade base. Define `Preposition`, `Singular`, `Plural` (virtual) |
| `Person` | `Name`, `LastName`, `CPF`, `BirthDate`, `UpdateAt`, `IsActive` | Herda `BaseModel` + `IDeletable`. Base para User/Client |

### 4.2 Models de Domínio (18)

| Model | Herda | Campos principais |
|--|--|--|
| `User` | `Person` | `Email`, `Password`, `Role` |
| `Client` | `Person` | (campos herdados de Person) |
| `Supplier` | `BaseModel` | `Name`, `CNPJ`, `IsActive` |
| `Category` | `BaseModel` | `Name`, `Description`, `IsActive` |
| `CategoryProduct` | — | `CategoryId`, `ProductId` (N:N) |
| `Product` | `BaseModel` | `Name`, `ProductCode`, `Price`, `CostPrice`, `Quantity`, `UnitType`, `Image`, etc. |
| `StockMovement` | `BaseModel` | `ProductId`, `Quantity`, `Type`, `Reason`, `UserId` |
| `Order` | `BaseModel` | `ClientId`, `UserId`, `Type`, `Status`, `TotalAmount`, `LossReason` |
| `OrderItem` | `BaseModel` | `OrderId`, `ProductId`, `Quantity`, `UnitPrice`, `Discount` |
| `Sale` | `BaseModel` | `OrderId`, `TotalAmount`, `UserId` |
| `Payment` | `BaseModel` | `SaleId`, `PaymentMethod`, `Amount` |
| `PurchaseOrder` | `BaseModel` | `SupplierId`, `Status`, `TotalAmount`, `UserId` |
| `PurchaseItem` | `BaseModel` | `PurchaseOrderId`, `ProductId`, `Quantity`, `UnitCost` |
| `Address` | `BaseModel` | `Street`, `Number`, `Complement`, `Neighborhood`, `ZipCode`, `CityId`, `OwnerId`, `OwnerType` |
| `Contact` | `BaseModel` | `Type`, `Value`, `OwnerId`, `OwnerType` |
| `Country` | `BaseModel` | `Name`, `Code`, `PhoneCode` |
| `State` | `BaseModel` | `Name`, `Abbreviation`, `CountryId` |
| `City` | `BaseModel` | `Name`, `StateId`, `IbgeCode` |

### 4.3 Enums (7)

| Enum | Valores |
|--|--|
| `RoleEnum` | `User = 1`, `Admin = 2` |
| `OrderType` | `Budget = 1`, `Sale = 2` |
| `OrderStatus` | `Budget = 1`, `PendingPayment = 2`, `PaymentConfirmed = 3`, `Canceled = 4`, `Refunded = 5` |
| `PaymentMethod` | `Cash = 1`, `CreditCard = 2`, `DebitCard = 3`, `BankTransfer = 4`, `Pix = 5`, `Ticket = 6` |
| `MovementType` | `Entry = 1`, `Exit = 2`, `Adjustment = 3`, `Sales = 4`, `Return = 5` |
| `PurchaseStatus` | `Budget = 1`, `Pending = 2`, `Received = 3`, `Canceled = 4` |
| `UnitType` | `Unit = 1`, `Kilogram = 2`, `Liter = 3`, `Meter = 4` |

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
**Utils:** `IFileData`, `IStorageService`
**Repositories:** `IBaseRepository<T>` + 18 interfaces específicas
**UseCases:** `IBaseUseCase` (em `Base/`) + 8 interfaces específicas (`ICategoryUseCase`, `IOrderUseCase`, `IProductImageUseCase`, `IPurchaseOrderUseCase`, `IResolveAddressFromCepUseCase`, `ISaleUseCase`, `IStockMovementUseCase`, `IUserUseCase`)

### 4.7 DTOs (Domain) — 16 arquivos

Cada arquivo contém DTOs de Request e Response para a entidade correspondente. Arquivo `SharedDto.cs` contém DTOs compartilhados (`ApiResponseMessage`).

---

## 5. Camada Application (`nextflow.Application/`)

### 5.1 UseCases (15 pastas)

Cada pasta contém use cases CRUD (Create, GetAll, GetById, Update, Delete) + operações específicas.

`Addresses`, `Base`, `Categories`, `Cities`, `Clients`, `Contacts`, `Countries`, `Orders`, `Products`, `PurchaseOrders`, `Sales`, `States`, `StockMovements`, `Suppliers`, `Users`

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
| `LocalStorageService.cs` | Salva/remove arquivos localmente em `assets/` |

---

## 6. Camada Infrastructure (`nextflow.Infrastructure/`)

### 6.1 Database

**`AppDbContext.cs`** — 18 `DbSet<>` mapeados. Constraints de unicidade:
- `User.Email` (unique)
- `User.CPF` (unique)
- `Client.CPF` (unique)
- `Supplier.CNPJ` (unique)

> ⚠️ **Nota dev:** `LogTo(Console.WriteLine)` e `EnableSensitiveDataLogging()` estão ativados (comentário "tirar depois").

### 6.2 BaseRepository

`BaseRepository<TEntity>` — Genérico com métodos:
`AddAsync`, `ExistsAsync`, `AddRangeAsync`, `GetAllAsync` (com paginação offset/limit + `OrderByDescending(CreateAt)`), `GetAsync`, `GetByIdAsync`, `CountAsync`, `RemoveAsync`, `RemoveRangeAsync`, `UpdateAsync`, `UpdateRangeAsync`, `SaveAsync`

### 6.3 Repositories — 18 + 1 (Base)

Herdam `BaseRepository<T>`. Repositório `UserRepository` tem lógica extra (busca por email).

### 6.4 Seeders — 6

`UsersSeeder`, `CountriesSeeder`, `CitiesSeeder` (~770KB de dados), `SuppliersSeeder`, `CategoriesSeeder`, `ProductsSeeder`

---

## 7. Configuração

### `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Port=5432;Database=nextflow_db;..."
  },
  "JwtSettings": { "Key": "" },
  "Storage": { "BaseUrl": "http://localhost:8080" }
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
```

---

## 8. Docker

- **`Dockerfile`** — Build de produção/homologação
- **`Dockerfile.development`** — Dev com hot reload
- **`Dockerfile.postgres`** — Imagem do PostgreSQL
- **`compose.development.yaml`** — Backend + Postgres em rede `nextflow-development-network`
- **`compose.staging.yaml`** — Backend + Postgres em rede `nextflow-staging-network`

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

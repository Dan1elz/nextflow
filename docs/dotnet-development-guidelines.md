# Guia de desenvolvimento .NET (Nextflow API)

Padrão de desenvolvimento do backend .NET do **Nextflow** (`nextflow`). Vale para todo código C# novo neste repositório.

O Nextflow é um micro-ERP com inteligência de compras para comércios de 4 a 15 funcionários (TCC do Daniel, com Ricardo
e Henrique). O padrão deriva do `atrena-api`, que por sua vez deriva do template `act-api`.

> Leitura obrigatória: `CLAUDE.md`, `docs/C_SHARP_ARCHITECTURE_RULES.md`, `docs/DOMAIN_LEVELS.md` e, para regra de
> negócio, o módulo em `../nextflow-docs/docs/architecture/modules/<módulo>/` (`overview.md`, `business-rules.md`, `contract.md`).

## Stack do projeto

- **Runtime**: .NET 10 — https://dotnet.microsoft.com/download/dotnet/10.0
- **ORM**: EF Core 10 + Npgsql (consultas pesadas de leitura podem usar Dapper dentro do repositório)
- **Banco**: PostgreSQL (chaves UUID)
- **Autenticação**: JWT via `Microsoft.AspNetCore.Authentication.JwtBearer`
- **Validação**: FluentValidation 12 — https://docs.fluentvalidation.net
- **Hash de senha**: BCrypt.Net-Next
- **Injeção por convenção**: Scrutor — https://github.com/khellang/Scrutor
- **Storage**: S3 compatível (Silo) via `AWSSDK.S3`
- **IA**: Google Gemini atrás de `IAiClient` (opcional)
- **Logs**: Serilog (inicialização em dois estágios) — https://serilog.net
- **Serialização**: Newtonsoft.Json (`Microsoft.AspNetCore.Mvc.NewtonsoftJson`), camelCase
- **Documentação da API**: Swashbuckle (Swagger)
- **Testes**: xUnit, NSubstitute, Shouldly, Testcontainers (PostgreSQL)

---

## 1. Princípios

- **Reaproveitar vence criar.** Antes de qualquer estrutura nova, veja se existe abstração base que resolve
  (`UseCases/Base/`, `BaseRepository`, `IResultFactory`, `Filters`, `IStockLedger`). Nunca abra um padrão paralelo.
- Clareza vence esperteza: código óbvio para o próximo dev (ou agente) vale mais que abstração elegante.
- `dotnet build` sem warnings é o padrão; warning é sinal de problema.
- Toda operação de escrita é um **use case** dedicado que herda uma base ou implementa interface própria, nunca um
  `Service` genérico que acumula responsabilidade.
- Toda resposta que sai da Application é `ResultDto<T>`; nunca `ActionResult`, exceção de negócio ou HTTP manual no use case.
- Se uma decisão parece "só para hoje" e foge da arquitetura, **ela não entra**: pare e aponte o conflito com a documentação.
- A regra de negócio mora na API. O front valida para ajudar o usuário, mas não decide preço, saldo, total nem status.

## 2. Estrutura do projeto

Arquitetura em camadas (Domain / Application / Infrastructure / Presentation), com o `Domain` no centro guardando
contratos, modelos, DTOs e validators:

```
Nextflow.Domain/                 # núcleo, sem dependência das outras camadas
  Models/                        # entidades persistidas (herdam BaseModel)
  Dtos/                          # ResultDto<T>, PaginationDto<T>, UploadFileDto, Option
    <Recurso>/                   # XRequestDto, XResponseDto, XListItemDto…, um arquivo por DTO
  Enums/                         # OrderStatusEnum, StockMovementReasonEnum… (com GetApiName)
  ValueObjects/                  # TaxDocument (CPF/CNPJ)…
  Validators/                    # FluentValidation, um validator por DTO de entrada
  Interfaces/
    Domain/Models/               # IEntityMetadata, IDeletable, IUpdatable<T>, IVersioned
    Application/Services/        # IResultFactory<T>, IStockLedger, IProductPricingService, IBusinessClock
    Application/UseCases/        # interfaces de use cases fora do CRUD
    Application/UseCases/Base/   # ICreateUseCase, IUpdateUseCase, IGetAllUseCase…
    Infrastructure/Repositories/ # IBaseRepository<T>, IUnitOfWork, I<Entidade>Repository
    Infrastructure/Services/     # IFileStorage, IAiClient, IPasswordHasher, IForecastQueue

Nextflow.Application/            # orquestração das regras de negócio
  DependencyInjection.cs         # AddApplication(): Scrutor registra UseCases e Validators
  UseCases/
    Base/                        # CreateUseCaseBase, UpdateUseCaseBase, DeleteUseCaseBase,
                                 # ReactivateUseCaseBase, GetAllUseCaseBase, GetByIdUseCaseBase, PagingLimits
    Auth/ Users/ Registry/ Catalog/ Inventory/ Sales/ Purchasing/ Forecast/ Dashboard/ Notifications/
  Services/                      # ResultFactory<T>, StockLedger, ProductPricingService, BusinessClock
  Forecast/                      # motor de previsão (cálculo puro), guardrails, prompt e templates da IA
  Filters/                       # FilterSet, FilterExpressionBuilder, FilterValueParsers

Nextflow.Infrastructure/         # EF Core, repositórios, integrações externas
  DependencyInjection.cs         # AddInfrastructure()
  Database/                      # AppDbContext, Configurations/, Converters/
  Repositories/                  # BaseRepository<T>, repositórios concretos, UnitOfWork
  Migrations/  Seeders/  Storage/  Ai/  Jobs/

Nextflow/                        # camada de apresentação (Web API)
  Extensions/                    # AddPresentation()
  Controllers/                   # ApiController + controllers concretos
  Middlewares/                   # GlobalResultMiddleware
  Utils/                         # TokenHelper, FilterHelper
  Program.cs                     # só compõe: AddInfrastructure + AddApplication + AddPresentation

Nextflow.Tests/                  # Unit/ e Integration/, espelhando os módulos
```

**Regra crítica:** cada projeto registra os próprios serviços no seu `DependencyInjection.cs`; o `Program.cs` só compõe
`AddInfrastructure() + AddApplication() + AddPresentation()`. Nunca registre serviço de camada direto no `Program.cs`.

Os módulos de `UseCases/` seguem os módulos do produto (`../nextflow-docs/docs/architecture/modules/`):

| Pasta | Módulo no docs | Exemplos |
|--|--|--|
| `Auth/`, `Users/` | `access` | `LoginUseCase`, `CreateUserUseCase` |
| `Registry/` | `registry` | clientes, fornecedores, endereços, contatos, país/estado/cidade |
| `Catalog/` | `catalog-inventory` | produtos, variações, categorias, coleções, eventos |
| `Inventory/` | `catalog-inventory` | lojas, locais, ajustes, transferências, inventários, lotes |
| `Sales/` | `sales` | pedidos, pagamentos, reembolso, promoções |
| `Purchasing/` | `purchasing` | pedidos de compra, recebimento |
| `Forecast/`, `Dashboard/`, `Notifications/` | `demand-forecast` | análise, recomendações, painel, notificações |

## 3. Dependências entre camadas

- `Domain` não depende de nada: só define contratos, modelos, DTOs e validators.
- `Application` depende de `Domain`.
- `Infrastructure` depende de `Domain` (implementa repositórios e serviços externos).
- `Nextflow` (API) depende de `Application` e `Infrastructure` só para compor.
- **NUNCA** injete `AppDbContext` em use case, serviço de aplicação ou controller. Banco só por `IBaseRepository<T>` /
  `I<Entidade>Repository`, gravado pelo `IUnitOfWork`.

### O que conferir em cada camada antes de entregar

| Camada | Confira |
|--|--|
| Domain | modelo herda `BaseModel`, construtor privado + construtor com DTO, `private set`, coleções `= []`, metadados preenchidos; DTOs um por arquivo; validator para cada DTO de entrada; enum com `GetApiName()` |
| Application | use case herda a base certa; regra no ponto de extensão certo; `Include` só em `GetInclude()`; estoque só pelo `IStockLedger`; nenhum `throw` de negócio; um commit |
| Infrastructure | `IEntityTypeConfiguration` com tabela, índices e FKs; repositório sem `SaveChanges` e sem regra; migration revisada |
| API | controller herda `ApiController`, uma linha por action, `CancellationToken` repassado, `[Authorize]` na classe, `[AllowAnonymous]` justificado |

## 4. Convenções de nome

Seguindo as convenções oficiais da Microsoft
([identifier names](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names)):

| Elemento | Convenção | Exemplo |
|--|--|--|
| Classe, record, struct | PascalCase | `CreateProductUseCase` |
| Interface | PascalCase com `I` | `IProductRepository` |
| Método e propriedade públicos | PascalCase | `ValidateBusinessRules` |
| Parâmetro e variável local | camelCase | `variantId` |
| Campo privado | `_camelCase` | `_stockLedger` |
| Enum | `<Conceito>Enum` | `OrderStatusEnum.Paid` |
| DTO de entrada | `<Recurso>RequestDto` / `Update<Recurso>RequestDto` / `<Ação><Recurso>RequestDto` | `ProductRequestDto`, `PayOrderRequestDto` |
| DTO de saída | `<Recurso>ResponseDto`, `<Recurso>ListItemDto`, `<Recurso>OptionDto` | `ProductResponseDto` |
| Validator | `<Dto>Validator` | `ProductRequestDtoValidator` |
| Repositório | `<Entidade>Repository` | `StockBalanceRepository` |
| Use case | `<Ação><Entidade>UseCase` | `ReceivePurchaseOrderUseCase` |
| Teste | `Should_<resultado>_When_<condição>` | `Should_ReturnConflict_When_SkuAlreadyExists` |

- Métodos assíncronos de I/O terminam em `Async` (`AddAsync`); consultas seguem o contrato da base (`Get`, `GetById`, `Exists`, `Count`).
- **Identificadores em inglês**, textos para o usuário em português. Termos do domínio pela tabela do `CLAUDE.md`
  (Pedido→Order, Variação→ProductVariant, Local de estoque→StockLocation…). Nunca misture os dois idiomas num nome.

## 5. Tipos e modelagem de domínio

- Toda entidade persistida herda `BaseModel` (`Id` Guid, `CreatedAt` UTC, metadados de `IEntityMetadata`).
- Comportamentos recorrentes por interface: `IDeletable`, `IUpdatable<T>`, `IVersioned`, `IEntityMetadata`.
- Status são enums nomeados, nunca `string` mágica ou `int` cru.
- Value objects para dado com invariante de formato (`TaxDocument.ParseCpf`, `TaxDocument.ParseCnpj`).
- Dinheiro e quantidade são `decimal`; quantidade respeita `DecimalPlaces` do produto.
- `nullable reference types` ligado; nada de `dynamic`.

```csharp
public abstract class BaseModel : IEntityMetadata
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public abstract string Preposition { get; }
    public abstract string Singular { get; }
    public abstract string Plural { get; }
}
```

## 6. Respostas padronizadas — `ResultDto<T>` + `IResultFactory<T>`

```csharp
// Sucesso
return _resultFactory.Success(new OrderResponseDto(order));

// Falha de negócio com status explícito
return _resultFactory.Failure("O pedido só pode ser pago enquanto estiver em Orçamento.", 400);

// Falha de validação por campo (a base já faz isso com o resultado do FluentValidation)
return _resultFactory.Failure(validationResult, 422);

// Aviso que pede confirmação (RN-VD13): 422 com FieldErrors["confirm"]
if (!dto.ConfirmWarnings && price < cost)
    return _resultFactory.Confirm("A promoção deixa o preço abaixo do custo médio. Deseja continuar?");
```

- `ResultDto<T>` fica em `Domain/Dtos/`. Nunca instancie à mão no use case; use a `IResultFactory<T>`.
- Listagens devolvem `ResultDto<PaginationDto<T>>`.
- No HTTP: sucesso devolve o `Data` puro; falha devolve `{ isSuccess, errors, fieldErrors, errorCode, message, data }`.

## 7. Use cases — bases + template methods

| Base | Fluxo |
|--|--|
| `CreateUseCaseBase<TEntity, TRepository, TRequest, TResponse>` | valida formato → regras de negócio → mapeia → `BeforePersistence` → persiste → commit → `AfterPersistence` |
| `UpdateUseCaseBase<…>` | busca rastreada → valida → regras → `UpdateEntity` → commit |
| `DeleteUseCaseBase<…>` | soft delete via `IDeletable` |
| `ReactivateUseCaseBase<…>` | desfaz o soft delete |
| `GetAllUseCaseBase<TEntity, TRepository, TResponse>` | listagem paginada com filtros, include e ordenação |
| `GetByIdUseCaseBase<…>` | busca por id |

Exemplo de escrita que mexe em estoque (venda de balcão paga na hora, RN-VD15):

```csharp
public class CreateCounterSaleUseCase(
    IOrderRepository orderRepository,
    IProductVariantRepository variantRepository,
    IUnitOfWork unitOfWork,
    IValidator<CounterSaleRequestDto> validator,
    IResultFactory<OrderResponseDto> resultFactory,
    IStockLedger stockLedger,
    IProductPricingService pricing,
    IOrderNumberGenerator numbers)
    : CreateUseCaseBase<Order, IOrderRepository, CounterSaleRequestDto, OrderResponseDto>(
        orderRepository, unitOfWork, validator, resultFactory)
{
    private IReadOnlyList<ProductVariant> _variants = [];

    protected override async Task<ResultDto<OrderResponseDto>?> ValidateBusinessRules(CounterSaleRequestDto dto, CancellationToken ct)
    {
        _variants = await variantRepository.GetForSale(dto.Items.Select(i => i.VariantId), ct);
        if (_variants.Count != dto.Items.Count)
            return _resultFactory.Failure("Há itens com produto inativo ou inexistente.", 404);

        var shortage = await stockLedger.FindShortage(dto.SaleLocationId, dto.Items, ct);
        return shortage is null ? null : _resultFactory.Failure($"Estoque insuficiente para {shortage}.", 400);
    }

    protected override Order MapToEntity(CounterSaleRequestDto dto)
        => Order.CounterSale(dto, _variants, pricing);   // preço efetivo e custo médio congelados no item

    protected override async Task BeforePersistence(Order entity, CounterSaleRequestDto dto, CancellationToken ct)
    {
        entity.AssignNumber(await numbers.Next(ct));
        await stockLedger.Register(StockMovementReasonEnum.Sale, entity, ct);  // mesmo commit
    }

    protected override OrderResponseDto MapToResponseDto(Order entity) => new(entity);
}
```

- A validação de formato é disparada **pela base**; nunca chame o validator num use case que herda base.
- A regra fica no ponto de extensão certo, nunca espalhada no Controller ou no `MapToEntity`.
- Use case que não cabe nas bases (`PayOrderUseCase`, `LoginUseCase`, `ApplyRecommendationUseCase`) implementa sua
  interface, valida o DTO, usa `IResultFactory<T>` e faz um commit pelo `IUnitOfWork`.

## 8. Validação — FluentValidation

- Presença, formato e coerência do DTO em `Domain/Validators/`, uma classe por DTO herdando `AbstractValidator<TDto>`.
- Registrados por convenção (Scrutor); não registre à mão.
- Regra que depende do banco (unicidade, FK existente, saldo) vai em `ValidateBusinessRules`, não no validator.
- Mensagens em português; o nome do campo vem do `PropertyName` em camelCase, para o front marcar o campo certo.
- As validações listadas no `contract.md` do módulo são a fonte: cada uma vira regra de validator (formato) ou de
  `ValidateBusinessRules` (negócio).

```csharp
public class ProductRequestDtoValidator : AbstractValidator<ProductRequestDto>
{
    public ProductRequestDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome do produto.");
        RuleFor(x => x.DecimalPlaces).InclusiveBetween(0, 3);
        RuleFor(x => x.Variants).NotEmpty().WithMessage("O produto precisa de ao menos uma variação.");
        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(x => x.Sku).NotEmpty();
            v.RuleFor(x => x.Price).GreaterThan(0).WithMessage("O preço deve ser maior que zero.");
        });
        RuleFor(x => x.ShelfLifeDays).GreaterThan(0).When(x => x.TracksLots);
        RuleFor(x => x.Suppliers).Must(s => s.Count(x => x.IsPreferred) <= 1)
            .WithMessage("Só um fornecedor pode ser o preferido.");
    }
}
```

## 9. Listagem, filtros e paginação

- Listagens usam `GetAllUseCaseBase` e devolvem `ResultDto<PaginationDto<TResponse>>`.
- O controller recebe `offset`, `limit`, `filters` (JSON na query), `sort` e `order`; o `FilterHelper.Parse(...)` converte
  os filtros em `IReadOnlyDictionary<string,string>`. A loja selecionada no front chega como filtro `storeId`.
- Filtros complexos com `FilterSet` + `FilterExpressionBuilder` no `BuildFilterExpression`.
- `GetInclude` e `GetOrderBy` são pontos de extensão; o padrão do repositório é `CreatedAt` decrescente.
- Listagem devolve `XListItemDto` enxuto quando o contrato da tela pede.

## 10. Repositórios + Unit of Work

- Acesso a dados começa no `BaseRepository<TEntity>` (leitura com `AsNoTracking()`).
- Repositório concreto herda a base e só ganha método quando a consulta não cabe nas abstrações.
- **Repositório não faz commit.** Só registra no `DbContext` (`AddAsync`, `Update`, `Remove`). Persistência só por
  `IUnitOfWork.CommitAsync(ct)`, coordenado pelo use case (as bases já fazem).

```csharp
public interface ISupplierRepository : IBaseRepository<Supplier>;

public class SupplierRepository(AppDbContext context) : BaseRepository<Supplier>(context), ISupplierRepository;
```

- Uma `IEntityTypeConfiguration<T>` por entidade em `Database/Configurations/`, nunca fluent API espalhada no `AppDbContext`.
- Índices explícitos para as consultas críticas: SKU e código de barras únicos, saldo por (variação, local, lote),
  movimentos por (variação, data), pedidos por (loja, status, data), histórico mensal por (produto, ano, mês).

## 11. Controllers — finos + `ApiController` + `Handle()`

- Todo controller herda `ApiController` (que herda `ControllerBase`).
- Só repassa o DTO ao use case e devolve por `Handle()`, `HandleCreated()` ou `HandleNoContent()`.
- Sem regra, sem `AppDbContext`, sem chamada a storage ou IA.
- Upload: o controller recebe `IFormFile` e monta um `UploadFileDto` (nome, tipo, tamanho, `Stream`); o use case nunca vê `IFormFile`.

```csharp
[Authorize]
[Route("api/suppliers")]
public class SuppliersController(
    ICreateUseCase<SupplierRequestDto, SupplierResponseDto> createSupplierUseCase,
    IGetAllUseCase<Supplier, SupplierListItemDto> getAllSuppliersUseCase,
    IDeleteUseCase<Supplier> deleteSupplierUseCase,
    IReactivateUseCase<Supplier> reactivateSupplierUseCase) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SupplierRequestDto dto, CancellationToken ct)
        => HandleCreated(await createSupplierUseCase.Execute(dto, ct));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null, CancellationToken ct = default)
        => Handle(await getAllSuppliersUseCase.Execute(offset, limit, FilterHelper.Parse(filters), sort, order, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleNoContent(await deleteSupplierUseCase.Execute(id, UserId, ct));

    [HttpPatch("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => HandleNoContent(await reactivateSupplierUseCase.Execute(id, UserId, ct));
}
```

## 12. Injeção de dependência (Scrutor)

- Use cases e validators registrados por convenção em `Nextflow.Application/DependencyInjection.cs`: basta seguir o nome
  (`*UseCase`, `AbstractValidator<>`).
- `IResultFactory<>` como genérico aberto (`typeof(IResultFactory<>) → typeof(ResultFactory<>)`).
- Serviços de aplicação (`IStockLedger`, `IProductPricingService`, `IBusinessClock`) registrados explicitamente no
  `AddApplication()`.
- Repositórios, `IUnitOfWork`, storage, IA e jobs no `Nextflow.Infrastructure/DependencyInjection.cs`.

```csharp
services.AddScoped(typeof(IResultFactory<>), typeof(ResultFactory<>));
services.AddValidatorsFromAssemblyContaining<ProductRequestDtoValidator>();

services.Scan(scan => scan
    .FromAssemblyOf<LoginUseCase>()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("UseCase")))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

## 13. Concorrência e assíncrono

- `async`/`await` de ponta a ponta; nunca `.Result` ou `.Wait()`.
- `CancellationToken ct` é o **último parâmetro** de todo método assíncrono e é repassado na cadeia inteira; nunca
  `CancellationToken.None` em código de produção (exceto no rollback e no job, que tem o próprio token).
- Documentos editáveis (pedido em orçamento, compra, transferência, inventário) implementam `IVersioned`; o update envia a
  `Version` lida e o conflito vira 409 pelo middleware.
- Saldo de estoque atualizado com controle de concorrência (`Version` em `stock_balances`), nunca "ler, somar e gravar"
  sem token.
- Guard clauses com retorno antecipado em vez de `if` aninhado.

## 14. Estoque, preço e datas (regras transversais)

- **Estoque é livro-razão** (ADR 0007): toda entrada e saída passa pelo `IStockLedger`, que grava o movimento (motivo,
  origem, custo, saldo após) e atualiza o saldo materializado no mesmo commit. Movimento não se edita nem se apaga.
- **Variação é a unidade** de estoque, venda, compra e promoção (ADR 0008). Previsão é por produto, rateada pela grade.
- **Saldo por variação × local × lote** (ADR 0009). Venda informa o local de baixa; compra informa o local de entrega.
- **Custo médio** atualizado na entrada de compra (RN-CE31); o item de venda congela o custo médio do momento.
- **Preço efetivo** só pelo `IProductPricingService`.
- **Datas de negócio** pelo `IBusinessClock` (`America/Sao_Paulo`); banco em UTC; `DateOnly` para datas sem hora.
- **Números visíveis** (pedido, compra, transferência, inventário) por sequência no banco (L-CP8).

## 15. Integrações externas e segundo plano

- Integrações isoladas em `Infrastructure` atrás de interface (`IFileStorage`, `IAiClient`), nunca chamadas pelo controller.
- IA (ADR 0005): o backend calcula, a IA só prioriza e explica. Sem `Ai:ApiKey`, a análise roda só com o cálculo e
  termina `CompletedWithoutAi`. Nunca gere análises em loop para testar: cada chamada consome cota.
- Análise de previsão em segundo plano (RN-DF23): o use case cria a análise em `Running`, enfileira em `IForecastQueue` e
  devolve; o `BackgroundService` processa os lotes, grava e cria as notificações.
- Chamadas externas com tempo limite e log do que falhou, sem dado sensível.

## 16. Regras de negócio

A fonte de verdade é `../nextflow-docs/docs/architecture/modules/<módulo>/business-rules.md` (IDs `RN-*`), as decisões em
`decisions/` e o contrato de telas em `contract.md`. Regra marcada "rascunho / a validar" ainda não é fechada: na dúvida,
pergunte ao Daniel.

Se uma feature conflita com a documentação, **pare e aponte o conflito**; não mude a regra em silêncio. Mudou regra,
fluxo ou contrato: atualize o módulo no docs no mesmo trabalho.

## 17. Banco e migrations

```bash
dotnet ef migrations add <Nome> --project Nextflow.Infrastructure --startup-project Nextflow
dotnet ef database update --project Nextflow.Infrastructure --startup-project Nextflow
```

- PostgreSQL com PK UUID. As migrations são donas do schema e rodam no startup, seguidas dos seeders, protegidas por
  try/catch com log fatal (nunca engolir exceção de startup).
- Configuração por entidade em `Database/Configurations/`. Tabelas e colunas em `snake_case`.
- Entidade nova entra na ordem de `docs/DOMAIN_LEVELS.md`.

## 18. Segurança

- JWT validado em todo endpoint, exceto os com `[AllowAnonymous]` justificado (login).
- Perfil único Admin por enquanto (ADR 0004); autorização por perfil validada na camada certa quando houver outros perfis.
- Senha via `IPasswordHasher` (BCrypt), mínimo 8 com letra e número; nunca em texto puro nem em log.
- Toda consulta parametrizada (EF ou Dapper com parâmetros); **nunca** SQL concatenado com entrada do usuário.
- Segredos (`ConnectionStrings`, `JwtSettings:Key`, `Ai:ApiKey`, `STORAGE_*`) vêm de variável de ambiente; nunca no
  código, em log ou na documentação.

## 19. Logs e observabilidade

- Serilog em **dois estágios** (`CreateBootstrapLogger()` no bootstrap, configuração completa por `ReadFrom.Configuration`).
- `UseSerilogRequestLogging()` para as requisições.
- Log estruturado com propriedades nomeadas (`"Order {OrderId} paid"`), nunca interpolação.
- Nunca logue senha, token, chave de API ou CPF/CNPJ completo.

## 20. Faça / não faça

- **Um use case por operação**, herdando base, nunca um `Service` genérico.
- **Repositório herda `BaseRepository<T>`**; nada de `DbContext` fora da Infrastructure.
- **Regra no ponto de extensão** (`ValidateBusinessRules` / `BeforePersistence` / `AfterPersistence`), nunca no controller.
- **Resposta sempre `ResultDto<T>` via `IResultFactory<T>`.**

```csharp
// Ruim: regra + banco + HTTP no controller
[HttpPost("{id:guid}/pay")]
public async Task<IActionResult> Pay(Guid id, PayOrderRequestDto dto)
{
    var order = await _db.Orders.FindAsync(id);          // ❌ DbContext no controller
    if (order.Status != OrderStatus.Budget) return BadRequest();   // ❌ regra + HTTP manual
    order.Status = OrderStatus.Paid;                     // ❌ set público
    product.Quantity -= item.Quantity;                   // ❌ estoque fora do livro-razão
    await _db.SaveChangesAsync();
}

// Bom: controller fino, regra no use case, estoque pelo IStockLedger, um commit
[HttpPost("{id:guid}/pay")]
public async Task<IActionResult> Pay(Guid id, [FromBody] PayOrderRequestDto dto, CancellationToken ct)
    => Handle(await payOrderUseCase.Execute(id, dto, UserId, ct));
```

## 21. Testes

Sempre adicione ou atualize testes para: transições de status (pedido, compra, recomendação), regras de validação,
livro-razão e saldo (RNF002: saldo = soma dos movimentos), custo médio, preço efetivo com promoção, reembolso,
recebimento de compra, cálculo da previsão (`ForecastCalculator`, prioridade por ser puro) e autorização.

- **Unitário** para regra de domínio e comportamento de use case (mock só das bordas: repositórios, storage, IA; nunca
  de entidade).
- **Integração** para repositório e fluxos de venda e compra contra PostgreSQL real (Testcontainers).
- Um teste por comportamento, `Should_<resultado>_When_<condição>`, com o ID da regra no `[Trait("Rule", "RN-VD16")]`.

## 22. Comentários e documentação

- Comentário só para o **porquê** não óbvio (trade-off, referência a uma `RN-*` ou ADR, ordem de um passo).
- XML doc (`/// <summary>`) nas abstrações base reaproveitadas (bases de use case, `IStockLedger`, Filters).
- Mudou estrutura ou rota: atualize `docs/PROJECT.md`. Mudou contrato de tela: o `contract.md` do módulo.

## 23. Desenvolvimento com agentes de IA

O código do Nextflow é produzido em boa parte por agentes. Para manter o padrão:

1. **Uma fatia por vez**, na ordem de `docs/DOMAIN_LEVELS.md` e do plano (fundação → estoque → vendas/compras → previsão).
   Cada fatia é um recurso completo: modelo → configuração → migration → repositório → DTOs → validators → use cases →
   controller → testes → contrato no docs.
2. **Antes de escrever**, o agente lê este guia, as regras de arquitetura, o `business-rules.md` e o `contract.md` do
   módulo, e procura no código uma fatia pronta parecida para copiar a forma.
3. **Sem padrão novo.** Se a fatia não cabe nas bases, o agente para e pergunta, propondo a extensão da base (que vale
   para todos), nunca uma variação local.
4. **Sem regra inventada.** Regra ausente ou ambígua no docs vira pergunta ao Daniel, com recomendação.
5. **Entrega verificada:** `dotnet build` sem warning, testes da fatia verdes, checklist da seção 25 marcado.
6. Commit, push, merge e comando em servidor só com aprovação do Daniel.

## 24. Regras de ouro

1. Reaproveite a abstração base antes de criar qualquer coisa.
2. Toda resposta da Application é `ResultDto<T>` via `IResultFactory<T>`.
3. CRUD herda `UseCases/Base/`; nunca reescrito do zero.
4. Regra de negócio no ponto de extensão certo, nunca no controller.
5. `AppDbContext` nunca entra em use case nem em controller.
6. Repositório registra; commit só pelo `IUnitOfWork.CommitAsync()`, uma vez por requisição.
7. Controller herda `ApiController` e usa `Handle()`; fino, sem lógica.
8. Formato no FluentValidation (`Domain/Validators/`); regra de banco em `ValidateBusinessRules`.
9. `CancellationToken ct` repassado de ponta a ponta; nunca `.Result`/`.Wait()`.
10. Estoque só pelo `IStockLedger`; movimento nunca é editado nem apagado.
11. Variação é a unidade de estoque, venda e compra.
12. Preço efetivo só pelo `IProductPricingService`; data de negócio só pelo `IBusinessClock`.
13. O front nunca recalcula regra: a API devolve preço, total, saldo e status prontos.
14. Nunca logue dado sensível nem segredo.

## 25. Checklist antes do commit

- [ ] Entidade herda `BaseModel`; use case reaproveita uma base de `UseCases/Base/` quando aplicável.
- [ ] Resposta em `ResultDto<T>` via `IResultFactory<T>`; nenhum `throw` de negócio.
- [ ] Validators em `Domain/Validators/` (FluentValidation); regras de banco em `ValidateBusinessRules`.
- [ ] Repositório herda `BaseRepository<TEntity>`; commit via `IUnitOfWork.CommitAsync()`.
- [ ] Controller herda `ApiController` e usa `Handle()`; sem regra de negócio.
- [ ] Estoque só pelo `IStockLedger`; preço pelo `IProductPricingService`; datas pelo `IBusinessClock`.
- [ ] `CancellationToken ct` em toda a cadeia assíncrona.
- [ ] `dotnet build` sem warnings novos; testes da fatia verdes.
- [ ] Migration gerada e revisada, se mudou o schema.
- [ ] Enum alterado também em `../nextflow-front/src/types/enums.ts`.
- [ ] Nenhum segredo ou dado sensível em log, resposta ou commit.
- [ ] Regras `RN-*` respeitadas; conflito com o docs apontado, não silenciado; `contract.md` atualizado se o DTO mudou.

## 26. Referências

- `CLAUDE.md` — contexto do produto e regras inegociáveis.
- `docs/C_SHARP_ARCHITECTURE_RULES.md`, `docs/DOMAIN_LEVELS.md`, `docs/DOCKER.MD`, `docs/local-setup.md`.
- `../nextflow-docs/docs/architecture/` — ADRs, DER consolidado, módulos (regras e contratos).
- `atrena-api` — projeto de referência deste padrão.
- [.NET Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [FluentValidation Docs](https://docs.fluentvalidation.net)
- [Scrutor](https://github.com/khellang/Scrutor)
- [Serilog + ASP.NET Core](https://github.com/serilog/serilog-aspnetcore)

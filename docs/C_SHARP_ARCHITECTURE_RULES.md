# Regras de arquitetura C# (Nextflow API)

> **Origem:** padrão do `atrena-api` (`docs/C_SHARP_ARCHITECTURE_RULES.md`, versão de jun/2026), adotado pelo Daniel em 28/10/2026.
> Decisão registrada em `../nextflow-docs/docs/architecture/decisions/0010-padrao-de-arquitetura-atrena.md`.
> Complementos: `docs/dotnet-development-guidelines.md` (como trabalhar) e `docs/DOMAIN_LEVELS.md` (ordem das entidades).

Este documento define as regras de arquitetura, as abstrações e os padrões da API que **não podem ser quebrados**. Dizer que o
código segue SOLID e DDD não basta: é obrigatório reaproveitar as abstrações listadas aqui, sem criar padrões paralelos.
O código do Nextflow é escrito em boa parte por agentes de IA; estas regras são o que garante que o resultado pareça escrito
por uma pessoa só.

## 0. Mapa das camadas

```
Nextflow (API)  ──►  Nextflow.Application  ──►  Nextflow.Domain  ◄──  Nextflow.Infrastructure
 controllers          use cases, serviços         modelos, DTOs,          EF Core, repositórios,
 middleware           de aplicação, filtros,      validators, enums,      UnitOfWork, storage S3,
 Program.cs           motor de previsão           interfaces              IA, jobs, seeders
```

| Camada | Pode | Não pode |
|--|--|--|
| Domain | modelos, DTOs, validators, enums, value objects, interfaces (contratos de todas as camadas) | depender de qualquer outro projeto; acessar banco, HTTP, relógio do sistema |
| Application | orquestrar regras em use cases; chamar repositórios e serviços pelas interfaces do Domain | conhecer `AppDbContext`, EF (exceto `Include` em `GetInclude()`), HTTP, `IFormFile` |
| Infrastructure | implementar repositórios, `IUnitOfWork`, storage, cliente de IA, jobs, seeders, migrations | regra de negócio; commit fora do `UnitOfWork` |
| API | receber HTTP, autenticar, chamar **um** use case e devolver com `Handle()` | regra de negócio, `AppDbContext`, montar resposta à mão, `try/catch` de negócio |

## 1. Organização geral e padrões fundamentais

### 1.1 Respostas padronizadas (`ResultDto` e `ResultFactory`)
* **`ResultDto<T>`**: toda resposta (sucesso ou falha) que sai da Application para o Controller trafega em `ResultDto<T>`,
  centralizado em `Domain/Dtos/ResultDto.cs`.
* Campos: `Data`, `IsSuccess`, `Errors` (lista de mensagens), `FieldErrors` (mensagens por campo, em camelCase),
  `ErrorCode` (status HTTP) e `Message` (as mensagens unidas).
* **`IResultFactory<T>`**: injetada no use case; é a única forma de criar um `ResultDto`.
  `_resultFactory.Success(data)` · `_resultFactory.Failure("Estoque insuficiente para Brigadeiro 50 g.", 400)`.
* `FieldErrors` é a única extensão do Nextflow em relação ao atrena (ADR 0010). Ele existe para:
  * a base de use case devolver o erro de FluentValidation **por campo** (o front marca o `Field` certo);
  * o **aviso que pede confirmação** (RN-VD13): `_resultFactory.Confirm("A promoção deixa o preço abaixo do custo.")`
    devolve 422 com `FieldErrors["confirm"]`; o front confirma e reenvia com `ConfirmWarnings = true`.

### 1.2 Validações (FluentValidation)
* Regras de preenchimento e formato de DTO são escritas **só** com FluentValidation, em `Domain/Validators/`.
* Um validator por DTO de entrada: `<Dto>Validator : AbstractValidator<<Dto>>`. Mensagens em português.
* O use case filho de uma base **nunca** chama o validator: a base valida antes de tudo e devolve 422.
* Regra que precisa de banco (unicidade, existência de FK, saldo) **não** vai no validator; vai em `ValidateBusinessRules`.

## 2. Padrões de Domínio (`Nextflow.Domain`)

### 2.0 Padrão de DTOs

Todo recurso tem **dois DTOs fundamentais**: entrada (`XRequestDto`) e saída (`XResponseDto`). O `UpdateXRequestDto` só
existe quando a atualização aceita menos campos que a criação. DTOs ficam em `Domain/Dtos/<Recurso>/`, um arquivo por DTO.

**`XRequestDto`** — entrada para criação (e para atualização, quando os campos são os mesmos):
- contém todos os campos que o cliente envia;
- nunca contém `IFormFile`: arquivo chega como `UploadFileDto` (nome, tipo, `Stream`), montado no Controller;
- coleções filhas vêm aninhadas (`Variants`, `Items`, `Payments`) quando a tela salva tudo de uma vez.

**`UpdateXRequestDto`** — só os campos que podem mudar. Se todos podem, reaproveite o `XRequestDto`.

**`XResponseDto`** — saída de leitura:
- **é o único lugar onde a entidade vira representação de API**;
- construtor recebe a entidade: `XResponseDto(X entity)`; construtor vazio para serialização;
- converte enum em string com `GetApiName()`, monta campos calculados e mapeia navegações já carregadas;
- referência que a tela mostra vem com o nome junto (`SupplierId` + `SupplierName`).

**Exceções permitidas no Nextflow** (contratos de tela em `../nextflow-docs/docs/architecture/modules/<m>/contract.md`):

| DTO | Quando | Regra |
|--|--|--|
| `XListItemDto` | listagem com colunas bem menores que o detalhe (RNF003) | mesmo padrão do ResponseDto: construtor recebe a entidade |
| `XFormDto` | formulário que precisa de opções junto com os dados (`GET /{id}/form`) | herda o `XRequestDto` e acrescenta `Options` |
| `XOptionDto` | combo que precisa de mais que `value`/`label` (ex.: preço) | construtor recebe a entidade |
| `XSummaryDto` | resumo de leitura que um use case compõe na resposta, por consulta separada (`OwnerSummaryDto`, `CitySummaryDto`) | `record` posicional, montado no repositório ou no serviço; não sai sozinho na API nem aparece no contrato |

Nenhum outro sufixo é criado sem registrar a decisão.

#### Exemplo canônico — Fornecedor

```csharp
// Domain/Dtos/Suppliers/SupplierRequestDto.cs
public class SupplierRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public List<AddressRequestDto> Addresses { get; set; } = [];
    public List<ContactRequestDto> Contacts { get; set; } = [];
}

// Domain/Dtos/Suppliers/SupplierResponseDto.cs
public class SupplierResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public bool IsActive { get; set; }
    public List<AddressResponseDto> Addresses { get; set; } = [];
    public DateTime CreatedAt { get; set; }

    public SupplierResponseDto() { }

    public SupplierResponseDto(Supplier entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Cnpj = entity.Cnpj.Value;
        LeadTimeDays = entity.LeadTimeDays;
        IsActive = entity.IsActive;
        Addresses = [.. entity.Addresses.Select(a => new AddressResponseDto(a))];
        CreatedAt = entity.CreatedAt;
    }
}
```

---

### 2.1 Modelos e interfaces base
* **`BaseModel`**: toda entidade persistida herda `BaseModel` (`Id` Guid, `CreatedAt` UTC e os metadados abstratos
  `Preposition`, `Singular`, `Plural` de `IEntityMetadata`, usados nas mensagens das bases: "Produto já está inativo").
* Interfaces de comportamento, em `Domain/Interfaces/Domain/Models/`:
  * `IDeletable` — soft delete (`IsActive`, `Delete()`, `Reactivate()`);
  * `IUpdatable<TDto>` — a entidade se atualiza a partir de um DTO;
  * `IVersioned` — documento editável com `Version` (token de concorrência; conflito vira 409);
  * `IEntityMetadata` — metadados de apresentação.
* Tabelas em `snake_case` plural, configuradas por `IEntityTypeConfiguration<T>` (seção 4.3), nunca por atributo no modelo.

### 2.1.1 Construtores de modelo

- **Construtor privado sem parâmetros** (`private Product() { }`): só para o EF Core. Nunca público.
- **Construtor público que recebe o DTO**: o modelo não recebe campos avulsos que já estão no DTO. Valores que não vêm do
  DTO (id do usuário logado, número sequencial, custo calculado) entram como parâmetros a mais.
- **`private set` em todas as propriedades.** O estado muda por métodos com nome de intenção: `Update`, `Delete`,
  `Reactivate`, `Pay`, `Refund`, `Expire`, `Receive`, `ChangePrice`, `ApplyMovement`.

```csharp
public class Supplier : BaseModel, IDeletable, IUpdatable<SupplierRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public TaxDocument Cnpj { get; private set; } = null!;
    public int LeadTimeDays { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public virtual ICollection<Address> Addresses { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "Fornecedor";
    public override string Plural => "Fornecedores";

    private Supplier() { }

    public Supplier(SupplierRequestDto dto)
    {
        Name = dto.Name.Trim();
        Cnpj = TaxDocument.ParseCnpj(dto.Cnpj);
        LeadTimeDays = dto.LeadTimeDays;
        foreach (var address in dto.Addresses)
            Addresses.Add(new Address(address, Id, AddressOwnerTypeEnum.Supplier));
    }
}
```

### 2.1.2 Atualização — `IUpdatable<TUpdateDto>`
`Update(dto)` aplica só o que o DTO permite e sempre grava `UpdatedAt = DateTime.UtcNow`. Nunca é chamado pelo Controller;
quem chama é o `UpdateUseCaseBase`. Coleções filhas (variações, itens) são sincronizadas dentro do próprio `Update`
(adiciona novas, atualiza existentes por `Id`, inativa as que saíram), nunca apagadas por `DELETE` físico se tiverem histórico.

### 2.1.3 Soft delete e reativação — `IDeletable`
- `Delete()`: `IsActive = false` e `UpdatedAt`. `Reactivate()`: `IsActive = true` e `UpdatedAt`.
- Nunca há `DELETE` físico de entidade `IDeletable`. As bases `DeleteUseCaseBase` e `ReactivateUseCaseBase` chamam esses métodos.
- Livro-razão (`StockMovement`), histórico de preço, pagamentos e itens de documento fechado **não** são `IDeletable`:
  são imutáveis. Erro se corrige com lançamento contrário (ADR 0007).

### 2.1.4 Campos virtuais — navigation properties

**No modelo**
```csharp
public virtual Product Product { get; private set; } = null!;                 // objeto único
public virtual ICollection<ProductVariant> Variants { get; private set; } = []; // coleção
```
- `private set` sempre; o EF preenche com `Include`. Coleção nunca começa `null` (`= []`).
- O agregado raiz pode popular a própria coleção no construtor (produto cria as variações, pedido cria os itens).

**No ResponseDto**
```csharp
public SupplierResponseDto? PreferredSupplier { get; set; }
PreferredSupplier = entity.PreferredSupplier != null ? new SupplierResponseDto(entity.PreferredSupplier) : null;

public List<ProductVariantResponseDto> Variants { get; set; } = [];
Variants = [.. entity.Variants.Select(v => new ProductVariantResponseDto(v))];
```
- O ResponseDto **nunca** consulta o banco; só traduz o grafo que chegou.
- Objeto único é verificado com `!= null` antes de mapear; coleção não carregada chega vazia.

#### Quem decide o que é carregado — `GetInclude()`
Só o use case define o que vem do banco, sobrescrevendo `GetInclude()`. Nunca `Include` no repositório ou no Controller.
Inclua apenas o que o DTO de resposta mapeia.

```csharp
protected override Func<IQueryable<Product>, IQueryable<Product>>? GetInclude()
    => q => q.Include(p => p.Variants).ThenInclude(v => v.AttributeValues).ThenInclude(av => av.AttributeValue)
             .Include(p => p.Categories).ThenInclude(c => c.Category);
```

### 2.2 Value objects e enums
* Dado com invariante de formato é value object em `Domain/ValueObjects/` (`TaxDocument` para CPF/CNPJ, `Money` se precisar).
* Status e tipos são enums nomeados `<Conceito>Enum` (`OrderStatusEnum`, `StockMovementReasonEnum`), persistidos como
  inteiro (`: byte`, começando em 1) e expostos na API como string pelo `GetApiName()` do próprio arquivo do enum.
* Nunca `string` mágica nem `int` cru para status.

### 2.3 Contratos (`Domain/Interfaces/`)
```
Interfaces/
  Domain/Models/                 IEntityMetadata, IDeletable, IUpdatable<T>, IVersioned
  Application/Services/          IResultFactory<T>, IStockLedger, IProductPricingService, IBusinessClock
  Application/UseCases/Base/     ICreateUseCase, IUpdateUseCase, IDeleteUseCase, IReactivateUseCase,
                                 IGetAllUseCase, IGetByIdUseCase, IGetOptionsUseCase
  Application/UseCases/          interfaces de use cases fora do CRUD (IPayOrderUseCase, IReceivePurchaseOrderUseCase…)
  Infrastructure/Repositories/   IBaseRepository<T>, IUnitOfWork, I<Entidade>Repository
  Infrastructure/Services/       IFileStorage, IAiClient, IPasswordHasher, IForecastQueue
```

## 3. Padrões de Application (`Nextflow.Application`)

### 3.1 Use cases base (reaproveitamento)
* CRUD **nunca** é escrito do zero. Use as bases de `UseCases/Base/`: `CreateUseCaseBase`, `UpdateUseCaseBase`,
  `DeleteUseCaseBase`, `ReactivateUseCaseBase`, `GetAllUseCaseBase`, `GetByIdUseCaseBase`, `GetOptionsUseCaseBase`.
* O que foge do trivial é feito herdando a base e sobrescrevendo os template methods:
  * `ValidateBusinessRules(dto, ct)` — regras que consultam banco (SKU único, saldo suficiente, pedido no status certo).
    Retorna `ResultDto?`; `null` = ok.
  * `MapToEntity(dto)` — constrói a entidade (obrigatório no Create).
  * `BeforePersistence(entity, dto, ct)` — prepara o que entra no **mesmo commit**: entidades dependentes, número
    sequencial, lançamentos no livro-razão via `IStockLedger`.
  * `AfterPersistence(entity, dto, ct)` — efeitos depois do commit: notificação, fila da previsão, storage.
  * `GetInclude()`, `GetOrderBy()`, `BuildFilterExpression(filters)`, `UpdateEntity(entity, dto)`.

### 3.2 Use cases fora do CRUD
Operação de negócio que não é criar/editar/listar (pagar pedido, reembolsar, receber compra, aplicar recomendação, login)
é um use case com interface própria (`IPayOrderUseCase`), e mesmo assim:
- recebe um DTO validado por FluentValidation (chama o validator explicitamente, pois não há base);
- devolve `ResultDto<T>` pela `IResultFactory<T>`;
- grava com **um** `IUnitOfWork.CommitAsync(ct)` no fim;
- nunca chama outro use case. Lógica compartilhada vira serviço de aplicação com interface (ex.: `IStockLedger`).

### 3.3 Serviços de aplicação
Permitidos só quando a regra pertence a mais de um use case e tem nome de conceito do domínio, nunca "XService" genérico:
- `IStockLedger` — **único** caminho para mexer em `stock_movements` e `stock_balances` (ADR 0007). Registra o movimento e
  atualiza o saldo no contexto; **não faz commit**.
- `IProductPricingService` — preço efetivo (promoção vigente). O front nunca calcula preço.
- `IBusinessClock` — data de negócio no fuso `America/Sao_Paulo`; persistência sempre em UTC.
- Motor de previsão (`Forecast/`): cálculo puro, sem banco; o use case busca os dados e entrega ao motor.

### 3.4 Listagens e filtros
* Listagem usa `GetAllUseCaseBase` e devolve `ResultDto<PaginationDto<T>>` (`Items`, `Total`).
* Filtros chegam como JSON na query (`filters`), convertidos pelo `FilterHelper` em dicionário e transformados em
  Expression Tree com `FilterSet` + `FilterExpressionBuilder` no `BuildFilterExpression`.
* Ordenação pela query (`sort`, `order`) só aceita os campos da lista branca do use case (`GetSortableFields()`); fora dela
  vale o `GetOrderBy()` padrão.
* Combos usam `GetOptionsUseCaseBase` (`GET /<recurso>/options`): busca, só ativos, até 20 itens, devolvendo lista.
* O que a linha mostra e a entidade não tem por navegação (dono polimórfico) é lido por consulta separada em `PrepareMapping`.

## 4. Padrões de Infraestrutura (`Nextflow.Infrastructure`)

### 4.1 BaseRepository
* Todo repositório herda `BaseRepository<TEntity>`; leitura com `AsNoTracking()`, escrita com `GetByIdForUpdate` (rastreado).
* Método novo em `I<Entidade>Repository` só quando a consulta não cabe na base (agregações do painel, histórico mensal da
  previsão, saldo por variação × local). Consulta pesada de leitura pode usar Dapper dentro do repositório.

### 4.2 UnitOfWork
* Repositório **só registra** mudanças (`AddAsync`, `Update`, `Remove`). Ninguém chama `SaveChanges` fora do `UnitOfWork`.
* O commit é `IUnitOfWork.CommitAsync(ct)`, feito pela base ou pelo use case fora do CRUD. Um commit por requisição.
* Venda, movimento de estoque e saldo entram no mesmo commit; por isso não existe transação manual.

### 4.3 Banco
* `AppDbContext` com `ApplyConfigurationsFromAssembly`; uma `IEntityTypeConfiguration<T>` por entidade em
  `Database/Configurations/` (tabela, colunas `snake_case`, índices, FKs, `IsRowVersion`/`IsConcurrencyToken` em `Version`).
* Migrations são o dono do schema. Ordem das entidades: `docs/DOMAIN_LEVELS.md`.
* Seeders implementam `ISeeder` com `Order`, são idempotentes; o seed demo só roda com `Seed:Demo:Enabled`.

### 4.4 Integrações e trabalho em segundo plano
* Storage S3 (Silo) atrás de `IFileStorage`; IA (Gemini) atrás de `IAiClient`; sem chave de IA a previsão roda só com o cálculo.
* A análise de previsão roda em `BackgroundService` lendo `IForecastQueue`; o use case de disparo só cria a análise em
  `Running` e enfileira. O job usa um escopo de DI próprio e grava pelo `IUnitOfWork` desse escopo.

## 5. Padrões de Presentation (API)

### 5.1 `ApiController` e `Handle()`
* Todo controller herda `ApiController`. Herdar `ControllerBase` direto quebra o padrão de resposta.
* O controller é uma passagem: recebe o DTO, chama **um** use case e devolve `Handle(result)` (ou `HandleCreated`,
  `HandleNoContent`). Sucesso devolve o `Data`; falha devolve o `ResultDto` com o status do `ErrorCode`.
* `UserId` vem do token por `ApiController.UserId`; o controller o repassa ao use case quando a regra precisa.
* O `ApiController` é também um `IActionFilter`: corpo que não vira DTO devolve 422 no campo que não converteu (`birthDate: "xx"`) e 400 se estiver vazio ou com JSON quebrado, antes de chegar ao use case.
* Rotas explícitas em minúsculo (`[Route("api/products")]`), soft delete por `DELETE /{id}` e reativação por
  `PATCH /{id}/reactivate`.

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] SupplierRequestDto dto, CancellationToken ct)
    => Handle(await createSupplierUseCase.Execute(dto, ct));
```

### 5.2 `GlobalResultMiddleware`
Só para o inesperado: `DbUpdateConcurrencyException` → 409 ("Este registro foi alterado por outra pessoa"),
`DecoderFallbackException` (corpo com UTF-8 inválido, que estoura na leitura, antes do filtro do `ApiController`) → 400 com a
mesma mensagem de corpo ilegível, qualquer outra exceção → 500 com log no Serilog. Sempre no formato `ResultDto`. Erro de negócio nunca chega aqui.

---

### **REGRAS INQUEBRÁVEIS**
🚫 **NUNCA** crie uma estrutura "nova e criativa" de CRUD fora de `UseCases/Base/`.\
🚫 **NUNCA** lance exceção nem devolva `ActionResult` em use case; use a `IResultFactory`.\
🚫 **NUNCA** injete `AppDbContext` em use case, serviço de aplicação ou controller.\
🚫 **NUNCA** chame `SaveChanges` fora do `UnitOfWork`, nem faça mais de um commit por requisição.\
🚫 **NUNCA** coloque regra de negócio no controller.\
🚫 **NUNCA** omita o `CancellationToken ct` na cadeia assíncrona.\
🚫 **NUNCA** crie `UpdateXRequestDto` se todos os campos da criação podem ser atualizados.\
🚫 **NUNCA** mapeie entidade → DTO fora do construtor do DTO de saída (`ResponseDto`, `ListItemDto`, `OptionDto`).\
🚫 **NUNCA** use `public set` em modelo de domínio ou em navigation property.\
🚫 **NUNCA** passe campos avulsos ao construtor do modelo quando eles estão no DTO.\
🚫 **NUNCA** acesse um `virtual` de objeto único no DTO sem checar `!= null`.\
🚫 **NUNCA** defina `Include` fora do `GetInclude()` do use case.\
🚫 **NUNCA** inicie coleção de navegação com `null`.\
🚫 **NUNCA** grave em `stock_movements` ou `stock_balances` fora do `IStockLedger`.\
🚫 **NUNCA** apague ou edite um movimento de estoque, pagamento ou histórico de preço; corrija com lançamento contrário.\
🚫 **NUNCA** chame um use case de dentro de outro.\
🚫 **NUNCA** calcule preço efetivo fora do `IProductPricingService` nem use `DateTime.Now` para data de negócio.

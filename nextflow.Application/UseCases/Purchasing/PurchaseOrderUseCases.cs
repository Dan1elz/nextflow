using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Purchasing;

/// <summary>Criar pedido de compra em Orçamento (T-CP2, RN-CP07), com número sequencial e total calculados aqui.</summary>
public class CreatePurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    ISupplierRepository supplierRepository,
    IStockLocationRepository locationRepository,
    IProductVariantRepository variantRepository,
    IOwnerDirectory ownerDirectory,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<SavePurchaseOrderDto> validator,
    IResultFactory<PurchaseOrderDetailDto> resultFactory)
    : CreateUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, SavePurchaseOrderDto, PurchaseOrderDetailDto>(
        repository, unitOfWork, validator, resultFactory)
{
    private PurchaseOrderRules.Outcome _rules = null!;
    private PurchaseOrderDetailDto? _response;

    protected override async Task<ResultDto<PurchaseOrderDetailDto>?> ValidateBusinessRules(SavePurchaseOrderDto dto, CancellationToken ct)
    {
        _rules = await PurchaseOrderRules.Check(dto, supplierRepository, locationRepository, variantRepository, ct);
        return _rules.ToFailure(dto, _resultFactory);
    }

    protected override PurchaseOrder MapToEntity(SavePurchaseOrderDto dto)
        => new(dto, _rules.StoreId, UserId ?? Guid.Empty, _rules.LeadTimeDays, clock.Today);

    protected override async Task AfterPersistence(PurchaseOrder entity, SavePurchaseOrderDto dto, CancellationToken ct)
        => _response = await PurchaseOrderDetails.Load(_repository, ownerDirectory, entity.Id, ct);

    protected override PurchaseOrderDetailDto MapToResponseDto(PurchaseOrder entity) => _response ?? new(entity);
}

/// <summary>
/// Editar (T-CP2): só Orçamento ou Pendente (RN-CP03), com a versão lida (409 se mudou). Pendente continua Pendente e a
/// previsão segue contando do envio.
/// </summary>
public class UpdatePurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    ISupplierRepository supplierRepository,
    IStockLocationRepository locationRepository,
    IProductVariantRepository variantRepository,
    IOwnerDirectory ownerDirectory,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<SavePurchaseOrderDto> validator,
    IResultFactory<PurchaseOrderDetailDto> resultFactory)
    : UpdateUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, SavePurchaseOrderDto, PurchaseOrderDetailDto>(
        repository, unitOfWork, validator, resultFactory)
{
    private PurchaseOrderRules.Outcome _rules = null!;
    private PurchaseOrderDetailDto? _response;

    protected override Func<IQueryable<PurchaseOrder>, IQueryable<PurchaseOrder>> GetInclude() => q => q.Include(p => p.Items);

    protected override async Task<ResultDto<PurchaseOrderDetailDto>?> ValidateBusinessRules(PurchaseOrder entity, SavePurchaseOrderDto dto, CancellationToken ct)
    {
        if (PurchaseOrderRules.CheckState(entity, dto.Version, _resultFactory) is { } stateFailure)
            return stateFailure;

        _rules = await PurchaseOrderRules.Check(dto, supplierRepository, locationRepository, variantRepository, ct);
        return _rules.ToFailure(dto, _resultFactory);
    }

    protected override void UpdateEntity(PurchaseOrder entity, SavePurchaseOrderDto request)
        => entity.Update(request, _rules.StoreId, _rules.LeadTimeDays, clock.ToBusinessDate(entity.SentAt ?? entity.CreatedAt));

    protected override async Task AfterPersistence(PurchaseOrder entity, SavePurchaseOrderDto dto, CancellationToken ct)
        => _response = await PurchaseOrderDetails.Load(_repository, ownerDirectory, entity.Id, ct);

    protected override PurchaseOrderDetailDto MapToResponseDto(PurchaseOrder entity) => _response ?? new(entity);
}

/// <summary>Enviar ao fornecedor (RN-CP08): só Orçamento; grava <c>sentAt</c> e recomeça a previsão do dia do envio.</summary>
public class SendPurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    IOwnerDirectory ownerDirectory,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IResultFactory<PurchaseOrderDetailDto> resultFactory) : ISendPurchaseOrderUseCase
{
    public async Task<ResultDto<PurchaseOrderDetailDto>> Execute(Guid id, PurchaseOrderVersionDto? dto, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var order = await repository.GetByIdForUpdate(id, ct: ct);
        if (order is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        if (PurchaseOrderRules.CheckState(order, dto.Version, resultFactory) is { } failure)
            return failure;
        if (order.Status != PurchaseStatusEnum.Budget)
            return resultFactory.Failure("Só um pedido em orçamento pode ser enviado ao fornecedor.", 400);

        order.Send(DateTime.UtcNow, clock.Today);
        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success((await PurchaseOrderDetails.Load(repository, ownerDirectory, id, ct))!);
    }
}

/// <summary>
/// Cancelar (T-CP5): Orçamento ou Pendente, nunca Recebido (RN-CP03). Não gera movimentação. Pedido gerado por "Comprar" da
/// previsão (F3.4) devolve a recomendação a Pendente no mesmo commit (expirada, se já houver análise mais nova).
/// </summary>
public class CancelPurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastRunRepository runRepository,
    IOwnerDirectory ownerDirectory,
    IUnitOfWork unitOfWork,
    IValidator<CancelPurchaseOrderDto> validator,
    IResultFactory<PurchaseOrderDetailDto> resultFactory) : ICancelPurchaseOrderUseCase
{
    public async Task<ResultDto<PurchaseOrderDetailDto>> Execute(Guid id, CancelPurchaseOrderDto? dto, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var order = await repository.GetByIdForUpdate(id, ct: ct);
        if (order is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        if (PurchaseOrderRules.CheckState(order, dto.Version, resultFactory) is { } failure)
            return failure;

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation, 422);

        var utcNow = DateTime.UtcNow;
        order.Cancel(dto.Reason, utcNow);

        // RN-DF28: a recomendação só volta (ou expira) quando nenhum outro pedido dela segue aberto.
        var applied = await recommendationRepository.GetAppliedToPurchaseOrderForUpdate(id, ct);
        if (applied.Count > 0)
        {
            var latestRun = (await runRepository.GetAll(
                r => r.Status == ForecastRunStatusEnum.Completed || r.Status == ForecastRunStatusEnum.CompletedWithoutAi,
                0, 1, orderBy: q => q.OrderByDescending(r => r.StartedAt), ct: ct)).FirstOrDefault();
            foreach (var (recommendation, stillOpen) in applied)
                recommendation.RevertPurchase(id, stillOpen, recommendation.ForecastRunId == latestRun?.Id, utcNow);
        }

        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success((await PurchaseOrderDetails.Load(repository, ownerDirectory, id, ct))!);
    }
}

/// <summary>
/// Receber (T-CP4): só Pendente, o pedido inteiro (RN-CP04). Uma entrada `Purchase` por item no local de entrega pelo
/// <see cref="IStockLedger"/> (RN-CE14), que recalcula o custo médio e o último custo da variação (RN-CE31); grava o último
/// custo no vínculo com o fornecedor, o custo médio após a entrada por item, quem recebeu e o lead time real (RN-CP08).
/// Tudo num commit; entrada simultânea na mesma variação estoura no token dela e vira 409.
/// </summary>
public class ReceivePurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    IStockLedger ledger,
    ILotRepository lotRepository,
    IStockMovementRepository movementRepository,
    IOwnerDirectory ownerDirectory,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<ReceivePurchaseOrderDto> validator,
    IResultFactory<PurchaseOrderDetailDto> resultFactory) : IReceivePurchaseOrderUseCase
{
    private const string LinePrefix = "Lines[";

    public async Task<ResultDto<PurchaseOrderDetailDto>> Execute(Guid id, ReceivePurchaseOrderDto? dto, Guid userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation, 422);

        var order = await repository.GetByIdForUpdate(id, q => q
            .Include(p => p.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product).ThenInclude(p => p.Suppliers), ct);
        if (order is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        if (PurchaseOrderRules.CheckState(order, dto.Version, resultFactory) is { } failure)
            return failure;
        if (order.Status != PurchaseStatusEnum.Pending)
            return resultFactory.Failure("Só um pedido pendente (enviado ao fornecedor) pode ser recebido.", 400);

        var itemIds = dto.Items.Select(i => i.ItemId).ToList();
        if (itemIds.Count != itemIds.Distinct().Count() || !itemIds.ToHashSet().SetEquals(order.Items.Select(i => i.Id)))
            return resultFactory.FieldFailure("items", "Receba todos os itens do pedido, cada um uma vez: não há recebimento parcial.");

        var items = itemIds.Select(itemId => order.Items.Single(i => i.Id == itemId)).ToList();
        var lotFailures = CheckLots(dto, items);
        if (lotFailures.Count > 0)
            return resultFactory.Failure(new ValidationResult(lotFailures), 422);

        // Uma entrada por lote (F1.7): o item que controla lote vira tantas linhas quantos lotes chegaram, todas com o custo
        // do item (o médio é da variação, RN-CE31). Sem lote, uma linha por item, como antes.
        var known = await lotRepository.GetForVariants([.. items.Select(i => i.VariantId).Distinct()], ct);
        var lines = new List<StockLineDto>();
        var lineItem = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var lots = dto.Items[i].Lots ?? [];
            if (lots.Count == 0)
            {
                lines.Add(Line(order, item, null, item.Quantity));
                lineItem.Add(i);
                continue;
            }

            for (var j = 0; j < lots.Count; j++)
            {
                var (lot, error) = await LotRegistry.FindOrCreate(lotRepository, known, item.VariantId, lots[j].Code, lots[j].ExpiresOn, ct);
                if (error is not null)
                    return resultFactory.FieldFailure($"items[{i}].lots[{j}].code", error);
                lines.Add(Line(order, item, lot!.Id, lots[j].Quantity));
                lineItem.Add(i);
            }
        }

        var document = new StockDocumentDto { SourceType = SourceTypeEnum.PurchaseOrder, SourceId = order.Id, UserId = userId };
        var failures = await ledger.Post(document, lines, ct);
        if (failures.Count > 0)
            return resultFactory.Failure(new ValidationResult(failures.Select(f => ToFormField(f, lineItem))), 422);

        var utcNow = DateTime.UtcNow;
        foreach (var item in items)
        {
            // A variação rastreada é a mesma instância que o livro-razão atualizou: o médio já inclui esta entrada.
            item.RecordReceipt(item.Variant.AverageCost);
            item.Variant.Product.Suppliers.FirstOrDefault(s => s.SupplierId == order.SupplierId)?.RecordLastCost(item.UnitCost, utcNow);
        }

        order.Receive(userId, utcNow, clock.Today, clock.ToBusinessDate(order.SentAt!.Value));
        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success((await PurchaseOrderDetails.Load(repository, ownerDirectory, id, ct, movementRepository))!);
    }

    /// <summary>
    /// Produto que controla lote recebe ao menos um lote, e os lotes somam a quantidade do item (F1.7); produto sem lote não
    /// recebe lote. O item vem na ordem do corpo, então o índice é o do formulário.
    /// </summary>
    private static List<ValidationFailure> CheckLots(ReceivePurchaseOrderDto dto, List<PurchaseItem> items)
    {
        var failures = new List<ValidationFailure>();
        for (var i = 0; i < items.Count; i++)
        {
            var product = items[i].Variant.Product;
            var lots = dto.Items[i].Lots ?? [];
            if (!product.TracksLots)
            {
                if (lots.Count > 0)
                    failures.Add(new($"items[{i}].lots", $"{product.Name} não controla lote."));
                continue;
            }

            if (lots.Count == 0)
            {
                failures.Add(new($"items[{i}].lots", $"Informe o lote e a validade de {product.Name}."));
                continue;
            }

            var total = lots.Sum(l => l.Quantity);
            if (total != items[i].Quantity)
                failures.Add(new($"items[{i}].lots", $"Os lotes somam {total:0.###}, mas o item tem {items[i].Quantity:0.###}."));
        }

        return failures;
    }

    private static StockLineDto Line(PurchaseOrder order, PurchaseItem item, Guid? lotId, decimal quantity) => new()
    {
        VariantId = item.VariantId,
        LocationId = order.ReceiveLocationId,
        LotId = lotId,
        Type = MovementTypeEnum.In,
        Reason = MovementReasonEnum.Purchase,
        Quantity = quantity,
        UnitCost = item.UnitCost,
    };

    /// <summary>
    /// "Lines[k].Quantity" do livro-razão vira "items[i].quantity" (k é a linha, i o item dela: um item com lote gera várias);
    /// local fechado vira "receiveLocationId".
    /// </summary>
    private static ValidationFailure ToFormField(ValidationFailure failure, List<int> lineItem)
    {
        var name = failure.PropertyName;
        if (name.EndsWith(".LocationId"))
            return new("receiveLocationId", failure.ErrorMessage);
        if (!name.StartsWith(LinePrefix))
            return failure;
        var close = name.IndexOf(']');
        var field = name[(close + 2)..];
        var item = lineItem[int.Parse(name[LinePrefix.Length..close])];
        return new($"items[{item}].{char.ToLowerInvariant(field[0])}{field[1..]}", failure.ErrorMessage);
    }
}

/// <summary>Visualização (T-CP3), com o contato principal do fornecedor.</summary>
public class GetPurchaseOrderByIdUseCase(
    IPurchaseOrderRepository repository,
    IStockMovementRepository movementRepository,
    IOwnerDirectory ownerDirectory,
    IResultFactory<PurchaseOrderDetailDto> resultFactory)
    : GetByIdUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, PurchaseOrderDetailDto>(repository, resultFactory)
{
    public override async Task<ResultDto<PurchaseOrderDetailDto>> Execute(Guid id, CancellationToken ct = default)
    {
        var result = await base.Execute(id, ct);
        if (result.IsSuccess)
        {
            result.Data!.Supplier.MainContact = (await ownerDirectory.GetMainContact(OwnerTypeEnum.Supplier, result.Data.Supplier.Id, ct))?.Value;
            await PurchaseOrderDetails.FillLots(result.Data, movementRepository, ct);
        }

        return result;
    }

    protected override Func<IQueryable<PurchaseOrder>, IQueryable<PurchaseOrder>> GetInclude() => PurchaseOrderDetails.Include;

    protected override PurchaseOrderDetailDto MapToResponseDto(PurchaseOrder entity) => new(entity);
}

/// <summary>Formulário de edição (T-CP2): o pedido, a sugestão de cada linha e os locais de entrega ativos.</summary>
public class GetPurchaseOrderFormUseCase(
    IPurchaseOrderRepository repository,
    IStockLocationRepository locationRepository,
    IStockBalanceRepository balanceRepository,
    IPurchaseItemRepository itemRepository,
    IResultFactory<PurchaseOrderFormDto> resultFactory) : IGetPurchaseOrderFormUseCase
{
    public async Task<ResultDto<PurchaseOrderFormDto>> Execute(Guid id, CancellationToken ct = default)
    {
        var order = await repository.GetById(id, PurchaseOrderDetails.Include, ct);
        if (order is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        var variantIds = order.Items.Select(i => i.VariantId).ToList();
        var available = await PurchaseStock.AvailableAtStore(balanceRepository, variantIds, order.StoreId, ct);
        var incoming = await itemRepository.SumIncomingByVariant(variantIds, order.StoreId, ct);

        var items = order.Items.OrderBy(i => i.SortOrder)
            .Select(i => new PurchaseOrderFormItemDto(i, order.SupplierId, available.GetValueOrDefault(i.VariantId), incoming.GetValueOrDefault(i.VariantId)))
            .ToList();

        var locations = (await locationRepository.GetAll(
                l => l.IsActive && l.Store.IsActive, 0, int.MaxValue, q => q.Include(l => l.Store),
                q => q.OrderBy(l => l.Store.Name).ThenBy(l => l.Name).ThenBy(l => l.Id), ct))
            .Select(l => new LocationOptionDto(l))
            .ToList();

        return resultFactory.Success(new PurchaseOrderFormDto(order, items, locations));
    }
}

/// <summary>Sugestão da linha (T-CP2): vínculo com o fornecedor, custo médio, disponível na loja e a caminho (RN-CP05).</summary>
public class GetPurchaseItemSuggestionUseCase(
    IProductVariantRepository variantRepository,
    IStoreRepository storeRepository,
    IStockBalanceRepository balanceRepository,
    IPurchaseItemRepository itemRepository,
    IResultFactory<PurchaseItemSuggestionDto> resultFactory) : IGetPurchaseItemSuggestionUseCase
{
    public async Task<ResultDto<PurchaseItemSuggestionDto>> Execute(Guid supplierId, Guid variantId, Guid storeId, CancellationToken ct = default)
    {
        if (!await storeRepository.Exists(s => s.Id == storeId && s.IsActive, ct))
            return resultFactory.FieldFailure("storeId", "Loja não encontrada ou inativa.");

        var variant = await variantRepository.GetById(variantId, PurchaseVariants.Include, ct);
        if (variant is null || variant.RemovedOn is not null)
            return resultFactory.Failure($"Entidade com id {variantId} não encontrada.", 404);

        var available = await PurchaseStock.AvailableAtStore(balanceRepository, [variantId], storeId, ct);
        var incoming = await itemRepository.SumIncomingByVariant([variantId], storeId, ct);

        return resultFactory.Success(new PurchaseItemSuggestionDto(
            variant, supplierId, available.GetValueOrDefault(variantId), incoming.GetValueOrDefault(variantId)));
    }
}

/// <summary>
/// Busca de itens (T-CP2): variações ativas de produtos ativos por nome, SKU ou código de barras. Com <c>onlyLinked</c>, só
/// as de produtos vinculados ao fornecedor; sem, todas, com as vinculadas primeiro.
/// </summary>
public class GetPurchaseVariantOptionsUseCase(
    IProductVariantRepository variantRepository,
    IFileStorage fileStorage,
    IResultFactory<List<PurchaseVariantOptionDto>> resultFactory) : IGetPurchaseVariantOptionsUseCase
{
    private const int MaxLimit = 50;
    private const int DefaultLimit = 20;

    public async Task<ResultDto<List<PurchaseVariantOptionDto>>> Execute(
        Guid supplierId, string? search, bool onlyLinked, int limit, CancellationToken ct = default)
    {
        var term = search?.Trim().ToLowerInvariant() ?? string.Empty;
        var variants = await variantRepository.GetAll(
            v => v.IsActive && v.RemovedOn == null && v.Product.IsActive
                && (!onlyLinked || v.Product.Suppliers.Any(s => s.SupplierId == supplierId))
                && (term == string.Empty
                    || v.Product.Name.ToLower().Contains(term)
                    || v.Sku.ToLower().Contains(term)
                    || (v.Barcode != null && v.Barcode.ToLower().Contains(term))),
            0,
            limit is >= 1 and <= MaxLimit ? limit : DefaultLimit,
            PurchaseVariants.Include,
            q => q.OrderBy(v => v.Product.Suppliers.Any(s => s.SupplierId == supplierId) ? 0 : 1)
                .ThenBy(v => v.Product.Name).ThenBy(v => v.SortOrder).ThenBy(v => v.Id),
            ct);

        return resultFactory.Success([.. variants.Select(v =>
            new PurchaseVariantOptionDto(v, supplierId, fileStorage.GetPublicUrl(v.Product.ImageKey)))]);
    }
}

/// <summary>Listagem (T-CP1) da loja selecionada, com atraso calculado pela data de negócio.</summary>
public class GetAllPurchaseOrdersUseCase(
    IPurchaseOrderRepository repository,
    IBusinessClock clock,
    IResultFactory<PaginationDto<PurchaseOrderListItemDto>> resultFactory)
    : GetAllUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, PurchaseOrderListItemDto>(repository, resultFactory),
        IGetPurchaseOrdersUseCase
{
    private Guid? _storeId;

    public Task<ResultDto<PaginationDto<PurchaseOrderListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters, string? sort, string? order, Guid? storeId,
        CancellationToken ct = default)
    {
        _storeId = storeId;
        return Execute(offset, limit, filters, sort, order, ct);
    }

    protected override Expression<Func<PurchaseOrder, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<PurchaseOrder>()
            .WhereEnumEquals(set, "status", p => p.Status)
            .WhereGuidEquals(set, "supplierId", p => p.SupplierId)
            .WhereGuidEquals(set, "receiveLocationId", p => p.ReceiveLocationId);

        if (_storeId is { } storeId)
            builder.And(p => p.StoreId == storeId);

        if (set.TryGetString("search", out var search))
        {
            var term = search.Trim().ToLowerInvariant();
            if (int.TryParse(term, out var number))
                builder.And(p => p.Number == number || p.Supplier.Name.ToLower().Contains(term));
            else
                builder.And(p => p.Supplier.Name.ToLower().Contains(term));
        }

        if (set.TryGetString("onlyLate", out var rawLate) && FilterValueParsers.TryParseBool(rawLate, out var onlyLate) && onlyLate)
        {
            var today = clock.Today;
            builder.And(p => p.Status == PurchaseStatusEnum.Pending && p.ExpectedOn < today);
        }

        // Período de criação em dias de negócio (America/Sao_Paulo): "to" inclui o dia inteiro.
        if (set.TryGetString("from", out var rawFrom) && FilterValueParsers.TryParseDateOnly(rawFrom, out var from))
        {
            var start = clock.StartOfDayUtc(from);
            builder.And(p => p.CreatedAt >= start);
        }
        if (set.TryGetString("to", out var rawTo) && FilterValueParsers.TryParseDateOnly(rawTo, out var to))
        {
            var end = clock.StartOfDayUtc(to.AddDays(1));
            builder.And(p => p.CreatedAt < end);
        }

        return builder.Predicate;
    }

    protected override Func<IQueryable<PurchaseOrder>, IQueryable<PurchaseOrder>> GetInclude()
        => q => q.Include(p => p.Supplier).Include(p => p.ReceiveLocation).Include(p => p.Store).Include(p => p.Items);

    protected override Func<IQueryable<PurchaseOrder>, IOrderedQueryable<PurchaseOrder>> GetOrderBy()
        => q => q.OrderByDescending(p => p.Number);

    protected override IReadOnlyDictionary<string, Expression<Func<PurchaseOrder, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<PurchaseOrder, object?>>>
        {
            ["number"] = p => p.Number,
            ["createdAt"] = p => p.CreatedAt,
            ["expectedOn"] = p => p.ExpectedOn,
            ["total"] = p => p.Total,
        };

    protected override PurchaseOrderListItemDto MapToResponseDto(PurchaseOrder entity) => new(entity, clock.Today);
}

/// <summary>
/// Regras do save que dependem do banco (T-CP2): fornecedor e local ativos, variação válida e única no pedido, casas
/// decimais do produto, e os avisos de mínimo, múltiplo (RN-CE30, L-CP2) e fora de linha (RN-CE19), que pedem confirmação.
/// </summary>
internal static class PurchaseOrderRules
{
    public const string ConflictMessage = "Este registro foi alterado por outra pessoa.";

    internal sealed class Outcome
    {
        public List<ValidationFailure> Failures { get; } = [];
        public List<string> Warnings { get; } = [];
        public Guid StoreId { get; set; }
        public int LeadTimeDays { get; set; }

        public ResultDto<T>? ToFailure<T>(SavePurchaseOrderDto dto, IResultFactory<T> resultFactory)
        {
            if (Failures.Count > 0)
                return resultFactory.Failure(new ValidationResult(Failures), 422);
            if (Warnings.Count > 0 && !dto.ConfirmWarnings)
                return resultFactory.Confirm($"{string.Join(" ", Warnings)} Deseja salvar mesmo assim?");
            return null;
        }
    }

    /// <summary>Versão obrigatória e igual à atual (409), e status que ainda muda (RN-CP03).</summary>
    public static ResultDto<T>? CheckState<T>(PurchaseOrder order, uint? version, IResultFactory<T> resultFactory)
    {
        if (version is null)
            return resultFactory.FieldFailure("version", "Informe a versão do pedido.");
        if (version != order.Version)
            return resultFactory.Failure(ConflictMessage, 409);
        if (!order.IsOpen)
            return resultFactory.Failure("Só pedidos em orçamento ou pendentes podem ser alterados.", 400);
        return null;
    }

    public static async Task<Outcome> Check(
        SavePurchaseOrderDto dto,
        ISupplierRepository supplierRepository,
        IStockLocationRepository locationRepository,
        IProductVariantRepository variantRepository,
        CancellationToken ct)
    {
        var outcome = new Outcome();

        var supplier = await supplierRepository.GetById(dto.SupplierId, ct: ct);
        if (supplier is null || !supplier.IsActive)
            outcome.Failures.Add(new(nameof(dto.SupplierId), "Escolha um fornecedor ativo."));

        var location = await locationRepository.GetById(dto.ReceiveLocationId, q => q.Include(l => l.Store), ct);
        if (location is null || !location.IsActive || !location.Store.IsActive)
            outcome.Failures.Add(new(nameof(dto.ReceiveLocationId), "Escolha um local ativo de uma loja ativa."));
        else
            outcome.StoreId = location.StoreId;

        var variantIds = dto.Items.Select(i => i.VariantId).Distinct().ToList();
        var variants = (await variantRepository.GetAll(v => variantIds.Contains(v.Id), 0, int.MaxValue,
                q => q.Include(v => v.Product).ThenInclude(p => p.Suppliers), ct: ct))
            .ToDictionary(v => v.Id);

        var seen = new HashSet<Guid>();
        var itemLeadTimes = new List<int?>();
        for (var index = 0; index < dto.Items.Count; index++)
        {
            var item = dto.Items[index];
            var prefix = $"{nameof(dto.Items)}[{index}].";

            if (!seen.Add(item.VariantId))
            {
                outcome.Failures.Add(new(prefix + nameof(item.VariantId), "Esta variação já está no pedido."));
                continue;
            }

            if (!variants.TryGetValue(item.VariantId, out var variant) || !variant.IsActive || variant.RemovedOn is not null || !variant.Product.IsActive)
            {
                outcome.Failures.Add(new(prefix + nameof(item.VariantId), "Escolha uma variação ativa."));
                continue;
            }

            var product = variant.Product;
            if (decimal.Round(item.Quantity, product.DecimalPlaces) != item.Quantity)
                outcome.Failures.Add(new(prefix + nameof(item.Quantity), product.DecimalPlaces == 0
                    ? "Este produto é comprado em quantidade inteira."
                    : $"A quantidade aceita até {product.DecimalPlaces} casa(s) decimal(is)."));

            var link = product.Suppliers.FirstOrDefault(s => s.SupplierId == dto.SupplierId);
            itemLeadTimes.Add(link?.LeadTimeDays);

            if (product.Lifecycle == LifecycleEnum.Discontinued)
                outcome.Warnings.Add($"{product.Name} está fora de linha.");
            if (link?.MinOrderQty is { } minimum && item.Quantity < minimum)
                outcome.Warnings.Add($"{product.Name}: abaixo do pedido mínimo do fornecedor ({minimum:0.###}).");
            if (link?.OrderMultiple is { } multiple && multiple > 0 && item.Quantity % multiple != 0)
                outcome.Warnings.Add($"{product.Name}: fora do múltiplo de compra ({multiple:0.###}).");
        }

        if (supplier is not null)
            outcome.LeadTimeDays = PurchaseOrder.EffectiveLeadTime(supplier.LeadTimeDays, itemLeadTimes);

        return outcome;
    }
}

/// <summary>Carga do detalhe (T-CP3), usada pela visualização e pela resposta de criar, editar, enviar e cancelar.</summary>
internal static class PurchaseOrderDetails
{
    public static IQueryable<PurchaseOrder> Include(IQueryable<PurchaseOrder> query)
        => query.Include(p => p.Supplier)
            .Include(p => p.ReceiveLocation).ThenInclude(l => l.Store)
            .Include(p => p.CreatedBy)
            .Include(p => p.ReceivedBy)
            .Include(p => p.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product).ThenInclude(p => p.Suppliers)
            .Include(p => p.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.AttributeValues)
                .ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute);

    /// <summary>Com <paramref name="movements"/>, traz também os lotes recebidos (só existem depois do recebimento).</summary>
    public static async Task<PurchaseOrderDetailDto?> Load(IPurchaseOrderRepository repository, IOwnerDirectory ownerDirectory, Guid id,
        CancellationToken ct, IStockMovementRepository? movements = null)
    {
        var order = await repository.GetById(id, Include, ct);
        if (order is null)
            return null;

        var dto = new PurchaseOrderDetailDto(order);
        dto.Supplier.MainContact = (await ownerDirectory.GetMainContact(OwnerTypeEnum.Supplier, order.SupplierId, ct))?.Value;
        if (movements is not null)
            await FillLots(dto, movements, ct);
        return dto;
    }

    /// <summary>Lotes recebidos por item (T-CP3, F1.7), lidos das entradas do livro-razão que o recebimento gravou.</summary>
    public static async Task FillLots(PurchaseOrderDetailDto dto, IStockMovementRepository movements, CancellationToken ct)
    {
        if (dto.Items.All(i => !i.TracksLots))
            return;

        var entries = await movements.GetAll(
            m => m.SourceType == SourceTypeEnum.PurchaseOrder && m.SourceId == dto.Id && m.LotId != null,
            0, int.MaxValue, q => q.Include(m => m.Lot), q => q.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id), ct);
        var byVariant = entries.GroupBy(m => m.VariantId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var item in dto.Items.Where(i => i.TracksLots && byVariant.ContainsKey(i.VariantId)))
        {
            item.Lots = [.. byVariant[item.VariantId].Select(m => new PurchaseItemLotDto
            {
                LotCode = m.Lot!.Code,
                ExpiresOn = m.Lot.ExpiresOn,
                Quantity = m.Quantity,
            })];
        }
    }
}

internal static class PurchaseVariants
{
    public static IQueryable<ProductVariant> Include(IQueryable<ProductVariant> query)
        => query.Include(v => v.Product).ThenInclude(p => p.Suppliers)
            .Include(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute);
}

internal static class PurchaseStock
{
    /// <summary>Saldo físico nos locais ativos da loja (disponível = físico até haver reserva, RN-CE08).</summary>
    public static async Task<Dictionary<Guid, decimal>> AvailableAtStore(
        IStockBalanceRepository balanceRepository, IReadOnlyCollection<Guid> variantIds, Guid storeId, CancellationToken ct)
        => (await balanceRepository.GetAll(
                b => variantIds.Contains(b.VariantId) && b.Location.StoreId == storeId && b.Location.IsActive, 0, int.MaxValue, ct: ct))
            .GroupBy(b => b.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));
}

using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Services.Forecast;
using Nextflow.Application.UseCases.Purchasing;
using Nextflow.Application.UseCases.Sales;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Forecast;

/// <summary>Cargas comuns das recomendações (T-DF2 a T-DF8).</summary>
internal static class ForecastRecommendationReading
{
    public const string NotFound = "Recomendação não encontrada.";
    public const string NotPending = "Esta recomendação não está mais pendente.";

    public static IQueryable<ForecastRecommendation> ListInclude(IQueryable<ForecastRecommendation> query)
        => query.Include(r => r.Product).ThenInclude(p => p.Categories).ThenInclude(link => link.Category).ThenInclude(c => c.Parent)
            .Include(r => r.Product).ThenInclude(p => p.Suppliers).ThenInclude(link => link.Supplier)
            .Include(r => r.AppliedPurchaseOrder);

    public static async Task<List<ForecastRecommendationListItemDto>> ListItemsAsync(
        IForecastRecommendationRepository repository, IFileStorage fileStorage, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var items = await repository.GetAll(r => ids.Contains(r.Id), 0, int.MaxValue, ListInclude, q => q.OrderBy(r => r.Product.Name), ct);
        return [.. items.Select(r => new ForecastRecommendationListItemDto(r, fileStorage.GetPublicUrl(r.Product?.ImageKey)))];
    }
}

/// <summary>
/// Detalhe (T-DF3): a recomendação com os motivos da IA e calculado, os números do cálculo (os do motor e os do cadastro atual), o
/// rateio por variação com o saldo por local (RN-DF21) e a previsão do produto.
/// </summary>
public class GetForecastRecommendationUseCase(
    IForecastRecommendationRepository recommendationRepository,
    IStockPolicyRepository policyRepository,
    IStockBalanceRepository balanceRepository,
    ProductForecastReader productForecastReader,
    IFileStorage fileStorage,
    IResultFactory<ForecastRecommendationDetailDto> resultFactory) : IGetForecastRecommendationUseCase
{
    public async Task<ResultDto<ForecastRecommendationDetailDto>> Execute(Guid recommendationId, CancellationToken ct = default)
    {
        var recommendation = await recommendationRepository.GetById(recommendationId, q => ForecastRecommendationReading.ListInclude(q)
            .Include(r => r.Product).ThenInclude(p => p.Events)
            .Include(r => r.Product).ThenInclude(p => p.Variants).ThenInclude(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute)
            .Include(r => r.Items), ct);
        if (recommendation is null)
            return resultFactory.Failure(ForecastRecommendationReading.NotFound, 404);

        var product = recommendation.Product;
        var preferred = product.Suppliers.FirstOrDefault(s => s.IsPreferred);
        StockPolicy? policy = null;
        if (product.AbcClass is { } abcClass)
            policy = await policyRepository.Get(p => p.AbcClass == abcClass, ct: ct);

        var calculation = new ForecastCalculationDto
        {
            LeadTimeDays = recommendation.LeadTimeDays,
            LeadTimeSource = recommendation.LeadTimeSource.GetApiName(),
            SafetyStock = recommendation.SafetyStock,
            PurchaseNeed = recommendation.CalculatedQuantity,
            ReservedStock = 0,
            TargetCoverageDays = product.TargetCoverageDays ?? policy?.TargetCoverageDays,
            ShelfLifeDays = product.TracksLots ? product.ShelfLifeDays : null,
            MinOrderQty = preferred?.MinOrderQty,
            OrderMultiple = preferred?.OrderMultiple,
            BasedOnLastYear = recommendation.BasedOnLastYear,
            MonthsOfHistory = recommendation.MonthsOfHistory,
            NextMonthDemand = recommendation.NextMonthDemand,
            NextMonthSeasonalIndex = recommendation.NextMonthSeasonalIndex,
        };

        var split = await SplitAsync(recommendation, ct);
        var forecast = await productForecastReader.LoadAsync(product, ProductForecastReader.DefaultMonths, ct);

        return resultFactory.Success(new ForecastRecommendationDetailDto(
            recommendation, fileStorage.GetPublicUrl(product.ImageKey), calculation, split, forecast));
    }

    private async Task<List<VariantSplitDto>> SplitAsync(ForecastRecommendation recommendation, CancellationToken ct)
    {
        if (recommendation.Items.Count == 0)
            return [];

        var variantIds = recommendation.Items.Select(i => i.VariantId).ToList();
        var balances = (await balanceRepository.GetAll(
                b => variantIds.Contains(b.VariantId) && b.Location.IsActive, 0, int.MaxValue, q => q.Include(b => b.Location), ct: ct))
            .ToList();
        var variants = recommendation.Product.Variants.ToDictionary(v => v.Id);

        return [.. recommendation.Items
            .OrderBy(i => variants.TryGetValue(i.VariantId, out var v) ? v.SortOrder : int.MaxValue)
            .Select(item => new VariantSplitDto
            {
                VariantId = item.VariantId,
                VariantLabel = variants.TryGetValue(item.VariantId, out var variant) && ProductVariantResponseDto.LabelOf(variant) is { Length: > 0 } label ? label : null,
                SharePercent = item.SharePercent,
                SuggestedQuantity = item.Quantity,
                AvailableByLocation = [.. balances
                    .Where(b => b.VariantId == item.VariantId)
                    .GroupBy(b => b.LocationId)
                    .Select(g => new LocationAvailableDto { LocationId = g.Key, LocationName = g.First().Location.Name, Available = g.Sum(b => b.Quantity) })
                    .OrderBy(l => l.LocationName, StringComparer.OrdinalIgnoreCase)],
            })];
    }
}

/// <summary>Aba "Previsão" do produto (T-DF5): a previsão sem exigir recomendação.</summary>
public class GetProductForecastUseCase(
    IProductRepository productRepository,
    ProductForecastReader productForecastReader,
    IResultFactory<ProductForecastDto> resultFactory) : IGetProductForecastUseCase
{
    public async Task<ResultDto<ProductForecastDto>> Execute(Guid productId, int months, CancellationToken ct = default)
    {
        var product = await productRepository.GetById(productId, q => q.Include(p => p.Events), ct);
        return product is null
            ? resultFactory.Failure("Produto não encontrado.", 404)
            : resultFactory.Success(await productForecastReader.LoadAsync(product, months, ct));
    }
}

/// <summary>
/// Aplicar "Comprar" (T-DF4, RN-DF15): cada grupo vira um pedido de compra em <b>Orçamento</b> para o fornecedor (nada é enviado),
/// com as regras do pedido de compra (fornecedor e local ativos, variação ativa, casas decimais, prazo pelo maior prazo efetivo); as
/// recomendações ficam Aplicadas com o link do pedido. Recomendação só pendente e "Comprar"; a variação tem de ser do produto dela;
/// uma recomendação não pode estar em dois fornecedores. Avisos de mínimo, múltiplo e validade são do dialog (não bloqueiam). Um commit.
/// </summary>
public partial class ApplyPurchaseRecommendationsUseCase(
    IForecastRecommendationRepository recommendationRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    ISupplierRepository supplierRepository,
    IStockLocationRepository locationRepository,
    IProductVariantRepository variantRepository,
    IFileStorage fileStorage,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<ApplyPurchaseDto> validator,
    IResultFactory<ApplyPurchaseResultDto> resultFactory) : IApplyPurchaseRecommendationsUseCase
{
    public async Task<ResultDto<ApplyPurchaseResultDto>> Execute(ApplyPurchaseDto? dto, Guid? userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation);

        var recommendationIds = dto.Groups.SelectMany(g => g.Items).Select(i => i.RecommendationId).Distinct().ToList();
        var recommendations = (await recommendationRepository.GetAllForUpdate(r => recommendationIds.Contains(r.Id), ct)).ToDictionary(r => r.Id);
        var variantIds = dto.Groups.SelectMany(g => g.Items).Select(i => i.VariantId).Distinct().ToList();
        var variants = (await variantRepository.GetAll(v => variantIds.Contains(v.Id), 0, int.MaxValue, ct: ct)).ToDictionary(v => v.Id);

        var failures = new List<ValidationFailure>();
        var groupOf = new Dictionary<Guid, int>();
        var orders = new List<(PurchaseOrder Order, HashSet<Guid> RecommendationIds)>();

        for (var g = 0; g < dto.Groups.Count; g++)
        {
            var group = dto.Groups[g];
            var prefix = $"groups[{g}].";

            for (var i = 0; i < group.Items.Count; i++)
            {
                var item = group.Items[i];
                var field = $"{prefix}items[{i}].";
                if (!recommendations.TryGetValue(item.RecommendationId, out var recommendation))
                    failures.Add(new(field + "recommendationId", ForecastRecommendationReading.NotFound));
                else if (!recommendation.IsPending || recommendation.Action != RecommendationActionEnum.Buy)
                    failures.Add(new(field + "recommendationId", recommendation.IsPending ? "Só recomendações \"Comprar\" geram pedido de compra." : ForecastRecommendationReading.NotPending));
                else if (!variants.TryGetValue(item.VariantId, out var variant) || variant.ProductId != recommendation.ProductId)
                    failures.Add(new(field + "variantId", "A variação não é do produto da recomendação."));
                else if (groupOf.TryGetValue(item.RecommendationId, out var other) && other != g)
                    failures.Add(new(field + "recommendationId", "A recomendação já está em outro fornecedor."));
                else
                    groupOf[item.RecommendationId] = g;
            }

            // Mesmas regras do pedido de compra (T-CP2), sobre os itens somados por variação; cada erro de item volta para as linhas
            // enviadas que formaram aquele item (F3.4c), e o resto ganha só o prefixo do grupo.
            var summed = group.Items.Select((item, line) => (Item: item, Line: line)).GroupBy(x => x.Item.VariantId).ToList();
            var save = new SavePurchaseOrderDto
            {
                SupplierId = group.SupplierId,
                ReceiveLocationId = group.ReceiveLocationId,
                Note = group.Note,
                ConfirmWarnings = true,
                Items = [.. summed.Select(v => new SavePurchaseItemDto { VariantId = v.Key, Quantity = v.Sum(x => x.Item.Quantity), UnitCost = v.First().Item.UnitCost })],
            };
            var lines = summed.Select(v => v.Select(x => x.Line).ToList()).ToList();
            var rules = await PurchaseOrderRules.Check(save, supplierRepository, locationRepository, variantRepository, ct);
            failures.AddRange(rules.Failures.SelectMany(f => ToRequestLines(f, prefix, lines)));

            if (rules.Failures.Count == 0)
                orders.Add((new PurchaseOrder(save, rules.StoreId, userId.Value, rules.LeadTimeDays, clock.Today), [.. group.Items.Select(i => i.RecommendationId)]));
        }

        if (failures.Count > 0)
            return resultFactory.Failure(new ValidationResult(failures), 422);

        var utcNow = clock.UtcNow;
        foreach (var (order, ids) in orders)
        {
            await purchaseOrderRepository.AddAsync(order, ct);
            foreach (var id in ids)
                recommendations[id].ApplyPurchase(order.Id, userId.Value, utcNow);
        }
        await unitOfWork.CommitAsync(ct);

        var orderIds = orders.Select(o => o.Order.Id).ToList();
        var saved = await purchaseOrderRepository.GetAll(p => orderIds.Contains(p.Id), 0, int.MaxValue, q => q.Include(p => p.Supplier), q => q.OrderBy(p => p.Number), ct);
        return resultFactory.Success(new ApplyPurchaseResultDto
        {
            PurchaseOrders = [.. saved.Select(p => new AppliedPurchaseOrderDto { Id = p.Id, Number = p.Number, SupplierName = p.Supplier.Name, Total = p.Total })],
            Recommendations = await ForecastRecommendationReading.ListItemsAsync(recommendationRepository, fileStorage, recommendationIds, ct),
        });
    }

    /// <summary>"Items[k].Quantity" da lista somada → "groups[g].items[i].quantity" para cada linha enviada i que formou o item k.</summary>
    private static IEnumerable<ValidationFailure> ToRequestLines(ValidationFailure failure, string prefix, IReadOnlyList<List<int>> lines)
    {
        var match = SummedItem().Match(failure.PropertyName);
        if (!match.Success)
            return [new ValidationFailure(prefix + Camel(failure.PropertyName), failure.ErrorMessage)];

        var field = Camel(match.Groups[2].Value);
        return lines[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)]
            .Select(line => new ValidationFailure($"{prefix}items[{line}].{field}", failure.ErrorMessage));
    }

    [GeneratedRegex(@"^Items\[(\d+)\]\.(.+)$")]
    private static partial Regex SummedItem();

    /// <summary>"Items[0].VariantId" → "items[0].variantId", como o resto do 422.</summary>
    private static string Camel(string path)
        => string.Join('.', path.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}

/// <summary>
/// Aplicar "Promover" (T-DF6): só recomendação pendente "Promover". Mesmas regras da promoção manual (RN-VD10/11 e L-VD10): início a
/// partir de hoje, variações do produto não removidas, sem sobreposição no mesmo escopo e confirmação abaixo do custo médio (RN-VD13).
/// Sem variações escolhidas, uma promoção do produto inteiro; com elas, uma por variação. Origem "Recomendação da IA", ligada à
/// recomendação, que fica Aplicada. Um commit.
/// </summary>
public class ApplyPromotionRecommendationUseCase(
    IForecastRecommendationRepository recommendationRepository,
    IPromotionRepository promotionRepository,
    IProductRepository productRepository,
    IProductPricingService pricing,
    IFileStorage fileStorage,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<ApplyPromotionDto> validator,
    IResultFactory<ApplyPromotionResultDto> resultFactory) : IApplyPromotionRecommendationUseCase
{
    public async Task<ResultDto<ApplyPromotionResultDto>> Execute(Guid recommendationId, ApplyPromotionDto? dto, Guid? userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var recommendation = await recommendationRepository.GetByIdForUpdate(recommendationId, ct: ct);
        if (recommendation is null)
            return resultFactory.Failure(ForecastRecommendationReading.NotFound, 404);
        if (!recommendation.IsPending)
            return resultFactory.Failure(ForecastRecommendationReading.NotPending, 400);
        if (recommendation.Action != RecommendationActionEnum.Promote)
            return resultFactory.Failure("Só recomendações \"Promover\" geram promoção.", 400);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation);
        if (dto.StartDate < clock.Today)
            return resultFactory.FieldFailure("startDate", "O início deve ser hoje ou depois.");

        var product = await productRepository.GetById(recommendation.ProductId, q => q.Include(p => p.Variants), ct);
        if (product is null || !product.IsActive)
            return resultFactory.Failure("Produto não encontrado ou inativo.", 400);

        // Escopo: produto inteiro (uma promoção) ou uma promoção por variação escolhida.
        var selected = dto.VariantIds is { Count: > 0 } ids ? ids.Distinct().ToList() : null;
        var scopes = new List<Guid?>();
        if (selected is null)
            scopes.Add(null);
        else
        {
            foreach (var variantId in selected)
            {
                if (PromotionScope.Variants(product, variantId).Count == 0)
                    return resultFactory.FieldFailure("variantIds", "Há variação que não pertence ao produto ou foi removida.");
                scopes.Add(variantId);
            }
        }

        // Uma consulta para todos os escopos: as promoções ativas do produto no período, cruzadas em memória.
        var (productId, start, end) = (product.Id, dto.StartDate, dto.EndDate);
        var active = await promotionRepository.GetAll(
            p => p.ProductId == productId && p.IsActive && p.StartDate <= end && start <= p.EndDate, 0, int.MaxValue, ct: ct);
        var overlap = scopes
            .Select(variantId => active.FirstOrDefault(p => p.VariantId == null || variantId == null || p.VariantId == variantId))
            .FirstOrDefault(p => p is not null);
        if (overlap is not null)
            return resultFactory.FieldFailure("startDate",
                $"Já existe promoção ativa deste produto de {overlap.StartDate:dd/MM/yyyy} a {overlap.EndDate:dd/MM/yyyy} no mesmo escopo.");

        var belowCost = scopes
            .SelectMany(variantId => PromotionScope.Variants(product, variantId))
            .Any(v => PromotionScope.IsBelowCost(v, pricing.GetPromotionalPrice(v.Price, dto.DiscountPercent)));
        if (belowCost && !dto.ConfirmWarnings)
            return resultFactory.Confirm("A promoção deixa o preço abaixo do custo médio. Deseja continuar?");

        var promotions = scopes.Select(variantId => new Promotion(new PromotionRequestDto
        {
            ProductId = product.Id,
            VariantId = variantId,
            DiscountPercent = dto.DiscountPercent,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = "Recomendação da previsão",
        }, recommendation.Id, userId.Value, belowCost)).ToList();

        await promotionRepository.AddRangeAsync(promotions, ct);
        recommendation.ApplyPromotion(promotions[0].Id, userId.Value, clock.UtcNow);
        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success(new ApplyPromotionResultDto
        {
            PromotionIds = [.. promotions.Select(p => p.Id)],
            Recommendation = (await ForecastRecommendationReading.ListItemsAsync(recommendationRepository, fileStorage, [recommendation.Id], ct))[0],
        });
    }
}

/// <summary>Descartar (T-DF8): só pendente; motivo opcional ("Outro" pede o texto).</summary>
public class DismissRecommendationUseCase(
    IForecastRecommendationRepository recommendationRepository,
    IFileStorage fileStorage,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<DismissRecommendationDto> validator,
    IResultFactory<ForecastRecommendationListItemDto> resultFactory) : IDismissRecommendationUseCase
{
    public async Task<ResultDto<ForecastRecommendationListItemDto>> Execute(Guid recommendationId, DismissRecommendationDto? dto, Guid? userId, CancellationToken ct = default)
    {
        dto ??= new DismissRecommendationDto();
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation);

        var recommendation = await recommendationRepository.GetByIdForUpdate(recommendationId, ct: ct);
        if (recommendation is null)
            return resultFactory.Failure(ForecastRecommendationReading.NotFound, 404);
        if (!recommendation.IsPending)
            return resultFactory.Failure(ForecastRecommendationReading.NotPending, 400);

        DismissReasonEnum? code = dto.ReasonCode is null ? null : Enum.Parse<DismissReasonEnum>(dto.ReasonCode);
        recommendation.Dismiss(code, dto.Note, userId.Value, clock.UtcNow);
        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success((await ForecastRecommendationReading.ListItemsAsync(recommendationRepository, fileStorage, [recommendation.Id], ct))[0]);
    }
}

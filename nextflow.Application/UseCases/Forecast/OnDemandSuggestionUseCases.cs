using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;

namespace Nextflow.Application.UseCases.Forecast;

/// <summary>Janela de vendas e chamada à IA comuns às duas sugestões sob demanda (RN-DF30).</summary>
internal static class OnDemandSuggestion
{
    public const int SalesWindowDays = 90;
    public const int RecentWindowDays = 30;

    public static async Task<(decimal Daily, decimal RecentDaily)> SalesRate(
        IOrderRepository orderRepository, IBusinessClock clock, Guid variantId, Guid storeId, CancellationToken ct)
    {
        var sold = await orderRepository.GetPaidQuantityOfVariant(variantId, storeId,
            clock.StartOfDayUtc(clock.Today.AddDays(-SalesWindowDays)), ct);
        var recent = await orderRepository.GetPaidQuantityOfVariant(variantId, storeId,
            clock.StartOfDayUtc(clock.Today.AddDays(-RecentWindowDays)), ct);
        return (Round(sold / SalesWindowDays), Round(recent / RecentWindowDays));
    }

    /// <summary>Pergunta à IA; sem chave ou com falha, devolve nulo e quem chama fica com o cálculo (RN-DF13).</summary>
    public static async Task<OnDemandAiResponse?> AskAi(IAiClient aiClient, ILogger logger, object payload, CancellationToken ct)
    {
        if (!aiClient.IsConfigured)
            return null;
        try
        {
            var raw = await aiClient.GenerateJsonAsync(OnDemandSuggestionPrompt.SystemPrompt,
                OnDemandSuggestionPrompt.BuildUserPrompt(payload), OnDemandSuggestionPrompt.ResponseSchema, ct);
            return OnDemandSuggestionPrompt.ParseResponse(raw);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Sugestão sob demanda: a IA falhou ({Type}). Usando o cálculo.", ex.GetType().Name);
            return null;
        }
    }

    public static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Quanto comprar de uma variação abaixo do mínimo num local (RN-DF30): o cálculo volta ao mínimo e cobre o prazo de entrega do
/// fornecedor preferido + 30 dias de venda da loja; a IA revisa entre 0,5× e 1,5× do calculado (RN-DF12) e explica. Produto fora de
/// linha não recebe compra (RN-CE19). Só leitura; com <c>useAi</c> falso devolve só o cálculo, sem gastar cota.
/// </summary>
public class GetPurchaseSuggestionUseCase(
    IProductVariantRepository variantRepository,
    IStockLocationRepository locationRepository,
    IStockBalanceRepository balanceRepository,
    IPurchaseItemRepository itemRepository,
    IOrderRepository orderRepository,
    IAiClient aiClient,
    IBusinessClock clock,
    ILogger<GetPurchaseSuggestionUseCase> logger,
    IResultFactory<PurchaseSuggestionDto> resultFactory) : IGetPurchaseSuggestionUseCase
{
    public async Task<ResultDto<PurchaseSuggestionDto>> Execute(Guid variantId, Guid locationId, bool useAi, CancellationToken ct = default)
    {
        var variant = await variantRepository.GetById(variantId, q => q
            .Include(v => v.LocationMinimums)
            .Include(v => v.Product).ThenInclude(p => p.Suppliers).ThenInclude(s => s.Supplier), ct);
        if (variant is null)
            return resultFactory.Failure("Variação não encontrada.", 404);
        var location = await locationRepository.GetById(locationId, q => q.Include(l => l.Store), ct);
        if (location is null)
            return resultFactory.Failure("Local não encontrado.", 404);
        var product = variant.Product;
        if (product.Lifecycle == LifecycleEnum.Discontinued)
            return resultFactory.Failure("Produto fora de linha não recebe sugestão de compra.", 400);

        var available = (await balanceRepository.GetAll(b => b.VariantId == variantId && b.LocationId == locationId, 0, int.MaxValue, ct: ct))
            .Sum(b => b.Quantity);
        var incoming = (await itemRepository.SumIncomingByVariant([variantId], location.StoreId, ct)).GetValueOrDefault(variantId);
        var minimum = product.EffectiveMinimum(variant, locationId);
        var (daily, recentDaily) = await OnDemandSuggestion.SalesRate(orderRepository, clock, variantId, location.StoreId, ct);

        var supplier = product.Suppliers.Where(s => s.Supplier.IsActive).OrderByDescending(s => s.IsPreferred).FirstOrDefault();
        var leadTime = supplier?.LeadTimeDays ?? supplier?.Supplier.LeadTimeDays ?? 7;
        var input = new PurchaseSuggestionInput(available, minimum, incoming, daily, leadTime, supplier?.MinOrderQty, supplier?.OrderMultiple);
        var calculated = OnDemandSuggestionCalculator.PurchaseQuantity(input);

        var reason = $"Em {location.Store.Name} › {location.Name}: saldo {PtBr.Compact(available, 3)}, mínimo {PtBr.Compact(minimum, 3)}, " +
            $"a caminho {PtBr.Compact(incoming, 3)}; vende cerca de {PtBr.Compact(daily, 2)} por dia. " +
            (calculated > 0
                ? $"Comprar {PtBr.Compact(calculated, 3)} volta ao mínimo e cobre {leadTime} dias de entrega + {OnDemandSuggestionCalculator.CoverageDays} dias de venda."
                : "O que está a caminho já cobre o mínimo e a venda do período.");
        var dto = new PurchaseSuggestionDto
        {
            ProductId = product.Id,
            VariantId = variantId,
            LocationId = locationId,
            StoreId = location.StoreId,
            Available = available,
            MinQuantity = minimum,
            Incoming = incoming,
            DailySales = daily,
            LeadTimeDays = leadTime,
            SupplierId = supplier?.SupplierId,
            SupplierName = supplier?.Supplier.Name,
            MinOrderQty = supplier?.MinOrderQty,
            OrderMultiple = supplier?.OrderMultiple,
            Quantity = calculated,
            CalculatedQuantity = calculated,
            Reason = reason,
        };

        if (!useAi || calculated <= 0)
            return resultFactory.Success(dto);

        var ai = await OnDemandSuggestion.AskAi(aiClient, logger, new
        {
            acao = "comprar",
            produto = product.Name,
            loja = location.Store.Name,
            local = location.Name,
            saldo = available,
            minimo = minimum,
            aCaminho = incoming,
            vendaDiaria90Dias = daily,
            vendaDiaria30Dias = recentDaily,
            prazoEntregaDias = leadTime,
            pedidoMinimo = supplier?.MinOrderQty,
            multiplo = supplier?.OrderMultiple,
            sugestaoCalculada = new { quantidade = calculated, motivo = reason },
            quantidadeMinima = OnDemandSuggestionCalculator.RoundToSupplier(calculated * 0.5m, supplier?.MinOrderQty, supplier?.OrderMultiple),
            quantidadeMaxima = calculated * 1.5m,
        }, ct);
        if (ai?.Quantidade is not { } asked)
            return resultFactory.Success(dto);

        dto.Quantity = OnDemandSuggestionCalculator.ClampAiQuantity(asked, calculated, supplier?.MinOrderQty, supplier?.OrderMultiple);
        dto.Reason = OnDemandSuggestionPrompt.CleanReason(ai.Motivo) ?? reason;
        dto.FromAi = true;
        return resultFactory.Success(dto);
    }
}

/// <summary>
/// Desconto e dias para escoar um lote que vence num local (RN-DF30): o cálculo compara o saldo do lote com a venda normal até o
/// vencimento; a IA revisa com desconto entre 5% e o máximo da margem e dias entre 7 e 45, sem passar do vencimento (RN-DF12).
/// </summary>
public class GetLotPromotionSuggestionUseCase(
    IStockBalanceRepository balanceRepository,
    IOrderRepository orderRepository,
    IAiClient aiClient,
    IBusinessClock clock,
    ILogger<GetLotPromotionSuggestionUseCase> logger,
    IResultFactory<PromotionSuggestionDto> resultFactory) : IGetLotPromotionSuggestionUseCase
{
    public async Task<ResultDto<PromotionSuggestionDto>> Execute(Guid lotId, Guid locationId, CancellationToken ct = default)
    {
        var balance = await balanceRepository.Get(b => b.LotId == lotId && b.LocationId == locationId && b.Quantity > 0, q => q
            .Include(b => b.Lot)
            .Include(b => b.Location).ThenInclude(l => l.Store)
            .Include(b => b.Variant).ThenInclude(v => v.Product), ct);
        if (balance?.Lot is null)
            return resultFactory.Failure("Lote sem saldo neste local.", 404);

        var variant = balance.Variant;
        var lot = balance.Lot;
        var daysToExpire = Math.Max(lot.ExpiresOn.DayNumber - clock.Today.DayNumber, 0);
        var (daily, recentDaily) = await OnDemandSuggestion.SalesRate(orderRepository, clock, variant.Id, balance.Location.StoreId, ct);
        var cost = variant.AverageCost ?? variant.LastCost;
        var calc = OnDemandSuggestionCalculator.Promotion(new PromotionSuggestionInput(balance.Quantity, daysToExpire, daily, variant.Price, cost));

        var reason = $"Lote {lot.Code} em {balance.Location.Store.Name}: {PtBr.Compact(balance.Quantity, 3)} vencendo em {daysToExpire} " +
            $"{(daysToExpire == 1 ? "dia" : "dias")}; no ritmo atual ({PtBr.Compact(daily, 2)} por dia) saem cerca de {PtBr.Compact(calc.ExpectedSales, 2)}. " +
            $"Desconto de {PtBr.Percent(calc.DiscountPercent)} por {calc.Days} {(calc.Days == 1 ? "dia" : "dias")} (máximo pela margem: {PtBr.Percent(calc.MaxDiscountPercent)}).";
        var dto = new PromotionSuggestionDto
        {
            ProductId = variant.ProductId,
            ProductName = variant.Product.Name,
            VariantId = variant.Id,
            LotId = lot.Id,
            LocationId = locationId,
            StoreId = balance.Location.StoreId,
            Quantity = balance.Quantity,
            ExpiresOn = lot.ExpiresOn,
            DaysToExpire = daysToExpire,
            DailySales = daily,
            ExpectedSales = calc.ExpectedSales,
            MaxDiscountPercent = calc.MaxDiscountPercent,
            DiscountPercent = calc.DiscountPercent,
            PromotionDays = calc.Days,
            CalculatedDiscountPercent = calc.DiscountPercent,
            Reason = reason,
        };

        var minDays = OnDemandSuggestionCalculator.PromotionDays(0, daysToExpire);
        var maxDays = OnDemandSuggestionCalculator.PromotionDays(int.MaxValue, daysToExpire);
        var ai = await OnDemandSuggestion.AskAi(aiClient, logger, new
        {
            acao = "promover",
            produto = variant.Product.Name,
            loja = balance.Location.Store.Name,
            lote = lot.Code,
            quantidadeNoLote = balance.Quantity,
            venceEm = lot.ExpiresOn,
            diasParaVencer = daysToExpire,
            vendaDiaria90Dias = daily,
            vendaDiaria30Dias = recentDaily,
            vendaEsperadaAteVencer = calc.ExpectedSales,
            preco = variant.Price,
            custoMedio = cost,
            descontoMaximoPct = calc.MaxDiscountPercent,
            diasMinimos = minDays,
            diasMaximos = maxDays,
            sugestaoCalculada = new { descontoPct = calc.DiscountPercent, diasPromocao = calc.Days, motivo = reason },
        }, ct);
        if (ai?.DescontoPct is not { } asked)
            return resultFactory.Success(dto);

        dto.DiscountPercent = OnDemandSuggestionCalculator.ClampDiscount(asked, calc.MaxDiscountPercent, fromAi: true);
        dto.PromotionDays = ai.DiasPromocao is { } days
            ? OnDemandSuggestionCalculator.PromotionDays((int)Math.Round(days, MidpointRounding.AwayFromZero), daysToExpire)
            : calc.Days;
        dto.Reason = OnDemandSuggestionPrompt.CleanReason(ai.Motivo) ?? reason;
        dto.FromAi = true;
        return resultFactory.Success(dto);
    }
}

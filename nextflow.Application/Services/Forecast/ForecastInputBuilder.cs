using Nextflow.Domain.Dtos.Stock;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Forecast;
using Nextflow.Application.Services.DerivedData;
using Nextflow.Domain.Dtos.DerivedData;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Lê os dados da previsão pelos repositórios e monta as entradas puras do cálculo (F3.2). Só leitura: vendas e sazonalidade vêm das
/// tabelas derivadas da F3.1, o estoque e os lotes do livro-razão, o "a chegar" dos pedidos de compra em Orçamento ou Pendente.
/// </summary>
public class ForecastInputBuilder(
    IProductRepository productRepository,
    IStockLocationRepository locationRepository,
    IStockBalanceRepository balanceRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IOrderRepository orderRepository,
    IPromotionRepository promotionRepository,
    ICalendarEventRepository eventRepository,
    IStockPolicyRepository policyRepository,
    IProductSalesMonthlyRepository salesMonthlyRepository,
    IProductSeasonalityRepository seasonalityRepository,
    IPurchaseItemRepository purchaseItemRepository,
    IBusinessClock clock)
{
    /// <summary>Janela da participação de cada loja nas vendas ("Transferir", RN-DF16).</summary>
    public const int StoreShareWindowDays = 90;

    /// <summary>Pedidos recebidos usados para medir o prazo real de cada fornecedor.</summary>
    public const int LeadTimeSamplesPerSupplier = 5;

    /// <summary>Categoria "guarda-chuva": contém 80% ou mais dos produtos com venda (e há pelo menos 5 deles); não serve de referência.</summary>
    public const decimal UmbrellaCategoryShare = 0.8m;
    public const int UmbrellaCategoryMinProducts = 5;

    private const int DefaultLeadTimeDays = 7;

    public async Task<ForecastSourceData> LoadAsync(DateOnly today, CancellationToken ct)
    {
        var window = ForecastCalculationContext.HistoryWindowMonths;
        var calendar = new ForecastCalculationContext(today, 1);

        var products = (await productRepository.GetAll(p => p.IsActive, 0, int.MaxValue, query => query
                .Include(p => p.Variants.Where(v => v.IsActive && v.RemovedOn == null)).ThenInclude(v => v.LocationMinimums)
                .Include(p => p.Categories).ThenInclude(link => link.Category)
                .Include(p => p.Suppliers).ThenInclude(link => link.Supplier)
                .Include(p => p.Events).ThenInclude(link => link.Event),
                orderBy: query => query.OrderBy(p => p.Name).ThenBy(p => p.Id), ct: ct))
            .ToList();

        var sellableLocationIds = (await locationRepository.GetAll(l => l.IsSellable && l.IsActive, 0, int.MaxValue, ct: ct))
            .Select(l => l.Id)
            .ToList();
        var variantIds = products.SelectMany(p => p.Variants).Select(v => v.Id).ToList();

        var stockByVariant = await balanceRepository.SumByVariant(variantIds, ct);
        var incomingByVariant = await purchaseOrderRepository.GetIncomingByVariant(ct);
        var lots = await balanceRepository.GetLotBalances(ct);
        var soldByVariant = await orderRepository.GetPaidQuantityByVariant(clock.StartOfDayUtc(today.AddMonths(-window)), ct);

        var policies = (await policyRepository.GetAll(_ => true, 0, int.MaxValue, ct: ct)).ToDictionary(p => p.AbcClass);
        var promotions = (await promotionRepository.GetAll(p => p.IsActive && p.StartDate <= today && p.EndDate >= today, 0, int.MaxValue, ct: ct))
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.DiscountPercent).First());
        var calendarEvents = (await eventRepository.GetAll(e => e.IsActive, 0, int.MaxValue, ct: ct)).ToList();

        var leadTimes = await RealLeadTimes(today, ct);

        // F3.1: série mensal e índice sazonal já calculados.
        var monthlyRows = (await salesMonthlyRepository.GetAll(_ => true, 0, int.MaxValue, ct: ct)).ToList();
        var seasonalityRows = (await seasonalityRepository.GetAll(_ => true, 0, int.MaxValue, ct: ct))
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var productIds = products.Select(p => p.Id).ToHashSet();
        var series = new Dictionary<Guid, decimal[]>();
        var firstSale = new Dictionary<Guid, YearMonth>();
        var revenue12 = new Dictionary<Guid, decimal>();
        var firstWindowMonth = calendar.MonthAt(0);

        foreach (var row in monthlyRows.Where(r => productIds.Contains(r.ProductId)))
        {
            var month = new YearMonth(row.Year, row.Month);
            if (month > calendar.LastCompleteMonth)
                continue; // o mês corrente fica fora (RN-DF02)

            if (row.QuantitySold > 0 && (!firstSale.TryGetValue(row.ProductId, out var current) || month < current))
                firstSale[row.ProductId] = month;

            var index = YearMonth.MonthsBetween(firstWindowMonth, month);
            if (index < 0 || index >= window)
                continue;

            if (!series.TryGetValue(row.ProductId, out var values))
                series[row.ProductId] = values = new decimal[window];
            values[index] += row.QuantitySold;

            if (index >= window - 12)
                revenue12[row.ProductId] = revenue12.GetValueOrDefault(row.ProductId) + row.Revenue;
        }

        int MonthsOfHistory(Guid productId) => firstSale.TryGetValue(productId, out var first)
            ? Math.Clamp(YearMonth.MonthsBetween(first, calendar.LastCompleteMonth) + 1, 0, window)
            : 0;

        var abcClasses = AbcClassifier.Classify(products.Select(p => (p.Id, revenue12.GetValueOrDefault(p.Id))));
        var categoryIndexes = CategoryIndexes(products, series, today, out var referenceCategory);

        var lotsByVariant = lots.ToLookup(l => l.VariantId);
        var inputs = new List<ForecastProductInput>(products.Count);
        var variantHistory = new Dictionary<Guid, IReadOnlyList<(Guid, decimal)>>();
        var decimalPlaces = new Dictionary<Guid, int>();

        foreach (var product in products)
        {
            var variants = product.Variants.OrderBy(v => v.SortOrder).ToList();
            var history = variants.Select(v => (v.Id, soldByVariant.GetValueOrDefault(v.Id))).ToList();
            variantHistory[product.Id] = history;
            decimalPlaces[product.Id] = product.DecimalPlaces;

            // Preço e custo médios ponderados pela venda das variações (sem histórico, peso igual).
            var weights = variants.ToDictionary(v => v.Id, v => history.Any(h => h.Item2 > 0) ? soldByVariant.GetValueOrDefault(v.Id) : 1m);
            var priceWeight = weights.Values.Sum();
            var price = priceWeight > 0 ? variants.Sum(v => v.Price * weights[v.Id]) / priceWeight : variants.FirstOrDefault()?.Price ?? 0m;
            var costed = variants.Where(v => v.AverageCost > 0 && weights[v.Id] > 0).ToList();
            decimal? cost = costed.Count > 0 ? costed.Sum(v => v.AverageCost!.Value * weights[v.Id]) / costed.Sum(v => weights[v.Id]) : null;

            var minStock = variants.Sum(v => sellableLocationIds.Sum(locationId => product.EffectiveMinimum(v, locationId)));

            var productLots = variants.SelectMany(v => lotsByVariant[v.Id]).Where(l => l.ExpiresOn >= today).ToList();
            var nextExpiry = productLots.Count > 0 ? productLots.Min(l => l.ExpiresOn) : (DateOnly?)null;
            var nextExpiryQuantity = nextExpiry is { } expiry ? productLots.Where(l => l.ExpiresOn == expiry).Sum(l => l.Quantity) : 0m;

            var link = product.Suppliers.OrderByDescending(s => s.IsPreferred).ThenBy(s => s.Supplier.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            var (leadTime, leadSource) = EffectiveLeadTime(link, leadTimes);

            abcClasses.TryGetValue(product.Id, out var abcClass);
            policies.TryGetValue(abcClass, out var policy);

            promotions.TryGetValue(product.Id, out var promotion);
            referenceCategory.TryGetValue(product.Id, out var category);

            inputs.Add(new ForecastProductInput
            {
                ProductId = product.Id,
                Name = product.Name,
                Brand = product.Brand,
                SaleUnit = product.SaleUnit,
                DecimalPlaces = product.DecimalPlaces,
                ReferenceCategory = category?.Name,
                IsDiscontinued = product.Lifecycle == LifecycleEnum.Discontinued,
                CurrentStock = variants.Sum(v => stockByVariant.GetValueOrDefault(v.Id)),
                IncomingStock = variants.Sum(v => incomingByVariant.GetValueOrDefault(v.Id)),
                MinStock = minStock,
                LeadTimeDays = leadTime,
                LeadTimeSource = leadSource,
                Price = price,
                CostPrice = cost,
                NextExpiry = nextExpiry,
                NextExpiryQuantity = nextExpiryQuantity,
                ShelfLifeDays = product.TracksLots ? product.ShelfLifeDays : null,
                TargetCoverageDays = product.TargetCoverageDays ?? policy?.TargetCoverageDays,
                SafetyDays = policy?.SafetyDays,
                AbcClass = abcClass,
                MinOrderQty = link?.IsPreferred == true ? link.MinOrderQty : null,
                OrderMultiple = link?.IsPreferred == true ? link.OrderMultiple : null,
                ActivePromotionDiscountPercent = promotion?.DiscountPercent,
                ActivePromotionEndDate = promotion?.EndDate,
                MonthlySales = series.TryGetValue(product.Id, out var productSeries) ? productSeries : new decimal[window],
                MonthsOfHistory = MonthsOfHistory(product.Id),
                ProductSeasonalIndex = seasonalityRows.TryGetValue(product.Id, out var rows) ? ToIndex(rows.Select(r => (r.Month, r.SeasonalIndex))) : null,
                CategorySeasonalIndex = category != null && categoryIndexes.TryGetValue(category.Id, out var categoryIndex) ? categoryIndex : null,
                RevenueLast12Months = revenue12.GetValueOrDefault(product.Id),
                LinkedEvents = [.. product.Events.Where(e => e.Event.IsActive).Select(e => e.Event.Name).Order()]
            });
        }

        // "Transferir" (RN-DF16): posição de cada variação por local e a participação de cada loja nas vendas recentes.
        var positions = (await balanceRepository.GetLocationPositions(ct))
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<LocationPositionRowDto>)[.. g]);
        var soldByStore = await orderRepository.GetPaidQuantityByProductAndStore(clock.StartOfDayUtc(today.AddDays(-StoreShareWindowDays)), ct);
        var soldByProduct = soldByStore.GroupBy(x => x.Key.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
        var shares = soldByStore.ToDictionary(
            x => x.Key,
            x => soldByProduct[x.Key.ProductId] > 0 ? x.Value / soldByProduct[x.Key.ProductId] : 0m);

        // "A caminho" por loja (RN-CP05): a divisão da compra entre as lojas desconta o que já vem para cada uma (RN-DF27).
        var allProductIds = inputs.Select(i => i.ProductId).ToList();
        var incomingByStore = new Dictionary<(Guid ProductId, Guid StoreId), decimal>();
        foreach (var storeId in positions.Values.SelectMany(rows => rows).Select(r => r.StoreId).Distinct())
            foreach (var (productId, quantity) in await purchaseItemRepository.SumIncomingByProduct(allProductIds, storeId, ct))
                incomingByStore[(productId, storeId)] = quantity;

        return new ForecastSourceData
        {
            IncomingByStore = incomingByStore,
            Inputs = inputs,
            VariantHistory = variantHistory,
            DecimalPlaces = decimalPlaces,
            AbcClasses = abcClasses,
            CalendarEvents = calendarEvents,
            LocationPositions = positions,
            StoreShares = shares,
        };
    }

    /// <summary>Doze valores com janeiro primeiro; índice incompleto (menos de 12 meses) não vale (RN-DF03).</summary>
    private static decimal[]? ToIndex(IEnumerable<(short Month, decimal Index)> rows)
    {
        var byMonth = rows.ToDictionary(r => r.Month, r => r.Index);
        return byMonth.Count == 12 ? [.. Enumerable.Range(1, 12).Select(m => byMonth[(short)m])] : null;
    }

    /// <summary>
    /// Índice sazonal por categoria (RN-DF03): o mesmo cálculo do produto (F3.1) sobre a soma das vendas dos produtos da categoria.
    /// Devolve também a categoria de referência de cada produto: a primeira ativa em ordem alfabética, sem a guarda-chuva.
    /// </summary>
    private Dictionary<Guid, decimal[]> CategoryIndexes(
        List<Product> products,
        Dictionary<Guid, decimal[]> series,
        DateOnly today,
        out Dictionary<Guid, Category> referenceCategory)
    {
        var categoriesByProduct = products.ToDictionary(
            p => p.Id,
            p => p.Categories
                .Where(link => link.Category is { IsActive: true })
                .Select(link => link.Category)
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());

        var withSales = products.Where(p => series.TryGetValue(p.Id, out var s) && s.Any(q => q > 0)).Select(p => p.Id).ToHashSet();
        var umbrella = categoriesByProduct
            .Where(kv => withSales.Contains(kv.Key))
            .SelectMany(kv => kv.Value.Select(c => c.Id))
            .GroupBy(id => id)
            .Where(g => withSales.Count >= UmbrellaCategoryMinProducts && g.Count() >= UmbrellaCategoryShare * withSales.Count)
            .Select(g => g.Key)
            .ToHashSet();

        referenceCategory = categoriesByProduct
            .Select(kv => (kv.Key, Category: kv.Value.FirstOrDefault(c => !umbrella.Contains(c.Id)) ?? kv.Value.FirstOrDefault()))
            .Where(x => x.Category != null)
            .ToDictionary(x => x.Key, x => x.Category!);

        var window = ForecastCalculationContext.HistoryWindowMonths;
        var calendar = new ForecastCalculationContext(today, 1);
        var monthly = new List<ProductSalesMonthlyDto>();
        foreach (var category in referenceCategory.Values.DistinctBy(c => c.Id))
        {
            var sum = new decimal[window];
            foreach (var member in categoriesByProduct.Where(kv => kv.Value.Any(c => c.Id == category.Id)).Select(kv => kv.Key))
            {
                if (!series.TryGetValue(member, out var values))
                    continue;
                for (var i = 0; i < window; i++)
                    sum[i] += values[i];
            }

            for (var i = 0; i < window; i++)
            {
                if (sum[i] <= 0)
                    continue;
                var month = calendar.MonthAt(i);
                monthly.Add(new ProductSalesMonthlyDto(category.Id, (short)month.Year, (short)month.Month, sum[i], 0, 0, 0, 0, 0));
            }
        }

        return DerivedDataCalculator.Seasonality(monthly, today)
            .GroupBy(s => s.ProductId)
            .Select(g => (g.Key, Index: ToIndex(g.Select(s => (s.Month, s.SeasonalIndex)))))
            .Where(x => x.Index != null)
            .ToDictionary(x => x.Key, x => x.Index!);
    }

    /// <summary>Média do prazo real dos últimos recebimentos de cada fornecedor, em dias (1 a 180).</summary>
    private async Task<Dictionary<Guid, int>> RealLeadTimes(DateOnly today, CancellationToken ct)
    {
        var samples = await purchaseOrderRepository.GetReceivedLeadTimes(clock.StartOfDayUtc(today.AddYears(-2)), ct);
        return samples
            .GroupBy(s => s.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => (int)Math.Clamp(
                    Math.Round(g.OrderByDescending(s => s.ReceivedAt).Take(LeadTimeSamplesPerSupplier).Average(s => s.Days), MidpointRounding.AwayFromZero),
                    1, 180));
    }

    /// <summary>Prazo efetivo: média real do fornecedor, senão o do vínculo produto × fornecedor, senão o do cadastro do fornecedor (T-DF3).</summary>
    private static (int Days, LeadTimeSourceEnum Source) EffectiveLeadTime(ProductSupplier? link, Dictionary<Guid, int> realLeadTimes)
    {
        if (link == null)
            return (DefaultLeadTimeDays, LeadTimeSourceEnum.Supplier);

        if (realLeadTimes.TryGetValue(link.SupplierId, out var real))
            return (real, LeadTimeSourceEnum.History);

        return link.LeadTimeDays is > 0
            ? (link.LeadTimeDays.Value, LeadTimeSourceEnum.ProductSupplier)
            : (Math.Max(1, link.Supplier.LeadTimeDays), LeadTimeSourceEnum.Supplier);
    }
}

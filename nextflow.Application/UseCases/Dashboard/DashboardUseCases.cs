using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Forecast;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Dashboard;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;

namespace Nextflow.Application.UseCases.Dashboard;

/// <summary>Apoio do painel: limites de mês no fuso de negócio e o rótulo das variações do top 5.</summary>
internal static class DashboardReading
{
    public const int TopItems = 5;

    public static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    public static decimal? ChangePercent(decimal current, decimal reference)
        => reference > 0 ? Math.Round((current - reference) / reference * 100m, 1, MidpointRounding.AwayFromZero) : null;

    public static async Task<Dictionary<Guid, string?>> VariantLabels(IProductVariantRepository variantRepository, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];
        var variants = await variantRepository.GetAll(v => ids.Contains(v.Id), 0, int.MaxValue,
            q => q.Include(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute), ct: ct);
        return variants.ToDictionary(v => v.Id, v => ProductVariantResponseDto.LabelOf(v) is { Length: > 0 } label ? label : null);
    }
}

/// <summary>
/// KPIs do mês corrente (T-DF1), da loja ou da empresa: faturamento, vendas, ticket médio e margem bruta (receita − custo médio
/// congelado nos itens). As comparações usam o mesmo trecho do mês (dia 1 até hoje) no mês anterior e no mesmo mês do ano anterior,
/// para não comparar um mês pela metade com um mês cheio.
/// </summary>
public class GetSalesKpisUseCase(
    IOrderRepository orderRepository,
    IBusinessClock clock,
    IResultFactory<SalesKpisDto> resultFactory) : IGetSalesKpisUseCase
{
    public async Task<ResultDto<SalesKpisDto>> Execute(Guid? storeId, CancellationToken ct = default)
    {
        var today = clock.Today;
        var first = DashboardReading.FirstOfMonth(today);
        var elapsedDays = today.Day;

        async Task<List<PaidOrderTotalDto>> Period(DateOnly start)
        {
            var end = start.AddDays(elapsedDays);
            var monthEnd = start.AddMonths(1);
            return await orderRepository.GetPaidOrderTotals(storeId, clock.StartOfDayUtc(start), clock.StartOfDayUtc(end < monthEnd ? end : monthEnd), ct);
        }

        var current = await Period(first);
        var lastMonth = await Period(first.AddMonths(-1));
        var lastYear = await Period(first.AddYears(-1));

        var revenue = current.Sum(o => o.Total);
        var margin = revenue - current.Sum(o => o.Cost);
        return resultFactory.Success(new SalesKpisDto
        {
            Month = first.ToString("yyyy-MM"),
            Revenue = revenue,
            OrderCount = current.Count,
            AverageTicket = current.Count > 0 ? Math.Round(revenue / current.Count, 2, MidpointRounding.AwayFromZero) : 0,
            GrossMargin = margin,
            GrossMarginPercent = revenue > 0 ? Math.Round(margin / revenue * 100m, 1, MidpointRounding.AwayFromZero) : 0,
            RevenueVsLastMonthPercent = DashboardReading.ChangePercent(revenue, lastMonth.Sum(o => o.Total)),
            RevenueVsLastYearPercent = DashboardReading.ChangePercent(revenue, lastYear.Sum(o => o.Total)),
        });
    }
}

/// <summary>Bloco "Estoque baixo" (T-DF1): total de variações × locais abaixo do mínimo (RN-CE33) e as 5 mais críticas.</summary>
public class GetLowStockSummaryUseCase(
    IStockBalanceRepository balanceRepository,
    IProductVariantRepository variantRepository,
    IResultFactory<LowStockSummaryDto> resultFactory) : IGetLowStockSummaryUseCase
{
    public async Task<ResultDto<LowStockSummaryDto>> Execute(Guid? storeId, CancellationToken ct = default)
    {
        var (count, top) = await balanceRepository.GetLowStock(storeId, DashboardReading.TopItems, ct);
        var labels = await DashboardReading.VariantLabels(variantRepository, [.. top.Select(r => r.VariantId)], ct);
        return resultFactory.Success(new LowStockSummaryDto
        {
            Count = count,
            Items = [.. top.Select(r => new LowStockItemDto
            {
                ProductId = r.ProductId,
                ProductName = r.ProductName,
                VariantId = r.VariantId,
                VariantLabel = labels.GetValueOrDefault(r.VariantId),
                LocationName = r.LocationName,
                Available = r.Available,
                MinQuantity = r.MinQuantity,
            })],
        });
    }
}

/// <summary>Bloco "Lotes vencendo" (T-DF1): lotes × locais com saldo que vencem de hoje até N dias (padrão 30), valor a custo médio.</summary>
public class GetExpiringLotsSummaryUseCase(
    IStockBalanceRepository balanceRepository,
    IProductVariantRepository variantRepository,
    IBusinessClock clock,
    IResultFactory<ExpiringLotsSummaryDto> resultFactory) : IGetExpiringLotsSummaryUseCase
{
    public const int DefaultDays = 30;

    public async Task<ResultDto<ExpiringLotsSummaryDto>> Execute(Guid? storeId, int days, CancellationToken ct = default)
    {
        days = days is < 1 or > 365 ? DefaultDays : days;
        var today = clock.Today;
        var rows = await balanceRepository.GetExpiringLots(today, today.AddDays(days), storeId, ct);
        var top = rows.OrderBy(r => r.ExpiresOn).ThenBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase).Take(DashboardReading.TopItems).ToList();
        var labels = await DashboardReading.VariantLabels(variantRepository, [.. top.Select(r => r.VariantId).Distinct()], ct);

        return resultFactory.Success(new ExpiringLotsSummaryDto
        {
            Count = rows.Count,
            TotalCostValue = Math.Round(rows.Sum(r => r.Quantity * (r.UnitCost ?? 0m)), 2, MidpointRounding.AwayFromZero),
            Items = [.. top.Select(r => new ExpiringLotItemDto
            {
                ProductName = r.ProductName,
                VariantLabel = labels.GetValueOrDefault(r.VariantId),
                LotCode = r.LotCode,
                LocationName = r.LocationName,
                ExpiresOn = r.ExpiresOn,
                DaysToExpire = r.ExpiresOn.DayNumber - today.DayNumber,
                Quantity = r.Quantity,
            })],
        });
    }
}

/// <summary>Bloco "Próximos eventos" (T-DF1): eventos ativos nos próximos N dias (padrão 90), com a data em que a venda começa a subir.</summary>
public class GetUpcomingEventsUseCase(
    ICalendarEventRepository eventRepository,
    IBusinessClock clock,
    IResultFactory<List<UpcomingEventDto>> resultFactory) : IGetUpcomingEventsUseCase
{
    public const int DefaultDays = 90;

    public async Task<ResultDto<List<UpcomingEventDto>>> Execute(int days, CancellationToken ct = default)
    {
        days = days is < 1 or > 366 ? DefaultDays : days;
        var today = clock.Today;
        var until = today.AddDays(days);
        var events = await eventRepository.GetAll(e => e.IsActive, 0, int.MaxValue, q => q.Include(e => e.Products), ct: ct);

        var upcoming = new List<UpcomingEventDto>();
        foreach (var calendarEvent in events)
        {
            var next = new[] { calendarEvent.DateIn(today.Year), calendarEvent.DateIn(today.Year + 1) }
                .Where(d => d >= today && d <= until)
                .DefaultIfEmpty()
                .Min();
            if (next == default)
                continue;
            upcoming.Add(new UpcomingEventDto
            {
                EventId = calendarEvent.Id,
                Name = calendarEvent.Name,
                NextDate = next,
                DemandStartsOn = next.AddDays(-calendarEvent.LeadDays),
                ProductCount = calendarEvent.Products.Count,
            });
        }

        return resultFactory.Success([.. upcoming.OrderBy(e => e.NextDate).ThenBy(e => e.Name, StringComparer.Ordinal)]);
    }
}

/// <summary>Gráfico do painel (T-DF1): faturamento dos últimos 12 meses (o corrente incluso, até hoje) × os mesmos meses do ano anterior.</summary>
public class GetSalesChartUseCase(
    IOrderRepository orderRepository,
    IBusinessClock clock,
    IResultFactory<SalesChartDto> resultFactory) : IGetSalesChartUseCase
{
    public const int Months = 12;

    public async Task<ResultDto<SalesChartDto>> Execute(Guid? storeId, CancellationToken ct = default)
    {
        var first = DashboardReading.FirstOfMonth(clock.Today);
        var start = first.AddMonths(-(Months - 1) - 12);
        var orders = await orderRepository.GetPaidOrderTotals(storeId, clock.StartOfDayUtc(start), clock.StartOfDayUtc(first.AddMonths(1)), ct);
        var byMonth = orders
            .GroupBy(o => DashboardReading.FirstOfMonth(clock.ToBusinessDate(o.PaidAt)))
            .ToDictionary(g => g.Key, g => g.Sum(o => o.Total));

        return resultFactory.Success(new SalesChartDto
        {
            Months = [.. Enumerable.Range(0, Months).Select(i =>
            {
                var month = first.AddMonths(i - (Months - 1));
                return new SalesChartMonthDto
                {
                    Month = month.ToString("yyyy-MM"),
                    Revenue = byMonth.GetValueOrDefault(month),
                    RevenueLastYear = byMonth.TryGetValue(month.AddYears(-1), out var lastYear) ? lastYear : null,
                };
            })],
        });
    }
}

/// <summary>
/// Card "Recomendações" (T-DF1): a última análise e até N (padrão 5) pendentes de ação (Comprar, Promover, Transferir) por
/// prioridade. Manter não aparece: não pede ação.
/// </summary>
public class GetForecastTopUseCase(
    IForecastRunRepository runRepository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastSettingsRepository settingsRepository,
    IFileStorage fileStorage,
    IBusinessClock clock,
    IResultFactory<ForecastTopDto> resultFactory) : IGetForecastTopUseCase
{
    public const int DefaultLimit = 5;

    public async Task<ResultDto<ForecastTopDto>> Execute(int limit, CancellationToken ct = default)
    {
        limit = limit is < 1 or > 20 ? DefaultLimit : limit;
        var latest = (await runRepository.GetAll(_ => true, 0, 1, ForecastRunReading.Include(),
            q => q.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.CreatedAt), ct)).FirstOrDefault();
        if (latest is null)
            return resultFactory.Success(new ForecastTopDto());

        var top = await recommendationRepository.GetAll(
            r => r.Status == RecommendationStatusEnum.Pending && r.Action != RecommendationActionEnum.Hold, 0, limit,
            ForecastRecommendationReading.ListInclude,
            q => q.OrderByDescending(r => r.Priority).ThenBy(r => r.Deadline).ThenBy(r => r.Product.Name), ct);

        return resultFactory.Success(new ForecastTopDto
        {
            Run = await ForecastRunReading.SummaryAsync(latest, recommendationRepository, settingsRepository, clock, ct),
            Recommendations = [.. top.Select(r => new Nextflow.Domain.Dtos.Forecasts.ForecastRecommendationListItemDto(r, fileStorage.GetPublicUrl(r.Product?.ImageKey)))],
        });
    }
}

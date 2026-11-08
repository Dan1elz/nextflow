using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Monta a previsão de um produto (T-DF3, T-DF5) só com o que já está gravado: dados derivados da F3.1, projeções da última análise
/// concluída que projetou o produto, sazonalidade, eventos, saldo e precisão. Só leitura; nada é recalculado aqui (ADR 0005).
/// </summary>
public class ProductForecastReader(
    IProductSalesMonthlyRepository salesMonthlyRepository,
    IProductSeasonalityRepository seasonalityRepository,
    IForecastProjectionRepository projectionRepository,
    IForecastRecommendationRepository recommendationRepository,
    ICalendarEventRepository eventRepository,
    IStockBalanceRepository balanceRepository,
    ForecastAccuracyReader accuracyReader,
    IBusinessClock clock)
{
    public const int DefaultMonths = 24;
    public const int MaxMonths = 36;

    public async Task<ProductForecastDto> LoadAsync(Product product, int months, CancellationToken ct)
    {
        months = Math.Clamp(months <= 0 ? DefaultMonths : months, 1, MaxMonths);
        var today = clock.Today;
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var start = firstOfMonth.AddMonths(-months);

        // Histórico: meses completos (RN-DF02), do mais antigo ao mais recente; mês sem linha vem zerado.
        var (startYear, startMonth) = ((short)start.Year, (short)start.Month);
        var (endYear, endMonth) = ((short)firstOfMonth.Year, (short)firstOfMonth.Month);
        var productId = product.Id;
        var rows = (await salesMonthlyRepository.GetAll(
                r => r.ProductId == productId
                    && (r.Year > startYear || (r.Year == startYear && r.Month >= startMonth))
                    && (r.Year < endYear || (r.Year == endYear && r.Month < endMonth)),
                0, int.MaxValue, ct: ct))
            .ToDictionary(r => (r.Year, r.Month));
        var history = Enumerable.Range(0, months).Select(i =>
        {
            var month = start.AddMonths(i);
            rows.TryGetValue(((short)month.Year, (short)month.Month), out var row);
            return new ProductForecastHistoryDto
            {
                Month = month.ToString("yyyy-MM"),
                QuantitySold = row?.QuantitySold ?? 0,
                QuantitySoldOnPromo = row?.QuantitySoldOnPromo ?? 0,
                DaysOutOfStock = row?.DaysOutOfStock ?? 0,
                LostDemandQty = row?.LostDemandQty ?? 0,
                AvgPrice = row is { QuantitySold: > 0 } ? row.AvgPrice : null,
                Revenue = row?.Revenue ?? 0,
            };
        }).ToList();

        // Projeção da análise concluída mais recente que projetou o produto.
        var projections = (await projectionRepository.GetAll(
                p => p.ProductId == productId
                    && (p.ForecastRun.Status == ForecastRunStatusEnum.Completed || p.ForecastRun.Status == ForecastRunStatusEnum.CompletedWithoutAi),
                0, 60,
                q => q.Include(p => p.ForecastRun),
                q => q.OrderByDescending(p => p.ForecastRun.StartedAt).ThenBy(p => p.Year).ThenBy(p => p.Month), ct))
            .ToList();
        var latestRunId = projections.FirstOrDefault()?.ForecastRunId;
        var projection = projections
            .Where(p => p.ForecastRunId == latestRunId)
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .Select(p => new ProductForecastProjectionDto { Month = $"{p.Year:0000}-{p.Month:00}", ProjectedQuantity = p.ProjectedQuantity })
            .ToList();

        var seasonality = (await seasonalityRepository.GetAll(s => s.ProductId == productId, 0, 12, ct: ct)).ToDictionary(s => (int)s.Month);

        var latestRecommendation = (await recommendationRepository.GetAll(
                r => r.ProductId == productId, 0, 1,
                q => q.Include(r => r.ForecastRun),
                q => q.OrderByDescending(r => r.ForecastRun.StartedAt), ct))
            .FirstOrDefault();

        var linked = product.Events.Select(e => e.EventId).ToHashSet();
        var events = new List<ProductForecastEventDto>();
        foreach (var calendarEvent in await eventRepository.GetAll(e => e.IsActive, 0, int.MaxValue, ct: ct))
        {
            var date = new[] { calendarEvent.DateIn(today.Year), calendarEvent.DateIn(today.Year + 1) }
                .Where(d => d >= today && d <= today.AddYears(1))
                .DefaultIfEmpty()
                .Min();
            if (date == default)
                continue;
            events.Add(new ProductForecastEventDto
            {
                Name = calendarEvent.Name,
                Date = date,
                DemandStartsOn = date.AddDays(-calendarEvent.LeadDays),
                LinkedToProduct = linked.Contains(calendarEvent.Id),
            });
        }

        var stock = await balanceRepository.SumByProduct([productId], null, ct);

        // Precisão (RN-DF19, F3.6c): projeção de referência × vendido nos meses completos da mesma janela do histórico.
        var accuracy = await accuracyReader.LoadProductAsync(productId, accuracyReader.Window(months), ct);

        return new ProductForecastDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            SaleUnit = product.SaleUnit.GetApiName(),
            DecimalPlaces = product.DecimalPlaces,
            History = history,
            Projection = projection,
            Seasonality = [.. Enumerable.Range(1, 12).Select(m => new ProductForecastSeasonalityDto
            {
                Month = m,
                ProductIndex = seasonality.TryGetValue(m, out var s) ? s.SeasonalIndex : null,
            })],
            MonthsOfHistory = latestRecommendation?.MonthsOfHistory ?? seasonality.Values.FirstOrDefault()?.MonthsOfHistory ?? 0,
            Events = [.. events.OrderBy(e => e.Date).ThenBy(e => e.Name, StringComparer.Ordinal)],
            CurrentStock = stock.GetValueOrDefault(productId),
            Accuracy = [.. accuracy.Select(m => new ForecastAccuracyItemDto
            {
                Month = $"{m.Month.Year:0000}-{m.Month.Month:00}",
                Projected = m.Projected,
                Actual = m.Actual,
                ErrorPercent = m.ErrorPercent,
            })],
            LatestRecommendationId = latestRecommendation?.Id,
        };
    }
}

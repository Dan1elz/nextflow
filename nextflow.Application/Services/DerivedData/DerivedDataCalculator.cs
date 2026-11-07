using Nextflow.Domain.Dtos.DerivedData;

namespace Nextflow.Application.Services.DerivedData;

/// <summary>
/// Cálculo puro dos dados derivados da F3.1 (sem banco, sem relógio): recebe vendas, movimentações e a função que converte
/// UTC em dia de negócio, e devolve as linhas das três tabelas. Fácil de testar e de explicar na banca.
/// </summary>
public static class DerivedDataCalculator
{
    /// <summary>Limite do índice sazonal (RN-DF03).</summary>
    public const decimal MinSeasonalIndex = 0.05m;
    public const decimal MaxSeasonalIndex = 5m;

    /// <summary>Um produto só ganha índice sazonal com 12 meses ou mais de histórico; o cálculo usa no máximo 24 (RN-DF03).</summary>
    public const int MinHistoryMonths = 12;
    public const int MaxHistoryMonths = 24;

    /// <summary>
    /// Saldo de fechamento por dia, de cada variação × local, desde a primeira movimentação até hoje. Dia sem movimento repete o
    /// saldo do dia anterior; o saldo é a soma acumulada das quantidades com sinal (o livro-razão, RN-CE01).
    /// </summary>
    public static List<StockDailySnapshotDto> DailySnapshots(
        IReadOnlyList<MovementDeltaDto> deltas, Func<DateTime, DateOnly> toBusinessDate, DateOnly today)
    {
        var rows = new List<StockDailySnapshotDto>();

        foreach (var group in deltas.GroupBy(d => (d.VariantId, d.LocationId)))
        {
            var perDay = group
                .GroupBy(d => toBusinessDate(d.AtUtc))
                .Where(g => g.Key <= today)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));
            if (perDay.Count == 0)
                continue;

            var running = 0m;
            for (var day = perDay.Keys.Min(); day <= today; day = day.AddDays(1))
            {
                if (perDay.TryGetValue(day, out var delta))
                    running += delta;
                rows.Add(new StockDailySnapshotDto(group.Key.VariantId, group.Key.LocationId, day, running));
            }
        }

        return rows;
    }

    /// <summary>
    /// Vendas por produto e mês de negócio, com a ruptura do mesmo mês. Dia sem estoque do produto é o dia em que a soma do
    /// fechamento das suas variações nos locais vendáveis é zero (ou menos). Demanda perdida = dias sem estoque × média diária de
    /// venda dos dias com estoque do mês; mês sem venda usa a média diária do último mês que vendeu (RN-CE24).
    /// </summary>
    public static List<ProductSalesMonthlyDto> MonthlySales(
        IReadOnlyList<SaleLineDto> lines,
        IReadOnlyList<StockDailySnapshotDto> snapshots,
        IReadOnlyDictionary<Guid, Guid> productByVariant,
        IReadOnlySet<Guid> sellableLocationIds,
        Func<DateTime, DateOnly> toBusinessDate)
    {
        var sold = lines
            .GroupBy(l =>
            {
                var day = toBusinessDate(l.SoldAtUtc);
                return (l.ProductId, day.Year, day.Month);
            })
            .ToDictionary(
                g => g.Key,
                g => (Quantity: g.Sum(l => l.Quantity),
                      Promo: g.Where(l => l.OnPromotion).Sum(l => l.Quantity),
                      Revenue: g.Sum(l => l.LineTotal)));

        var stock = StockDaysByMonth(snapshots, productByVariant, sellableLocationIds);

        var keys = sold.Keys.Union(stock.Keys)
            .OrderBy(k => k.ProductId).ThenBy(k => k.Year).ThenBy(k => k.Month)
            .ToList();

        var rows = new List<ProductSalesMonthlyDto>(keys.Count);
        var lastDailyByProduct = new Dictionary<Guid, decimal>();

        foreach (var key in keys)
        {
            sold.TryGetValue(key, out var sale);
            stock.TryGetValue(key, out var days);

            var daysInMonth = DateTime.DaysInMonth(key.Year, key.Month);
            var dailyAverage = sale.Quantity > 0
                ? sale.Quantity / (days.WithStock > 0 ? days.WithStock : daysInMonth)
                : lastDailyByProduct.GetValueOrDefault(key.ProductId);
            if (sale.Quantity > 0)
                lastDailyByProduct[key.ProductId] = dailyAverage;

            rows.Add(new ProductSalesMonthlyDto(
                key.ProductId,
                (short)key.Year,
                (short)key.Month,
                Math.Round(sale.Quantity, 3),
                Math.Round(sale.Promo, 3),
                (short)days.OutOfStock,
                Math.Round(days.OutOfStock * dailyAverage, 3),
                sale.Quantity > 0 ? Math.Round(sale.Revenue / sale.Quantity, 2) : 0m,
                Math.Round(sale.Revenue, 2)));
        }

        return rows;
    }

    /// <summary>
    /// Índice sazonal por produto e mês do calendário (RN-DF03, spec §5.2): média do mês-calendário ÷ média mensal geral, com mês
    /// sem venda valendo zero. Só entra produto com 12 a 24 meses completos desde a primeira venda (o mês corrente fica fora).
    /// </summary>
    public static List<ProductSeasonalityDto> Seasonality(IReadOnlyList<ProductSalesMonthlyDto> monthly, DateOnly today)
    {
        var lastCompleteIndex = MonthIndex(today.Year, today.Month) - 1;
        var rows = new List<ProductSeasonalityDto>();

        foreach (var product in monthly.GroupBy(m => m.ProductId))
        {
            var quantityByMonth = product
                .Where(m => MonthIndex(m.Year, m.Month) <= lastCompleteIndex)
                .ToDictionary(m => MonthIndex(m.Year, m.Month), m => m.QuantitySold);

            var firstSale = quantityByMonth.Where(p => p.Value > 0).Select(p => p.Key).DefaultIfEmpty(-1).Min();
            if (firstSale < 0)
                continue;

            var start = Math.Max(firstSale, lastCompleteIndex - MaxHistoryMonths + 1);
            var months = lastCompleteIndex - start + 1;
            if (months < MinHistoryMonths)
                continue;

            var window = Enumerable.Range(start, months)
                .Select(index => (CalendarMonth: index % 12 + 1, Quantity: quantityByMonth.GetValueOrDefault(index)))
                .ToList();
            var overallAverage = window.Average(w => w.Quantity);
            if (overallAverage == 0)
                continue;

            for (var calendarMonth = 1; calendarMonth <= 12; calendarMonth++)
            {
                var sameMonth = window.Where(w => w.CalendarMonth == calendarMonth).ToList();
                if (sameMonth.Count == 0)
                    continue;

                var index = Math.Clamp(sameMonth.Average(w => w.Quantity) / overallAverage, MinSeasonalIndex, MaxSeasonalIndex);
                rows.Add(new ProductSeasonalityDto(product.Key, (short)calendarMonth, Math.Round(index, 4), (short)months));
            }
        }

        return rows;
    }

    private static int MonthIndex(int year, int month) => year * 12 + month - 1;

    private static Dictionary<(Guid ProductId, int Year, int Month), (int OutOfStock, int WithStock)> StockDaysByMonth(
        IReadOnlyList<StockDailySnapshotDto> snapshots,
        IReadOnlyDictionary<Guid, Guid> productByVariant,
        IReadOnlySet<Guid> sellableLocationIds)
        => snapshots
            .Where(s => sellableLocationIds.Contains(s.LocationId) && productByVariant.ContainsKey(s.VariantId))
            .GroupBy(s => (ProductId: productByVariant[s.VariantId], s.Day))
            .Select(g => (g.Key.ProductId, g.Key.Day, Closing: g.Sum(s => s.ClosingQuantity)))
            .GroupBy(d => (d.ProductId, d.Day.Year, d.Day.Month))
            .ToDictionary(
                g => g.Key,
                g => (OutOfStock: g.Count(d => d.Closing <= 0), WithStock: g.Count(d => d.Closing > 0)));
}

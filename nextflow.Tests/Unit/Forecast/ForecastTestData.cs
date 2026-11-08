using Nextflow.Application.Forecast;
using Nextflow.Application.Services.DerivedData;
using Nextflow.Domain.Dtos.DerivedData;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Montagem de entradas do cálculo: séries mensais por função e índice sazonal pelo mesmo cálculo da F3.1.</summary>
internal static class ForecastTestData
{
    public static readonly DateOnly Today = new(2026, 10, 15);

    public static ForecastCalculationContext Context(int horizonMonths = 3, DateOnly? today = null)
        => new(today ?? Today, horizonMonths);

    /// <summary>24 meses completos antes de <paramref name="today"/>, do mais antigo ao mais recente, com a quantidade dada por (ano, mês).</summary>
    public static decimal[] Series(Func<int, int, decimal> quantityOf, DateOnly? today = null)
    {
        var ctx = Context(1, today);
        return [.. Enumerable.Range(0, ForecastCalculationContext.HistoryWindowMonths).Select(i =>
        {
            var month = ctx.MonthAt(i);
            return quantityOf(month.Year, month.Month);
        })];
    }

    public static decimal[] Flat(decimal quantity, DateOnly? today = null) => Series((_, _) => quantity, today);

    /// <summary>Índice sazonal do produto como a F3.1 grava em <c>product_seasonality</c> (12 valores, janeiro primeiro), ou nulo.</summary>
    public static decimal[]? IndexOf(decimal[] series, DateOnly? today = null)
    {
        var date = today ?? Today;
        var ctx = Context(1, date);
        var productId = Guid.NewGuid();
        var rows = series
            .Select((quantity, i) => (Month: ctx.MonthAt(i), Quantity: quantity))
            .Where(x => x.Quantity > 0)
            .Select(x => new ProductSalesMonthlyDto(productId, (short)x.Month.Year, (short)x.Month.Month, x.Quantity, 0, 0, 0, 0, 0))
            .ToList();

        var seasonality = DerivedDataCalculator.Seasonality(rows, date);
        return seasonality.Count == 12 ? [.. seasonality.OrderBy(s => s.Month).Select(s => s.SeasonalIndex)] : null;
    }

    public static ForecastProductInput Input(
        decimal[]? series = null,
        int monthsOfHistory = 24,
        decimal stock = 0,
        decimal incoming = 0,
        decimal minStock = 0,
        int leadTime = 10,
        decimal price = 10m,
        decimal? cost = 5m,
        decimal[]? productIndex = null,
        string name = "Produto",
        Action<ForecastProductInputBuilder>? configure = null)
    {
        var builder = new ForecastProductInputBuilder
        {
            Name = name,
            Series = series ?? Flat(100),
            MonthsOfHistory = monthsOfHistory,
            Stock = stock,
            Incoming = incoming,
            MinStock = minStock,
            LeadTime = leadTime,
            Price = price,
            Cost = cost,
            ProductIndex = productIndex
        };
        configure?.Invoke(builder);

        return new ForecastProductInput
        {
            ProductId = Guid.NewGuid(),
            Name = builder.Name,
            MonthlySales = builder.Series,
            MonthsOfHistory = builder.MonthsOfHistory,
            CurrentStock = builder.Stock,
            IncomingStock = builder.Incoming,
            MinStock = builder.MinStock,
            LeadTimeDays = builder.LeadTime,
            Price = builder.Price,
            CostPrice = builder.Cost,
            ProductSeasonalIndex = builder.ProductIndex,
            IsDiscontinued = builder.Discontinued,
            TargetCoverageDays = builder.TargetCoverageDays,
            MinOrderQty = builder.MinOrderQty,
            OrderMultiple = builder.OrderMultiple,
            ShelfLifeDays = builder.ShelfLifeDays,
            NextExpiry = builder.NextExpiry,
            NextExpiryQuantity = builder.NextExpiryQuantity,
            ActivePromotionDiscountPercent = builder.ActivePromotionDiscount,
            RevenueLast12Months = builder.Revenue,
            DecimalPlaces = builder.DecimalPlaces
        };
    }
}

/// <summary>Campos que os testes mexem; o resto fica no padrão.</summary>
internal sealed class ForecastProductInputBuilder
{
    public string Name { get; set; } = "Produto";
    public decimal[] Series { get; set; } = [];
    public int MonthsOfHistory { get; set; }
    public decimal Stock { get; set; }
    public decimal Incoming { get; set; }
    public decimal MinStock { get; set; }
    public int LeadTime { get; set; }
    public decimal Price { get; set; }
    public decimal? Cost { get; set; }
    public decimal[]? ProductIndex { get; set; }
    public bool Discontinued { get; set; }
    public int? TargetCoverageDays { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? OrderMultiple { get; set; }
    public int? ShelfLifeDays { get; set; }
    public DateOnly? NextExpiry { get; set; }
    public decimal NextExpiryQuantity { get; set; }
    public decimal? ActivePromotionDiscount { get; set; }
    public decimal Revenue { get; set; }
    public int DecimalPlaces { get; set; }
}

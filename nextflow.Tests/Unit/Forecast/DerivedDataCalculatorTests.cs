using Nextflow.Application.Services.DerivedData;
using Nextflow.Domain.Dtos.DerivedData;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Cálculo puro dos dados derivados (F3.1): sem banco e com o dia de negócio igual ao dia UTC, para o teste ser exato.</summary>
public class DerivedDataCalculatorTests
{
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Variant = Guid.NewGuid();
    private static readonly Guid Shop = Guid.NewGuid();
    private static readonly Guid Warehouse = Guid.NewGuid();

    private static readonly IReadOnlyDictionary<Guid, Guid> ProductByVariant = new Dictionary<Guid, Guid> { [Variant] = Product };
    private static readonly IReadOnlySet<Guid> Sellable = new HashSet<Guid> { Shop };

    private static DateOnly ToDate(DateTime utc) => DateOnly.FromDateTime(utc);

    private static DateTime At(int year, int month, int day) => new(year, month, day, 12, 0, 0, DateTimeKind.Utc);

    private static SaleLineDto Sale(DateTime at, decimal quantity, bool promo = false, decimal? total = null)
        => new(Product, at, quantity, promo, total ?? quantity * 10m);

    private static MovementDeltaDto Delta(DateTime at, decimal quantity, Guid? location = null)
        => new(Variant, location ?? Shop, at, quantity);

    private static List<ProductSalesMonthlyDto> Monthly(
        IReadOnlyList<SaleLineDto> sales, IReadOnlyList<MovementDeltaDto> deltas, DateOnly today)
    {
        var snapshots = DerivedDataCalculator.DailySnapshots(deltas, ToDate, today);
        return DerivedDataCalculator.MonthlySales(sales, snapshots, ProductByVariant, Sellable, ToDate);
    }

    [Fact]
    [Trait("Rule", "RN-DF02")]
    public void Should_AggregateMonthlySales_When_OrdersArePaid()
    {
        var sales = new[]
        {
            Sale(At(2026, 9, 3), 2, total: 20m),
            Sale(At(2026, 9, 28), 3, total: 36m),
            Sale(At(2026, 10, 1), 4, total: 40m),
        };
        var deltas = new[] { Delta(At(2026, 9, 1), 100) };

        var rows = Monthly(sales, deltas, new DateOnly(2026, 10, 15));

        var september = rows.Single(r => r.Year == 2026 && r.Month == 9);
        september.QuantitySold.ShouldBe(5m);
        september.Revenue.ShouldBe(56m);
        september.AvgPrice.ShouldBe(11.2m);
        rows.Single(r => r.Year == 2026 && r.Month == 10).QuantitySold.ShouldBe(4m);
    }

    [Fact]
    [Trait("Rule", "RN-CE25")]
    public void Should_SeparatePromoQuantity_When_ItemHasPromotion()
    {
        var sales = new[] { Sale(At(2026, 9, 3), 2), Sale(At(2026, 9, 4), 3, promo: true) };

        var september = Monthly(sales, [Delta(At(2026, 9, 1), 100)], new DateOnly(2026, 9, 30)).Single();

        september.QuantitySold.ShouldBe(5m);
        september.QuantitySoldOnPromo.ShouldBe(3m);
    }

    [Fact]
    [Trait("Rule", "RN-CE14")]
    public void Should_ClosePreviousBalance_When_DayHasNoMovement()
    {
        var deltas = new[] { Delta(At(2026, 9, 1), 10), Delta(At(2026, 9, 3), -4) };

        var snapshots = DerivedDataCalculator.DailySnapshots(deltas, ToDate, new DateOnly(2026, 9, 5));

        snapshots.Select(s => (s.Day.Day, s.ClosingQuantity))
            .ShouldBe([(1, 10m), (2, 10m), (3, 6m), (4, 6m), (5, 6m)]);
    }

    [Fact]
    [Trait("Rule", "RN-CE24")]
    public void Should_CountOutOfStockDays_When_ClosingIsZero()
    {
        // 10 un. entram dia 1 e saem dia 5: dias 5 a 10 ficam em zero (6 dias). Estoque só no depósito não conta como vendável.
        var deltas = new[]
        {
            Delta(At(2026, 9, 1), 10),
            Delta(At(2026, 9, 5), -10),
            Delta(At(2026, 9, 1), 50, Warehouse),
        };

        var september = Monthly([], deltas, new DateOnly(2026, 9, 10)).Single();

        september.DaysOutOfStock.ShouldBe((short)6);
    }

    [Fact]
    [Trait("Rule", "RN-CE24")]
    public void Should_ComputeLostDemand_When_DaysOutOfStock()
    {
        // Vendeu 8 un. em 4 dias com estoque (média 2/dia); 6 dias em zero → 12 un. perdidas.
        var sales = new[] { Sale(At(2026, 9, 2), 8) };
        var deltas = new[] { Delta(At(2026, 9, 1), 8), Delta(At(2026, 9, 5), -8) };

        var september = Monthly(sales, deltas, new DateOnly(2026, 9, 10)).Single();

        september.DaysOutOfStock.ShouldBe((short)6);
        september.LostDemandQty.ShouldBe(12m);
    }

    private static List<ProductSalesMonthlyDto> Series(Func<int, decimal> quantityOfMonthIndex, int months, int firstYear = 2024, int firstMonth = 1)
        => Enumerable.Range(0, months)
            .Select(i =>
            {
                var date = new DateOnly(firstYear, firstMonth, 1).AddMonths(i);
                return new ProductSalesMonthlyDto(Product, (short)date.Year, (short)date.Month, quantityOfMonthIndex(i), 0m, 0, 0m, 10m, quantityOfMonthIndex(i) * 10m);
            })
            .ToList();

    [Fact]
    [Trait("Rule", "RN-DF03")]
    public void Should_ClampSeasonalIndex_When_Computing()
    {
        // 24 meses (jan/2024 a dez/2025), mas só dezembro vende: o histórico conta desde a primeira venda (dez/2024, 13 meses),
        // o índice de dezembro estoura o teto e os demais meses (0) ficam no piso.
        var monthly = Series(i => i % 12 == 11 ? 120m : 0m, 24);

        var rows = DerivedDataCalculator.Seasonality(monthly, new DateOnly(2026, 1, 20));

        rows.Count.ShouldBe(12);
        rows.Single(r => r.Month == 12).SeasonalIndex.ShouldBe(DerivedDataCalculator.MaxSeasonalIndex);
        rows.Where(r => r.Month != 12).ShouldAllBe(r => r.SeasonalIndex == DerivedDataCalculator.MinSeasonalIndex);
        rows.ShouldAllBe(r => r.MonthsOfHistory == 13);
    }

    [Fact]
    [Trait("Rule", "RN-DF03")]
    public void Should_SkipSeasonality_When_LessThan12Months()
    {
        // 11 meses completos desde a primeira venda (fev a dez/2025, com hoje em jan/2026 = 11 meses).
        var monthly = Series(_ => 10m, 11, firstYear: 2025, firstMonth: 2);

        DerivedDataCalculator.Seasonality(monthly, new DateOnly(2026, 1, 20)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-DF03")]
    public void Should_ReturnFlatIndex_When_SalesAreConstant()
    {
        var monthly = Series(_ => 10m, 12, firstYear: 2025, firstMonth: 1);

        var rows = DerivedDataCalculator.Seasonality(monthly, new DateOnly(2026, 1, 20));

        rows.Count.ShouldBe(12);
        rows.ShouldAllBe(r => r.SeasonalIndex == 1m);
    }

    [Fact]
    [Trait("Rule", "RN-CE24")]
    public void Should_NotCountOutOfStock_When_OtherSellableVariantHasStock()
    {
        // A variação A zera no dia 5, mas a B (mesmo produto, local vendável) segue com 5 un.: o produto nunca fica sem estoque.
        var variantB = Guid.NewGuid();
        var productByVariant = new Dictionary<Guid, Guid> { [Variant] = Product, [variantB] = Product };
        var deltas = new[]
        {
            Delta(At(2026, 9, 1), 10),
            Delta(At(2026, 9, 5), -10),
            new MovementDeltaDto(variantB, Shop, At(2026, 9, 1), 5),
        };
        var today = new DateOnly(2026, 9, 10);

        var snapshots = DerivedDataCalculator.DailySnapshots(deltas, ToDate, today);
        var september = DerivedDataCalculator.MonthlySales([], snapshots, productByVariant, Sellable, ToDate).Single();

        september.DaysOutOfStock.ShouldBe((short)0);
    }

    [Fact]
    [Trait("Rule", "RN-DF03")]
    public void Should_ExcludeCurrentMonth_When_ComputingSeasonality()
    {
        // 13 meses com venda (jan/2025 a jan/2026); hoje é 20/01/2026, então janeiro/2026 está em curso e fica de fora: 12 meses.
        var monthly = Series(_ => 10m, 13, firstYear: 2025, firstMonth: 1);

        var rows = DerivedDataCalculator.Seasonality(monthly, new DateOnly(2026, 1, 20));

        rows.Count.ShouldBe(12);
        rows.ShouldAllBe(r => r.MonthsOfHistory == 12);
    }
}

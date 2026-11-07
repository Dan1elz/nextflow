using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Application.Services;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Dados derivados da previsão (F3.1) num Postgres real: o seed de histórico (mesma simulação dos 36 meses do dev, só com janela
/// menor) roda o <c>DerivedDataSeeder</c> no startup, e as tabelas precisam bater com as vendas e o livro-razão.
/// </summary>
[Collection(DemoSeedCollection.Name)]
public class DerivedDataSeedTests(DemoSeedFixture fixture)
{
    private const int Months = 2;

    /// <summary>Mais de 12 meses completos, para a sazonalidade (RN-DF03) ter produto elegível.</summary>
    private const int MonthsForSeasonality = 14;

    private async Task<(string Connection, IServiceProvider Services)> SeededDatabase(int months = Months)
    {
        var connection = await fixture.NewDatabaseAsync();
        var services = fixture.StartApi(connection, demoEnabled: true, historyMonths: months);
        return (connection, services);
    }

    [Fact]
    [Trait("Rule", "RN-DF02")]
    public async Task Should_MatchSeed_When_RebuildingAfterDemoHistory()
    {
        var (connection, _) = await SeededDatabase();
        await using var context = fixture.CreateContext(connection);
        var clock = new BusinessClock();

        // Vendas: soma por produto e mês de negócio = itens de pedidos Pagos; receita = LineTotal.
        var items = await context.Set<OrderItem>()
            .Where(i => i.Order.Status == OrderStatusEnum.Paid)
            .Select(i => new { i.Variant.ProductId, PaidAt = i.Order.PaidAt!.Value, i.Quantity, i.LineTotal, Promo = i.PromotionId != null })
            .ToListAsync();
        items.ShouldNotBeEmpty();

        var expected = items
            .GroupBy(i => (i.ProductId, clock.ToBusinessDate(i.PaidAt).Year, clock.ToBusinessDate(i.PaidAt).Month))
            .ToDictionary(g => g.Key, g => (Quantity: g.Sum(i => i.Quantity), Revenue: g.Sum(i => i.LineTotal), Promo: g.Where(i => i.Promo).Sum(i => i.Quantity)));

        var monthly = await context.Set<ProductSalesMonthly>().ToListAsync();
        foreach (var (key, value) in expected)
        {
            var row = monthly.Single(m => m.ProductId == key.ProductId && m.Year == key.Item2 && m.Month == key.Item3);
            row.QuantitySold.ShouldBe(value.Quantity);
            row.Revenue.ShouldBe(value.Revenue);
            row.QuantitySoldOnPromo.ShouldBe(value.Promo);
        }
        monthly.Sum(m => m.QuantitySold).ShouldBe(items.Sum(i => i.Quantity));

        // Saldo diário: o último dia fecha igual ao saldo atual da variação × local (somando lotes).
        var balances = (await context.Set<StockBalance>()
                .GroupBy(b => new { b.VariantId, b.LocationId })
                .Select(g => new { g.Key.VariantId, g.Key.LocationId, Quantity = g.Sum(b => b.Quantity) })
                .ToListAsync())
            .ToDictionary(b => (b.VariantId, b.LocationId), b => b.Quantity);
        var today = clock.Today;
        var lastDay = (await context.Set<StockDailySnapshot>().Where(s => s.Day == today).ToListAsync())
            .ToDictionary(s => (s.VariantId, s.LocationId), s => s.ClosingQuantity);

        lastDay.Count.ShouldBe(balances.Count);
        foreach (var (key, quantity) in balances)
            lastDay[key].ShouldBe(quantity);
    }

    private static async Task<(List<string> Monthly, List<string> Snapshots, List<string> Seasonality)> Fingerprint(Nextflow.Infrastructure.Database.AppDbContext context)
        => (
            (await context.Set<ProductSalesMonthly>().ToListAsync())
                .Select(m => $"{m.ProductId}|{m.Year}|{m.Month}|{m.QuantitySold}|{m.QuantitySoldOnPromo}|{m.DaysOutOfStock}|{m.LostDemandQty}|{m.AvgPrice}|{m.Revenue}")
                .Order().ToList(),
            (await context.Set<StockDailySnapshot>().ToListAsync())
                .Select(s => $"{s.VariantId}|{s.LocationId}|{s.Day:yyyy-MM-dd}|{s.ClosingQuantity}")
                .Order().ToList(),
            (await context.Set<ProductSeasonality>().ToListAsync())
                .Select(p => $"{p.ProductId}|{p.Month}|{p.SeasonalIndex}|{p.MonthsOfHistory}")
                .Order().ToList());

    [Fact]
    [Trait("Rule", "RN-DF02")]
    public async Task Should_BeIdempotent_When_RebuildingTwice()
    {
        var (connection, services) = await SeededDatabase();
        await using var context = fixture.CreateContext(connection);

        var before = await Fingerprint(context);
        before.Monthly.ShouldNotBeEmpty();
        before.Snapshots.ShouldNotBeEmpty();

        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IDerivedDataService>().RebuildAsync(CancellationToken.None);

        // Mesmas linhas, com os mesmos valores (não só a mesma contagem).
        await using var after = fixture.CreateContext(connection);
        var rebuilt = await Fingerprint(after);
        rebuilt.Monthly.ShouldBe(before.Monthly);
        rebuilt.Snapshots.ShouldBe(before.Snapshots);
        rebuilt.Seasonality.ShouldBe(before.Seasonality);
    }

    [Fact]
    [Trait("Rule", "RN-CE14")]
    public async Task Should_NotTouchLedger_When_Rebuilding()
    {
        var (connection, services) = await SeededDatabase();
        await using var context = fixture.CreateContext(connection);

        var movementsBefore = (await context.Set<StockMovement>().CountAsync(), await context.Set<StockMovement>().SumAsync(m => m.Quantity));
        var balancesBefore = (await context.Set<StockBalance>().CountAsync(), await context.Set<StockBalance>().SumAsync(b => b.Quantity));

        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IDerivedDataService>().RebuildAsync(CancellationToken.None);

        await using var after = fixture.CreateContext(connection);
        (await after.Set<StockMovement>().CountAsync(), await after.Set<StockMovement>().SumAsync(m => m.Quantity)).ShouldBe(movementsBefore);
        (await after.Set<StockBalance>().CountAsync(), await after.Set<StockBalance>().SumAsync(b => b.Quantity)).ShouldBe(balancesBefore);
    }

    [Fact]
    [Trait("Rule", "RN-DF03")]
    public async Task Should_CreateSeasonality_When_HistoryHasMoreThan12Months()
    {
        var (connection, _) = await SeededDatabase(MonthsForSeasonality);
        await using var context = fixture.CreateContext(connection);

        var rows = await context.Set<ProductSeasonality>().ToListAsync();

        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(r => r.SeasonalIndex >= 0.05m && r.SeasonalIndex <= 5m);
        rows.ShouldAllBe(r => r.MonthsOfHistory >= 12 && r.MonthsOfHistory <= 24);
        rows.GroupBy(r => r.ProductId).ShouldAllBe(g => g.Count() <= 12 && g.Select(r => r.Month).Distinct().Count() == g.Count());
    }
}

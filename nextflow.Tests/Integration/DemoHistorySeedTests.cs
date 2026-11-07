using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Histórico de demonstração (F2.9) num Postgres real, com 2 meses: a simulação e o lançamento são os mesmos dos 36 meses do
/// dev e do staging, só a janela é menor.
/// </summary>
[Collection(DemoSeedCollection.Name)]
public class DemoHistorySeedTests(DemoSeedFixture fixture)
{
    private const int Months = 2;

    private async Task<string> SeededDatabase()
    {
        var connection = await fixture.NewDatabaseAsync();
        fixture.StartApi(connection, demoEnabled: true, historyMonths: Months);
        return connection;
    }

    [Fact]
    [Trait("Rule", "RN-CE01")]
    public async Task Should_MatchBalancesToMovements_When_HistorySeeded()
    {
        var connection = await SeededDatabase();
        await using var context = fixture.CreateContext(connection);

        var sums = (await context.Set<StockMovement>()
                .GroupBy(m => new { m.VariantId, m.LocationId, m.LotId })
                .Select(g => new
                {
                    g.Key.VariantId,
                    g.Key.LocationId,
                    g.Key.LotId,
                    Quantity = g.Sum(m => m.Quantity), // a quantidade já vem com o sinal do sentido
                })
                .ToListAsync())
            .ToDictionary(x => (x.VariantId, x.LocationId, x.LotId), x => x.Quantity);

        // Saldo por variação × local × lote (RN-CE09): o alimento tem um saldo por lote (F1.7), o resto um saldo sem lote.
        var balances = await context.Set<StockBalance>().ToListAsync();
        balances.Count.ShouldBe(sums.Count);
        foreach (var balance in balances)
        {
            balance.Quantity.ShouldBe(sums[(balance.VariantId, balance.LocationId, balance.LotId)]);
            balance.Quantity.ShouldBeGreaterThanOrEqualTo(0);
        }

        var foodVariantIds = await context.Set<ProductVariant>().Where(v => v.Product.TracksLots).Select(v => v.Id).ToListAsync();
        foodVariantIds.ShouldNotBeEmpty();
        balances.Where(b => foodVariantIds.Contains(b.VariantId)).ShouldAllBe(b => b.LotId != null);
        balances.Where(b => !foodVariantIds.Contains(b.VariantId)).ShouldAllBe(b => b.LotId == null);

        (await context.Set<StockMovement>().AnyAsync(m => m.Reason == MovementReasonEnum.Sale)).ShouldBeTrue();
        (await context.Set<StockMovement>().AnyAsync(m => m.Reason == MovementReasonEnum.Purchase)).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-DF02")]
    public async Task Should_CreatePaidSalesAndReceivedPurchasesInWindow_When_HistorySeeded()
    {
        var connection = await SeededDatabase();
        await using var context = fixture.CreateContext(connection);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-Months).AddDays(-1);
        var windowEnd = DateTime.UtcNow.AddDays(1);

        var orders = await context.Set<Order>().Include(o => o.Items).Include(o => o.Payments).ToListAsync();
        orders.ShouldNotBeEmpty();
        orders.ShouldAllBe(o => o.Status == OrderStatusEnum.Paid && o.Channel == OrderChannelEnum.Counter);
        orders.ShouldAllBe(o => o.PaidAt > windowStart && o.PaidAt < windowEnd && o.CreatedAt == o.PaidAt);
        orders.ShouldAllBe(o => o.Items.Count > 0 && o.Payments.Sum(p => p.Amount) == o.Total);

        // Cada item de venda sai em Out/Sale no salão, apontando para o pedido; o alimento pode sair em mais de um lote
        // (FEFO, F1.7), então a conta é a soma por pedido × variação.
        var saleMovements = await context.Set<StockMovement>().Where(m => m.Reason == MovementReasonEnum.Sale).ToListAsync();
        saleMovements.ShouldAllBe(m => m.Type == MovementTypeEnum.Out && m.SourceType == SourceTypeEnum.Order);
        saleMovements.Count.ShouldBeGreaterThanOrEqualTo(orders.Sum(o => o.Items.Count));
        var soldByOrderVariant = saleMovements.GroupBy(m => (m.SourceId, m.VariantId)).ToDictionary(g => g.Key, g => -g.Sum(m => m.Quantity));
        foreach (var item in orders.SelectMany(o => o.Items.Select(i => (OrderId: (Guid?)o.Id, i.VariantId, i.Quantity))))
            soldByOrderVariant[(item.OrderId, item.VariantId)].ShouldBe(item.Quantity);

        var purchases = await context.Set<PurchaseOrder>().Include(p => p.Items).ToListAsync();
        var received = purchases.Where(p => p.Status == PurchaseStatusEnum.Received).ToList();
        received.ShouldNotBeEmpty();
        purchases.ShouldAllBe(p => p.Status == PurchaseStatusEnum.Received || p.Status == PurchaseStatusEnum.Pending);
        received.ShouldAllBe(p => p.SentAt > windowStart && p.ReceivedAt > p.SentAt && p.ReceivedAt < windowEnd
            && p.RealLeadTimeDays > 0 && p.Items.All(i => i.AverageCostAfter > 0));

        var purchaseMovements = await context.Set<StockMovement>().Where(m => m.Reason == MovementReasonEnum.Purchase).ToListAsync();
        purchaseMovements.ShouldAllBe(m => m.Type == MovementTypeEnum.In && m.SourceType == SourceTypeEnum.PurchaseOrder && m.UnitCost > 0);
        purchaseMovements.Count.ShouldBe(received.Sum(p => p.Items.Count));

        // A abertura é o dia 0 do histórico, e nenhum movimento fica depois de agora.
        var movements = await context.Set<StockMovement>().ToListAsync();
        movements.ShouldAllBe(m => m.CreatedAt > windowStart && m.CreatedAt < windowEnd);
        movements.Where(m => m.Reason == MovementReasonEnum.Opening).Max(m => m.CreatedAt)
            .ShouldBeLessThan(movements.Where(m => m.Reason == MovementReasonEnum.Sale).Min(m => m.CreatedAt));
    }

    [Fact]
    public async Task Should_BeIdempotent_When_HistorySeedRunsTwice()
    {
        var connection = await SeededDatabase();
        var first = await CountTotals(connection);

        fixture.StartApi(connection, demoEnabled: true, historyMonths: Months);

        var second = await CountTotals(connection);
        second.ShouldBe(first);
        first.Orders.ShouldBeGreaterThan(0);
        first.Purchases.ShouldBeGreaterThan(0);
    }

    private sealed record Totals(int Orders, int Purchases, int Movements, decimal Revenue);

    private async Task<Totals> CountTotals(string connectionString)
    {
        await using var context = fixture.CreateContext(connectionString);
        return new Totals(
            await context.Set<Order>().CountAsync(),
            await context.Set<PurchaseOrder>().CountAsync(),
            await context.Set<StockMovement>().CountAsync(),
            await context.Set<Order>().SumAsync(o => o.Total));
    }
}

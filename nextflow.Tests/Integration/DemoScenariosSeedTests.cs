using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Seeders.Demo;

namespace Nextflow.Tests.Integration;

/// <summary>Casos de teste do seed demo (<see cref="DemoScenariosSeeder"/>) num Postgres real, com 2 meses de histórico.</summary>
[Collection(DemoSeedCollection.Name)]
public class DemoScenariosSeedTests(DemoSeedFixture fixture)
{
    private const int Months = 2;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));

    [Fact]
    public async Task Should_CreatePromotionsInEveryStatus_When_HistorySeeded()
    {
        var connection = await fixture.NewDatabaseAsync();
        fixture.StartApi(connection, demoEnabled: true, historyMonths: Months);
        await using var context = fixture.CreateContext(connection);

        var promotions = await context.Set<Promotion>().Where(p => p.Reason!.EndsWith(DemoScenariosSeeder.Tag)).ToListAsync();
        promotions.Select(p => p.StatusOn(Today)).Distinct().ShouldBe(
            [PromotionStatusEnum.Active, PromotionStatusEnum.Scheduled, PromotionStatusEnum.Ended, PromotionStatusEnum.Canceled],
            ignoreOrder: true);
        promotions.ShouldContain(p => p.VariantId != null && p.StatusOn(Today) == PromotionStatusEnum.Active);
        promotions.ShouldContain(p => p.VariantId == null && p.StatusOn(Today) == PromotionStatusEnum.Active);
    }

    [Fact]
    [Trait("Rule", "RN-CP07")]
    public async Task Should_CreatePurchaseOrdersOutsideHistoryFlow_When_HistorySeeded()
    {
        var connection = await fixture.NewDatabaseAsync();
        fixture.StartApi(connection, demoEnabled: true, historyMonths: Months);
        await using var context = fixture.CreateContext(connection);

        var orders = await context.Set<PurchaseOrder>().Include(p => p.Items)
            .Where(p => p.Note!.EndsWith(DemoScenariosSeeder.Tag)).ToListAsync();
        orders.Select(p => p.Status).ShouldBe(
            [PurchaseStatusEnum.Budget, PurchaseStatusEnum.Pending, PurchaseStatusEnum.Canceled, PurchaseStatusEnum.Received],
            ignoreOrder: true);
        orders.Single(p => p.Status == PurchaseStatusEnum.Pending).IsLate(Today).ShouldBeTrue();
        orders.Single(p => p.Status == PurchaseStatusEnum.Canceled).CancelReason.ShouldNotBeNullOrWhiteSpace();

        // O recebido entrou pelo livro-razão com lotes vencendo dentro de 30 dias, e o saldo deles está lá.
        var received = orders.Single(p => p.Status == PurchaseStatusEnum.Received);
        var movements = await context.Set<StockMovement>().Include(m => m.Lot)
            .Where(m => m.SourceId == received.Id && m.Reason == MovementReasonEnum.Purchase).ToListAsync();
        movements.Count.ShouldBe(received.Items.Count);
        movements.ShouldAllBe(m => m.Lot != null && m.Lot.ExpiresOn > Today && m.Lot.ExpiresOn <= Today.AddDays(30));
        var lotIds = movements.Select(m => m.LotId).ToList();
        (await context.Set<StockBalance>().Where(b => lotIds.Contains(b.LotId)).SumAsync(b => b.Quantity))
            .ShouldBe(received.Items.Sum(i => i.Quantity));
        received.Items.ShouldAllBe(i => i.AverageCostAfter > 0);
    }

    [Fact]
    public async Task Should_NotDuplicateScenarios_When_SeedRunsTwice()
    {
        var connection = await fixture.NewDatabaseAsync();
        fixture.StartApi(connection, demoEnabled: true, historyMonths: Months);
        var first = await CountScenarios(connection);

        fixture.StartApi(connection, demoEnabled: true, historyMonths: Months);

        (await CountScenarios(connection)).ShouldBe(first);
        first.ShouldBe((5, 4));
    }

    [Fact]
    public async Task Should_NotCreateScenarios_When_HistoryDisabled()
    {
        var connection = await fixture.NewDatabaseAsync();
        fixture.StartApi(connection, demoEnabled: true);

        (await CountScenarios(connection)).ShouldBe((0, 0));
    }

    private async Task<(int Promotions, int PurchaseOrders)> CountScenarios(string connectionString)
    {
        await using var context = fixture.CreateContext(connectionString);
        return (await context.Set<Promotion>().CountAsync(p => p.Reason!.EndsWith(DemoScenariosSeeder.Tag)),
            await context.Set<PurchaseOrder>().CountAsync(p => p.Note!.EndsWith(DemoScenariosSeeder.Tag)));
    }
}

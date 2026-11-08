using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Dtos.DerivedData;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Tests.Support;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Projeção de referência da precisão (F3.6b, RN-DF19) num Postgres real: catálogo demo sem histórico, relógio parado em 26/10/2026
/// e, para um produto, vendas mensais e análises montadas à mão. Para cada mês completo vale a projeção da última análise concluída
/// com data de referência antes do 1º dia do mês.
/// </summary>
[Collection(DemoSeedCollection.Name)]
public class ForecastAccuracyQueryTests(DemoSeedFixture fixture)
{
    private static readonly DateOnly Today = new(2026, 10, 26);

    private sealed record Seeded(IServiceProvider Services, Guid ProductId);

    private static Seeded? _seeded;

    /// <summary>
    /// Vendas de jul a out. Análise A concluída em 10/07 (projeta jul a out), B concluída sem IA em 20/08 (projeta set e out) e
    /// F com falha em 25/08 (projeta set). Esperado: jul fora (nenhuma análise antes de 01/07), ago de A, set de B, out fora (mês corrente).
    /// </summary>
    private async Task<Seeded> Seed()
    {
        if (_seeded is not null)
            return _seeded;

        var connection = await fixture.NewDatabaseAsync();
        var services = fixture.StartApi(connection, demoEnabled: true, configureServices: s =>
            s.AddSingleton<IBusinessClock>(new FixedBusinessClock(Today)));

        await using var context = fixture.CreateContext(connection);
        var productId = await context.Set<Product>().OrderBy(p => p.Name).Select(p => p.Id).FirstAsync();

        await context.Set<ProductSalesMonthly>().Where(s => s.ProductId == productId).ExecuteDeleteAsync();
        context.AddRange(
            Sales(productId, 7, 90m, 0),
            Sales(productId, 8, 100m, 3),
            Sales(productId, 9, 80m, 20),
            Sales(productId, 10, 40m, 0));

        AddRun(context, productId, new DateOnly(2026, 7, 10), ForecastRunStatusEnum.Completed, (7, 95m), (8, 110m), (9, 120m), (10, 130m));
        AddRun(context, productId, new DateOnly(2026, 8, 20), ForecastRunStatusEnum.CompletedWithoutAi, (9, 85m), (10, 50m));
        AddRun(context, productId, new DateOnly(2026, 8, 25), ForecastRunStatusEnum.Failed, (9, 999m));
        await context.SaveChangesAsync();

        return _seeded = new Seeded(services, productId);
    }

    private static ProductSalesMonthly Sales(Guid productId, short month, decimal sold, short daysOut)
        => new(new ProductSalesMonthlyDto(productId, 2026, month, sold, 0, daysOut, 0, 0, 0));

    private static void AddRun(DbContext context, Guid productId, DateOnly reference, ForecastRunStatusEnum status, params (short Month, decimal Qty)[] projections)
    {
        var started = reference.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        var run = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Scheduled, null), reference, started);
        if (status == ForecastRunStatusEnum.Failed)
            run.Fail("falhou", started.AddMinutes(1));
        else
            run.Complete(status, "ok", null, null, null, started.AddMinutes(1));

        context.Add(run);
        foreach (var (month, qty) in projections)
            context.Add(new ForecastProjection(new NewForecastProjectionDto(run.Id, productId, 2026, month, qty)));
    }

    private static async Task<IReadOnlyList<ForecastAccuracyRowDto>> Rows(Seeded seeded)
    {
        using var scope = seeded.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IForecastProjectionRepository>().GetAccuracyRows(Today);
        return rows.Where(r => r.ProductId == seeded.ProductId).OrderBy(r => r.Month).ToList();
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public async Task Should_LeaveMonthOut_When_NoAnalysisBeforeIt()
    {
        var rows = await Rows(await Seed());

        Assert.DoesNotContain(rows, r => r.Month == 7);
        var august = Assert.Single(rows, r => r.Month == 8);
        Assert.Equal((2026, 110m, 100m, 3), (august.Year, august.Projected, august.Actual, august.DaysOutOfStock));
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public async Task Should_UseLatestConcludedAnalysis_When_LaterOneFailed()
    {
        var rows = await Rows(await Seed());

        var september = Assert.Single(rows, r => r.Month == 9);
        Assert.Equal((85m, 80m, 20), (september.Projected, september.Actual, september.DaysOutOfStock));
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public async Task Should_LeaveCurrentMonthOut_When_ItIsNotComplete()
    {
        var rows = await Rows(await Seed());

        Assert.Equal([8, 9], rows.Select(r => (int)r.Month));
    }
}

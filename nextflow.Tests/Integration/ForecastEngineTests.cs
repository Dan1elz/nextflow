using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;
using Nextflow.Tests.Support;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Motor de previsão (F3.2) de ponta a ponta num Postgres real: o seed demo de 14 meses (relógio parado em 26/10/2026, para o teste
/// não depender do dia em que roda) alimenta as tabelas derivadas da F3.1 e o motor roda com a <b>IA fake</b> (nenhum teste chama a
/// IA real). O banco é semeado uma vez por classe; cada teste abre as próprias análises e só olha o que elas gravaram.
/// </summary>
[Collection(DemoSeedCollection.Name)]
public class ForecastEngineTests(DemoSeedFixture fixture)
{
    private const int HistoryMonths = 14;
    private const int AiBatchSize = 10;
    private static readonly DateOnly Anchor = new(2026, 10, 26);

    /// <summary>Itens de temporada do catálogo demo: ovos de Páscoa (DP-013 a 016) e kits de Dia das Mães e Namorados (DP-044 e 045).</summary>
    private static readonly string[] SeasonalCodes = ["DP-013", "DP-014", "DP-015", "DP-016", "DP-044", "DP-045"];

    private sealed record Seeded(string Connection, IServiceProvider Services, FakeAiClient Ai);

    private static Seeded? _seeded;

    private async Task<Seeded> Seed()
    {
        if (_seeded is not null)
        {
            _seeded.Ai.Reset();
            return _seeded;
        }

        var ai = new FakeAiClient();
        var connection = await fixture.NewDatabaseAsync();
        var services = fixture.StartApi(
            connection,
            demoEnabled: true,
            historyMonths: HistoryMonths,
            configureServices: s =>
            {
                s.AddSingleton<IBusinessClock>(new FixedBusinessClock(Anchor));
                s.AddSingleton(ai);
                s.AddSingleton<IAiClient>(ai);
            },
            settings: new Dictionary<string, string> { [Nextflow.Application.Services.Forecast.ForecastEngine.AiBatchSizeKey] = AiBatchSize.ToString() });
        return _seeded = new Seeded(connection, services, ai);
    }

    /// <summary>Abre uma análise (como a F3.3 fará) e roda o motor num escopo de DI próprio, como o job em segundo plano.</summary>
    private async Task<ForecastRunOutcomeDto> Run(Seeded seeded, ForecastTriggerEnum trigger = ForecastTriggerEnum.Manual)
    {
        using var scope = seeded.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IForecastRunRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IBusinessClock>();
        var admin = await scope.ServiceProvider.GetRequiredService<IUserRepository>().Get(u => u.Email == AccessApiFixture.AdminEmail);

        var run = new ForecastRun(new StartForecastRunDto(trigger, trigger == ForecastTriggerEnum.Manual ? admin!.Id : null), clock.Today, clock.UtcNow);
        await runs.AddAsync(run);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();

        return await scope.ServiceProvider.GetRequiredService<IForecastEngine>().RunAsync(run.Id, CancellationToken.None);
    }

    private static string CodeOf(Product product) => product.Variants.Select(v => v.Sku).Order().First()[..6];

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_RunEndToEnd_When_SeededWithFakeAi()
    {
        var seeded = await Seed();

        var outcome = await Run(seeded);

        await using var context = fixture.CreateContext(seeded.Connection);
        var run = await context.Set<ForecastRun>().SingleAsync(r => r.Id == outcome.RunId);
        var products = await context.Set<Product>().Include(p => p.Variants).Where(p => p.IsActive).ToListAsync();
        var recommendations = await context.Set<ForecastRecommendation>().Include(r => r.Items).Where(r => r.ForecastRunId == run.Id).ToListAsync();
        var projections = await context.Set<ForecastProjection>().Where(p => p.ForecastRunId == run.Id).ToListAsync();
        var batches = await context.Set<ForecastAiBatch>().Where(b => b.ForecastRunId == run.Id).OrderBy(b => b.BatchNumber).ToListAsync();

        // Run fechado, com horizonte automático (RN-DF17) e datas de negócio.
        run.Status.ShouldBe(ForecastRunStatusEnum.Completed);
        run.ReferenceDate.ShouldBe(Anchor);
        run.HorizonMonths.ShouldBe((short)ForecastHorizon.Automatic(recommendations.Max(r => r.LeadTimeDays)));
        run.HorizonMonths.ShouldBeInRange((short)4, (short)6);
        run.FinishedAt.ShouldNotBeNull();
        run.DurationMs.ShouldNotBeNull();
        run.AiProvider.ShouldBe("Fake");
        run.Summary.ShouldBe(FakeAiClient.SummaryText);
        run.ErrorMessage.ShouldBeNull();
        run.ProductsAnalyzed.ShouldBe(products.Count);

        // Uma recomendação por produto ativo, todas pendentes; confiança só pelos meses de histórico (RN-DF05; a IA não a altera).
        recommendations.Count.ShouldBe(products.Count);
        recommendations.Select(r => r.ProductId).Distinct().Count().ShouldBe(products.Count);
        recommendations.ShouldAllBe(r => r.Status == RecommendationStatusEnum.Pending);
        recommendations.ShouldAllBe(r => r.Confidence == (r.MonthsOfHistory < 6 ? ForecastConfidenceEnum.Low : r.MonthsOfHistory < 18 ? ForecastConfidenceEnum.Medium : ForecastConfidenceEnum.High));
        recommendations.ShouldAllBe(r => r.Reason != "" && r.ReasonCalculated != "");
        recommendations.ShouldContain(r => r.Action == RecommendationActionEnum.Buy);

        // Projeções por produto com venda: uma por mês do horizonte, a partir do mês seguinte (L-DF2).
        var withSales = projections.GroupBy(p => p.ProductId).ToList();
        withSales.ShouldNotBeEmpty();
        withSales.ShouldAllBe(g => g.Count() == run.HorizonMonths);
        withSales.First().OrderBy(p => p.Year).ThenBy(p => p.Month).First().ShouldSatisfyAllConditions(
            p => p.Year.ShouldBe((short)2026),
            p => p.Month.ShouldBe((short)11));

        // Lotes (RN-DF20): sequenciais, de até N, todos concluídos; a IA fake foi chamada uma vez por lote e uma no resumo.
        run.ProductsSentToAi.ShouldBe(withSales.Count);
        batches.Count.ShouldBe((int)Math.Ceiling(withSales.Count / (double)AiBatchSize));
        run.BatchesTotal.ShouldBe(batches.Count);
        run.BatchesCompleted.ShouldBe(batches.Count);
        run.BatchesFailed.ShouldBe(0);
        batches.ShouldAllBe(b => b.Status == AiBatchStatusEnum.Completed && b.ProductsCount <= AiBatchSize && b.ProductIds.Length == b.ProductsCount);
        batches.SelectMany(b => b.ProductIds).Distinct().Count().ShouldBe(withSales.Count);
        seeded.Ai.Calls.ShouldBe([.. batches.Select(b => $"batch:{b.BatchNumber}"), "summary"]);
        seeded.Ai.MaxConcurrentCalls.ShouldBe(1);

        // Classe ABC recalculada (RN-CE29).
        products = await context.Set<Product>().Include(p => p.Variants).Where(p => p.IsActive).ToListAsync();
        products.ShouldAllBe(p => p.AbcClass != null);
        products.ShouldContain(p => p.AbcClass == AbcClassEnum.A);

        outcome.Status.ShouldBe(ForecastRunStatusEnum.Completed);
        outcome.Buy.ShouldBe(recommendations.Count(r => r.Action == RecommendationActionEnum.Buy));
        outcome.Hold.ShouldBe(recommendations.Count(r => r.Action == RecommendationActionEnum.Hold));
    }

    [Fact]
    [Trait("Rule", "RN-DF13")]
    public async Task Should_UseOnlyCalculation_When_AiIsNotConfigured()
    {
        var seeded = await Seed();
        seeded.Ai.Configured = false;

        var outcome = await Run(seeded);

        await using var context = fixture.CreateContext(seeded.Connection);
        var run = await context.Set<ForecastRun>().SingleAsync(r => r.Id == outcome.RunId);
        var recommendations = await context.Set<ForecastRecommendation>().Where(r => r.ForecastRunId == run.Id).ToListAsync();

        run.Status.ShouldBe(ForecastRunStatusEnum.CompletedWithoutAi);
        run.ErrorMessage!.ShouldContain("IA não configurada");
        run.AiProvider.ShouldBeNull();
        run.BatchesTotal.ShouldBe(0);
        run.ProductsSentToAi.ShouldBe(0);
        (await context.Set<ForecastAiBatch>().CountAsync(b => b.ForecastRunId == run.Id)).ShouldBe(0);
        seeded.Ai.Calls.ShouldBeEmpty();
        recommendations.ShouldNotBeEmpty();
        recommendations.ShouldAllBe(r => !r.AiAdjusted && r.Reason == r.ReasonCalculated);
        run.Summary!.ShouldContain("cálculo estatístico");
    }

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_ContinueAnalysis_When_OneBatchFails()
    {
        var seeded = await Seed();
        seeded.Ai.FailBatches.Add(2);

        var outcome = await Run(seeded);

        await using var context = fixture.CreateContext(seeded.Connection);
        var run = await context.Set<ForecastRun>().SingleAsync(r => r.Id == outcome.RunId);
        var batches = await context.Set<ForecastAiBatch>().Where(b => b.ForecastRunId == run.Id).OrderBy(b => b.BatchNumber).ToListAsync();
        var recommendations = await context.Set<ForecastRecommendation>().Where(r => r.ForecastRunId == run.Id).ToDictionaryAsync(r => r.ProductId);

        // A análise não cai: o lote 2 fica Failed e seus produtos seguem com o cálculo; os outros lotes têm o motivo da IA.
        run.Status.ShouldBe(ForecastRunStatusEnum.Completed);
        run.BatchesFailed.ShouldBe(1);
        run.BatchesCompleted.ShouldBe(batches.Count - 1);
        run.ErrorMessage!.ShouldContain("Lote 2");
        batches[1].Status.ShouldBe(AiBatchStatusEnum.Failed);
        batches[1].ErrorMessage.ShouldNotBeNullOrEmpty();
        batches.Where(b => b.BatchNumber != 2).ShouldAllBe(b => b.Status == AiBatchStatusEnum.Completed);
        batches[1].ProductIds.ShouldAllBe(id => recommendations[id].Reason == recommendations[id].ReasonCalculated);
        batches.Where(b => b.BatchNumber != 2).SelectMany(b => b.ProductIds).ShouldAllBe(id => recommendations[id].Reason.StartsWith("IA:"));
        seeded.Ai.Calls.Count(c => c.StartsWith("batch:")).ShouldBe(batches.Count); // o lote 3 saiu depois da falha do 2
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public async Task Should_NotRecommendBuy_When_SeasonalItemIsOutOfSeason()
    {
        var seeded = await Seed();

        var outcome = await Run(seeded);

        await using var context = fixture.CreateContext(seeded.Connection);
        var recommendations = await context.Set<ForecastRecommendation>()
            .Include(r => r.Product).ThenInclude(p => p.Variants)
            .Where(r => r.ForecastRunId == outcome.RunId)
            .ToListAsync();

        // Ovos de Páscoa e kits de Dia das Mães e Namorados em 26/10: nenhum vira Comprar, e muito menos Comprar de prioridade alta.
        var seasonal = recommendations.Where(r => SeasonalCodes.Contains(CodeOf(r.Product))).ToList();
        seasonal.Count.ShouldBe(SeasonalCodes.Length);
        seasonal.Where(r => r.Action == RecommendationActionEnum.Buy).Select(r => CodeOf(r.Product) + " | " + r.Reason + " | need=" + r.SuggestedQuantity + " stock=" + r.CurrentStock + " safety=" + r.SafetyStock + " hist=" + r.MonthsOfHistory + " idx=" + r.NextMonthSeasonalIndex + " demand=" + r.ProjectedDemand).ShouldBeEmpty();
        recommendations.Where(r => r.Action == RecommendationActionEnum.Buy && r.Priority == RecommendationPriorityEnum.High)
            .ShouldAllBe(r => !SeasonalCodes.Contains(CodeOf(r.Product)));
    }

    [Fact]
    [Trait("Rule", "RN-DF14")]
    public async Task Should_ExpirePreviousPending_When_NewRunCompletes()
    {
        var seeded = await Seed();
        var first = await Run(seeded);
        var second = await Run(seeded, ForecastTriggerEnum.Scheduled);

        await using var context = fixture.CreateContext(seeded.Connection);
        var oldOnes = await context.Set<ForecastRecommendation>().Where(r => r.ForecastRunId == first.RunId).ToListAsync();
        var newOnes = await context.Set<ForecastRecommendation>().Where(r => r.ForecastRunId == second.RunId).ToListAsync();

        oldOnes.ShouldNotBeEmpty();
        oldOnes.ShouldAllBe(r => r.Status == RecommendationStatusEnum.Expired);
        newOnes.ShouldAllBe(r => r.Status == RecommendationStatusEnum.Pending);
        (await context.Set<ForecastRecommendation>().CountAsync(r => r.Status == RecommendationStatusEnum.Pending && r.ForecastRunId != second.RunId)).ShouldBe(0);
        (await context.Set<ForecastRun>().SingleAsync(r => r.Id == second.RunId)).Trigger.ShouldBe(ForecastTriggerEnum.Scheduled);
    }

    [Fact]
    [Trait("Rule", "RN-DF21")]
    public async Task Should_SplitBuyAcrossVariants_When_ProductHasGrid()
    {
        var seeded = await Seed();

        var outcome = await Run(seeded);

        await using var context = fixture.CreateContext(seeded.Connection);
        var buys = await context.Set<ForecastRecommendation>()
            .Include(r => r.Items)
            .Include(r => r.Product).ThenInclude(p => p.Variants)
            .Where(r => r.ForecastRunId == outcome.RunId && r.Action == RecommendationActionEnum.Buy)
            .ToListAsync();

        buys.ShouldNotBeEmpty();
        foreach (var buy in buys)
        {
            var variantIds = buy.Product.Variants.Where(v => v.IsActive).Select(v => v.Id).ToHashSet();
            buy.Items.Select(i => i.VariantId).ToHashSet().ShouldBe(variantIds, ignoreOrder: true);
            buy.Items.Sum(i => i.Quantity).ShouldBe(buy.SuggestedQuantity!.Value);
            buy.Items.Sum(i => i.SharePercent).ShouldBeInRange(99.9m, 100.1m);
        }

        buys.ShouldContain(b => b.Product.Variants.Count > 1, "ao menos um Comprar de produto com grade (rateio de verdade)");

        // Só Comprar tem itens.
        (await context.Set<ForecastRecommendationItem>().CountAsync(i => i.Recommendation.ForecastRunId == outcome.RunId && i.Recommendation.Action != RecommendationActionEnum.Buy)).ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "RN-CE14")]
    public async Task Should_NotTouchLedger_When_Running()
    {
        var seeded = await Seed();
        await using var before = fixture.CreateContext(seeded.Connection);
        var movements = (await before.Set<StockMovement>().CountAsync(), await before.Set<StockMovement>().SumAsync(m => m.Quantity));
        var balances = (await before.Set<StockBalance>().CountAsync(), await before.Set<StockBalance>().SumAsync(b => b.Quantity));

        await Run(seeded);

        await using var after = fixture.CreateContext(seeded.Connection);
        (await after.Set<StockMovement>().CountAsync(), await after.Set<StockMovement>().SumAsync(m => m.Quantity)).ShouldBe(movements);
        (await after.Set<StockBalance>().CountAsync(), await after.Set<StockBalance>().SumAsync(b => b.Quantity)).ShouldBe(balances);
    }

    [Fact]
    public async Task Should_RefuseToRun_When_RunAlreadyFinished()
    {
        var seeded = await Seed();
        var outcome = await Run(seeded);

        using var scope = seeded.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IForecastEngine>();

        await Should.ThrowAsync<InvalidOperationException>(() => engine.RunAsync(outcome.RunId, CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => engine.RunAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    [Trait("Rule", "RN-DF23")]
    public async Task Should_CloseAsFailed_When_AnalysisIsCanceled()
    {
        var seeded = await Seed();
        using var cts = new CancellationTokenSource();
        // O host para no meio do lote 1: o cancelamento chega com a análise em andamento.
        seeded.Ai.BatchResponse = (_, _) =>
        {
            cts.Cancel();
            return null;
        };

        Guid runId;
        using (var scope = seeded.Services.CreateScope())
        {
            var clock = scope.ServiceProvider.GetRequiredService<IBusinessClock>();
            var run = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Scheduled, null), clock.Today, clock.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IForecastRunRepository>().AddAsync(run);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            runId = run.Id;

            await Should.ThrowAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<IForecastEngine>().RunAsync(runId, cts.Token));
        }

        await using var context = fixture.CreateContext(seeded.Connection);
        var canceled = await context.Set<ForecastRun>().SingleAsync(r => r.Id == runId);
        canceled.Status.ShouldBe(ForecastRunStatusEnum.Failed);
        canceled.ErrorMessage!.ShouldContain("cancelada");
        canceled.FinishedAt.ShouldNotBeNull();
        (await context.Set<ForecastRecommendation>().CountAsync(r => r.ForecastRunId == runId)).ShouldBe(0);

        // O índice único de análise em andamento não fica preso: a próxima abre e conclui.
        seeded.Ai.Reset();
        (await Run(seeded)).Status.ShouldBe(ForecastRunStatusEnum.Completed);
    }

    [Fact]
    [Trait("Rule", "RN-DF23")]
    public async Task Should_RejectSecondRunningAnalysis_When_OneIsInProgress()
    {
        var seeded = await Seed();
        using var scope = seeded.Services.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IBusinessClock>();
        var runs = scope.ServiceProvider.GetRequiredService<IForecastRunRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var first = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Manual, null), clock.Today, clock.UtcNow);
        await runs.AddAsync(first);
        await unitOfWork.CommitAsync();

        await runs.AddAsync(new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Scheduled, null), clock.Today, clock.UtcNow));
        await Should.ThrowAsync<DbUpdateException>(() => unitOfWork.CommitAsync());

        // Limpa para os outros testes da classe: a primeira roda e fecha.
        unitOfWork.Rollback();
        (await scope.ServiceProvider.GetRequiredService<IForecastEngine>().RunAsync(first.Id, CancellationToken.None)).Status.ShouldBe(ForecastRunStatusEnum.Completed);
    }
}

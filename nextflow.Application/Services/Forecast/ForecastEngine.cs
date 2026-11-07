using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Motor de previsão (F3.2). Lê os dados pelo <see cref="ForecastInputBuilder"/>, calcula (<see cref="ForecastCalculator"/>), pede
/// à IA os lotes em sequência (<see cref="ForecastAiOrchestrator"/>), aplica os guardrails e grava o resultado.
/// <para>
/// <b>Commits:</b> o progresso dos lotes é gravado a cada passo (lote enviado, concluído ou com falha), para a F3.3 mostrar "Lote 3 de 8"
/// com a análise ainda em andamento. As recomendações, as projeções, a expiração das pendentes da análise anterior (RN-DF14) e o
/// fechamento da análise entram <b>juntos num único commit final</b>, dentro de <c>ExecuteInTransactionAsync</c>: ou a análise nova
/// aparece inteira, ou nada dela aparece. Quem chama é um job em segundo plano, com escopo de DI próprio (um <c>DbContext</c> por análise).
/// </para>
/// <para>
/// O dia de negócio é o <c>ReferenceDate</c> da análise (gravado por quem a criou a partir do <c>IBusinessClock</c>); os carimbos de
/// início e fim também vêm do relógio de negócio.
/// </para>
/// </summary>
public class ForecastEngine(
    ForecastInputBuilder inputBuilder,
    ForecastAiOrchestrator aiOrchestrator,
    IForecastRunRepository runRepository,
    IForecastAiBatchRepository batchRepository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastProjectionRepository projectionRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    IBusinessClock clock,
    IConfiguration configuration,
    ILogger<ForecastEngine> logger) : IForecastEngine
{
    /// <summary>Chave opcional do tamanho do lote da IA; o padrão é o N inicial de 40 (RN-DF20).</summary>
    public const string AiBatchSizeKey = "Forecast:AiBatchSize";

    private const int MaxErrorLength = 1000;

    private int AiBatchSize => int.TryParse(configuration[AiBatchSizeKey], out var size) && size > 0 ? size : ForecastAiOrchestrator.DefaultBatchSize;

    public async Task<ForecastRunOutcomeDto> RunAsync(Guid runId, CancellationToken ct)
    {
        var run = await runRepository.GetByIdForUpdate(runId, ct: ct)
            ?? throw new InvalidOperationException($"Análise {runId} não encontrada.");
        if (!run.IsRunning)
            throw new InvalidOperationException($"A análise {runId} já terminou ({run.Status.GetApiName()}) e não pode rodar de novo.");

        try
        {
            return await ExecuteAsync(run, ct);
        }
        catch (OperationCanceledException)
        {
            // Cancelada no meio (parada do host, por exemplo): o run não pode ficar Running, porque o índice único
            // ux_forecast_runs_running (RN-DF23) bloquearia a próxima análise. Fecha como Failed e devolve o cancelamento.
            logger.LogWarning("Previsão {RunId}: análise cancelada antes de terminar.", runId);
            await FailRunAsync(runId, "Análise cancelada antes de terminar.");
            throw;
        }
        catch (Exception ex)
        {
            // Falha do cálculo ou do banco: a análise fecha como Failed, sem recomendação (o que a IA não respondeu nunca chega aqui).
            logger.LogError(ex, "Previsão {RunId}: falha na análise.", runId);
            return Outcome(await FailRunAsync(runId, Truncate($"Falha na análise: {ex.Message}", MaxErrorLength)), []);
        }
    }

    /// <summary>
    /// Compensação: desfaz o que ficou no rastreador e grava o run como Failed. Usa <c>CancellationToken.None</c> de propósito: o
    /// fechamento precisa gravar mesmo com o token já cancelado (única exceção à cadeia de token da fatia).
    /// </summary>
    private async Task<ForecastRun> FailRunAsync(Guid runId, string message)
    {
        unitOfWork.Rollback();

        var failed = await runRepository.GetByIdForUpdate(runId, ct: CancellationToken.None)
            ?? throw new InvalidOperationException($"Análise {runId} não encontrada.");
        failed.Fail(message, clock.UtcNow);
        await unitOfWork.CommitAsync(CancellationToken.None);
        return failed;
    }

    private async Task<ForecastRunOutcomeDto> ExecuteAsync(ForecastRun run, CancellationToken ct)
    {
        var today = run.ReferenceDate;

        var source = await inputBuilder.LoadAsync(today, ct);

        // RN-DF17: horizonte automático = maior prazo de entrega + 3 meses, teto de 6.
        var horizon = ForecastHorizon.Automatic(source.Inputs.Count == 0 ? 0 : source.Inputs.Max(i => i.LeadTimeDays));
        var ctx = new ForecastCalculationContext(today, horizon);
        var results = source.Inputs.Select(input => ForecastCalculator.Calculate(input, ctx)).ToList();
        var events = EventsInHorizon(source.CalendarEvents, ctx);

        // RN-DF20: lotes sequenciais só com os produtos com venda; sem chave não há lote (RN-DF13).
        var ordered = ForecastAiPrompt.OrderForAi(results);
        var batches = aiOrchestrator.IsConfigured ? ForecastAiOrchestrator.Split(ordered, AiBatchSize) : [];

        run.BeginAnalysis((short)horizon, results.Count, batches.Sum(b => b.Count), batches.Count);
        var batchRows = new List<ForecastAiBatch>(batches.Count);
        for (var index = 0; index < batches.Count; index++)
        {
            var row = new ForecastAiBatch(new NewForecastAiBatchDto(run.Id, (short)(index + 1), [.. batches[index].Select(p => p.Input.ProductId)]));
            batchRows.Add(row);
        }
        if (batchRows.Count > 0)
            await batchRepository.AddRangeAsync(batchRows, ct);
        await unitOfWork.CommitAsync(ct);

        var ai = await aiOrchestrator.AnalyzeAsync(
            ctx,
            [.. batches.Select(b => (IReadOnlyList<ProductForecastResult>)b)],
            events,
            (report, token) => RecordBatchAsync(run, batchRows[report.Number - 1], report, token),
            ct);

        // Guardrails: o que a IA devolveu só vale dentro dos limites do cálculo (RN-DF10 a RN-DF12).
        var decisions = results
            .Select(result => (Result: result, Decision: ai.Items.TryGetValue(result.Input.ProductId, out var item)
                ? ForecastGuardrails.Apply(result, item)
                : ForecastGuardrails.FromCalculation(result)))
            .ToList();

        var summary = ai.UsedAi
            ? await aiOrchestrator.SummarizeAsync(
                ctx,
                [.. decisions.Select(d => (d.Result.Input.Name, d.Decision.Action, d.Decision.Priority, d.Decision.SuggestedQuantity, d.Decision.SuggestedDiscountPercent, d.Decision.Reason))],
                results.Count,
                events,
                ct)
            : null;
        summary ??= ForecastReasonTemplates.BuildSummary(ctx, results.Count, [.. decisions.Select(d => (d.Decision.Action, d.Decision.Priority))], events);

        var recommendations = decisions.Select(d => ToRecommendation(run.Id, d.Result, d.Decision, source)).ToList();
        var projections = results
            .Where(r => r.HasSales)
            .SelectMany(r => r.Projections.Select(p =>
                new ForecastProjection(new NewForecastProjectionDto(run.Id, r.Input.ProductId, (short)p.Month.Year, (short)p.Month.Month, p.Quantity))))
            .ToList();

        var status = ai.UsedAi ? ForecastRunStatusEnum.Completed : ForecastRunStatusEnum.CompletedWithoutAi;
        var utcNow = clock.UtcNow;

        // Commit final: tudo ou nada (RN-DF14: a nova análise expira as pendentes da anterior no mesmo commit).
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var previous = await recommendationRepository.GetAllForUpdate(
                r => r.Status == RecommendationStatusEnum.Pending && r.ForecastRunId != run.Id, token);
            foreach (var pending in previous)
                pending.Expire(utcNow);

            await recommendationRepository.AddRangeAsync(recommendations, token);
            await projectionRepository.AddRangeAsync(projections, token);
            await ApplyAbcClassesAsync(source, token);

            run.Complete(status, summary, ai.Error, ai.UsedAi ? aiOrchestrator.ProviderName : null, ai.UsedAi ? aiOrchestrator.Model : null, utcNow);
            await unitOfWork.CommitAsync(token);
        }, ct);

        logger.LogInformation(
            "Previsão {RunId}: {Status}, horizonte de {Horizon} meses, {Products} produtos, {Batches} lotes ({Failed} com falha).",
            run.Id, status, horizon, results.Count, batches.Count, run.BatchesFailed);

        return Outcome(run, decisions.Select(d => d.Decision.Action).ToList());
    }

    /// <summary>Grava o andamento do lote (enviado, concluído, com falha) e o contador da análise, num commit próprio.</summary>
    private async Task RecordBatchAsync(ForecastRun run, ForecastAiBatch row, AiBatchReport report, CancellationToken ct)
    {
        var utcNow = clock.UtcNow;
        switch (report.Status)
        {
            case AiBatchStatusEnum.Sent:
                row.MarkSent(utcNow);
                break;
            case AiBatchStatusEnum.Completed:
                row.MarkCompleted(report.DurationMs, utcNow);
                run.RecordBatch(completed: true);
                break;
            case AiBatchStatusEnum.Failed:
                row.MarkFailed(report.Error ?? "Falha sem detalhe.", report.DurationMs, utcNow);
                run.RecordBatch(completed: false);
                break;
        }

        await unitOfWork.CommitAsync(ct);
    }

    private static ForecastRecommendation ToRecommendation(Guid runId, ProductForecastResult result, ForecastDecision decision, ForecastSourceData source)
    {
        var input = result.Input;

        // RN-DF21 e L-DF5: a quantidade de compra é rateada nas variações pela proporção histórica de venda da grade.
        IReadOnlyList<NewForecastRecommendationItemDto> items = decision is { Action: RecommendationActionEnum.Buy, SuggestedQuantity: > 0 }
            ? [.. VariantAllocator
                .Split(decision.SuggestedQuantity.Value, source.VariantHistory.GetValueOrDefault(input.ProductId) ?? [], source.DecimalPlaces.GetValueOrDefault(input.ProductId))
                .Select(share => new NewForecastRecommendationItemDto(share.VariantId, share.Quantity, share.SharePercent))]
            : [];

        return new ForecastRecommendation(new NewForecastRecommendationDto(
            runId,
            input.ProductId,
            decision.Action,
            decision.Priority,
            result.Confidence,
            decision.SuggestedQuantity,
            result.Action == RecommendationActionEnum.Buy ? result.SuggestedQuantity : null,
            decision.SuggestedDiscountPercent,
            decision.SuggestedPromotionDays,
            decision.Deadline,
            decision.Reason,
            Truncate(result.Reason, ForecastGuardrails.MaxReasonLength),
            decision.AiAdjusted,
            input.CurrentStock,
            input.IncomingStock,
            result.ProjectedDemand,
            result.NextMonthDemand,
            result.CoverageDays,
            result.NextMonthSeasonalIndex,
            result.SafetyStock,
            input.LeadTimeDays,
            input.LeadTimeSource,
            (short)result.MonthsOfHistory,
            result.UsedSamePeriodLastYear,
            items));
    }

    /// <summary>Eventos do calendário de hoje até o fim do horizonte; só os ativos, com a data do ano em que caem (RN-DF17).</summary>
    private static List<ForecastEvent> EventsInHorizon(IReadOnlyList<CalendarEvent> calendarEvents, ForecastCalculationContext ctx)
    {
        var start = ctx.Today;
        var end = ctx.Horizon[^1].LastDay;
        var events = new List<ForecastEvent>();
        foreach (var calendarEvent in calendarEvents)
        {
            for (var year = start.Year; year <= end.Year; year++)
            {
                var date = calendarEvent.DateIn(year);
                if (date >= start && date <= end)
                    events.Add(new ForecastEvent(calendarEvent.Name, date, calendarEvent.LeadDays));
            }
        }

        return [.. events.OrderBy(e => e.Date).ThenBy(e => e.Name, StringComparer.Ordinal)];
    }

    /// <summary>Classe ABC recalculada (RN-CE29): só grava o produto cuja classe mudou.</summary>
    private async Task ApplyAbcClassesAsync(ForecastSourceData source, CancellationToken ct)
    {
        var ids = source.AbcClasses.Keys.ToList();
        if (ids.Count == 0)
            return;

        var products = await productRepository.GetAllForUpdate(p => ids.Contains(p.Id), ct);
        foreach (var product in products.Where(p => p.AbcClass != source.AbcClasses[p.Id]))
            product.ApplyAbcClass(source.AbcClasses[product.Id]);
    }

    private static ForecastRunOutcomeDto Outcome(ForecastRun run, IReadOnlyCollection<RecommendationActionEnum> actions)
        => new(
            run.Id,
            run.Status,
            run.HorizonMonths,
            run.ProductsAnalyzed,
            run.ProductsSentToAi,
            actions.Count(a => a == RecommendationActionEnum.Buy),
            actions.Count(a => a == RecommendationActionEnum.Promote),
            actions.Count(a => a == RecommendationActionEnum.Hold),
            run.BatchesTotal,
            run.BatchesFailed);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";
}

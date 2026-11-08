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
    IForecastSettingsRepository settingsRepository,
    IUnitOfWork unitOfWork,
    IBusinessClock clock,
    IConfiguration configuration,
    ILogger<ForecastEngine> logger) : IForecastEngine
{
    /// <summary>Chave opcional do tamanho do lote da IA, usada enquanto ninguém salvou o agendamento (T-DF11); o padrão é 40 (RN-DF20).</summary>
    public const string AiBatchSizeKey = "Forecast:AiBatchSize";

    private const int MaxErrorLength = 1000;

    /// <summary>Tamanho do lote: o salvo em <c>/settings/forecast</c> (T-DF11), senão a configuração, senão 40.</summary>
    private async Task<int> AiBatchSizeAsync(CancellationToken ct)
    {
        var settings = await settingsRepository.Get(_ => true, ct: ct);
        if (settings != null)
            return settings.AiBatchSize;
        return int.TryParse(configuration[AiBatchSizeKey], out var size) && size > 0 ? size : ForecastAiOrchestrator.DefaultBatchSize;
    }

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
        var batches = aiOrchestrator.IsConfigured ? ForecastAiOrchestrator.Split(ordered, await AiBatchSizeAsync(ct)) : [];

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

        // "Transferir" (RN-DF16, RN-DF29): só onde o cálculo e a IA decidiram "Manter" (a empresa não precisa comprar nem promover)
        // e um local está abaixo do mínimo com excesso em outro. Sai do cálculo, a IA não cria nem muda.
        var final = decisions.Select(d => WithTransfer(d.Result, d.Decision, source))
            .Select(d => WithStoreSplit(d.Result, d.Decision, d.Plan, source))
            .ToList();
        var recommendations = final.Select(d => ToRecommendation(run.Id, d.Result, d.Decision, source, d.Plan, d.Stores, d.StoreText)).ToList();
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

        return Outcome(run, final.Select(d => d.Decision.Action).ToList());
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

    /// <summary>Cobertura alvo de quem tem excesso quando o produto não tem política (RN-DF16).</summary>
    public const int DefaultTransferCoverageDays = 30;

    /// <summary>
    /// "Manter" que vira "Transferir" (RN-DF16): demanda diária de cada local = a do produto (próximo mês ÷ 30) × participação da loja
    /// nas vendas ÷ locais vendáveis da loja × participação da variação na grade; local não vendável (depósito) tem demanda zero.
    /// </summary>
    private static (ProductForecastResult Result, ForecastDecision Decision, TransferPlan? Plan) WithTransfer(
        ProductForecastResult result, ForecastDecision decision, ForecastSourceData source)
    {
        var productId = result.Input.ProductId;
        if (decision.Action != RecommendationActionEnum.Hold || !source.LocationPositions.TryGetValue(productId, out var rows))
            return (result, decision, null);

        var daily = result.NextMonthDemand / 30m;
        var history = source.VariantHistory.GetValueOrDefault(productId) ?? [];
        var historyTotal = history.Sum(h => h.HistoricalQuantity);
        var variantCount = Math.Max(1, rows.Select(r => r.VariantId).Distinct().Count());
        decimal VariantShare(Guid variantId) => historyTotal > 0
            ? history.Where(h => h.VariantId == variantId).Sum(h => h.HistoricalQuantity) / historyTotal
            : 1m / variantCount;
        var sellableByStore = rows.Where(r => r.IsSellable).GroupBy(r => r.StoreId)
            .ToDictionary(g => g.Key, g => Math.Max(1, g.Select(r => r.LocationId).Distinct().Count()));

        var positions = rows.Select(r => new LocationPosition(r.VariantId, r.LocationId, r.Available, r.Minimum,
            r.IsSellable
                ? daily * source.StoreShares.GetValueOrDefault((productId, r.StoreId)) / sellableByStore[r.StoreId] * VariantShare(r.VariantId)
                : 0m)).ToList();
        var plan = TransferTrigger.Evaluate(positions, result.Input.TargetCoverageDays ?? DefaultTransferCoverageDays,
            source.DecimalPlaces.GetValueOrDefault(productId));
        if (plan is null)
            return (result, decision, null);

        string Name(Guid locationId) => rows.Where(r => r.LocationId == locationId).Select(r => $"{r.StoreName} › {r.LocationName}").First();
        var targetEmpty = plan.Items.Any(i => rows.Any(r => r.VariantId == i.VariantId && r.LocationId == plan.ToLocationId && r.Available <= 0));
        var reason = $"Transferir {plan.Total:0.###} de {Name(plan.FromLocationId)} para {Name(plan.ToLocationId)}: o destino está abaixo do "
            + "mínimo e a origem tem mais do que precisa para a cobertura alvo. A empresa não precisa comprar.";
        return (result, new ForecastDecision
        {
            Action = RecommendationActionEnum.Transfer,
            Priority = targetEmpty ? RecommendationPriorityEnum.High : RecommendationPriorityEnum.Medium,
            SuggestedQuantity = plan.Total,
            Reason = Truncate(reason, ForecastGuardrails.MaxReasonLength),
        }, plan);
    }

    /// <summary>
    /// "Comprar" dividido entre as lojas pela própria análise (RN-DF27): por loja, participação nas vendas de 90 dias, saldo (todos os
    /// locais da loja), a caminho e a falta no horizonte; a quantidade sugerida é rateada pela falta, no múltiplo do fornecedor. O
    /// motivo ganha a divisão no fim. Com uma loja só, não divide.
    /// </summary>
    private static (ProductForecastResult Result, ForecastDecision Decision, TransferPlan? Plan,
        IReadOnlyList<(Guid StoreId, decimal SharePercent, decimal Shortfall, decimal Quantity)> Stores, string StoreText) WithStoreSplit(
        ProductForecastResult result, ForecastDecision decision, TransferPlan? plan, ForecastSourceData source)
    {
        var productId = result.Input.ProductId;
        if (decision is not { Action: RecommendationActionEnum.Buy, SuggestedQuantity: > 0 } || !source.LocationPositions.TryGetValue(productId, out var rows))
            return (result, decision, plan, [], string.Empty);

        var stores = rows.GroupBy(r => (r.StoreId, r.StoreName)).OrderBy(g => g.Key.StoreName, StringComparer.Ordinal).ToList();
        if (stores.Count < 2)
            return (result, decision, plan, [], string.Empty);

        var inputs = stores.Select(store =>
        {
            var share = Math.Round(source.StoreShares.GetValueOrDefault((productId, store.Key.StoreId)) * 100m, 2, MidpointRounding.AwayFromZero);
            var stock = store.Sum(r => r.Available);
            var incoming = source.IncomingByStore.GetValueOrDefault((productId, store.Key.StoreId));
            return (store.Key.StoreId, store.Key.StoreName, Share: share,
                Shortfall: StoreSplitCalculator.Shortfall(result.ProjectedDemand, share, stock, incoming));
        }).ToList();
        var parts = StoreSplitCalculator.Split(decision.SuggestedQuantity.Value,
            [.. inputs.Select(i => new StoreSplitInput(i.StoreId, i.Share, i.Shortfall))], result.Input.OrderMultiple);

        var split = inputs.Select((i, index) => (i.StoreId, i.Share, i.Shortfall, parts[index].Quantity)).ToList();
        var text = " Por loja: " + string.Join(" · ", inputs.Select((i, index) => $"{i.StoreName} {parts[index].Quantity:0.###}")) + ".";
        var withSplit = new ForecastDecision
        {
            Action = decision.Action,
            Priority = decision.Priority,
            SuggestedQuantity = decision.SuggestedQuantity,
            SuggestedDiscountPercent = decision.SuggestedDiscountPercent,
            SuggestedPromotionDays = decision.SuggestedPromotionDays,
            Deadline = decision.Deadline,
            Reason = Truncate(decision.Reason + text, ForecastGuardrails.MaxReasonLength),
            FromAi = decision.FromAi,
            AiAdjusted = decision.AiAdjusted,
        };
        return (result, withSplit, plan, split, text);
    }

    private static ForecastRecommendation ToRecommendation(Guid runId, ProductForecastResult result, ForecastDecision decision, ForecastSourceData source,
        TransferPlan? plan = null, IReadOnlyList<(Guid StoreId, decimal SharePercent, decimal Shortfall, decimal Quantity)>? stores = null,
        string storeText = "")
    {
        var input = result.Input;

        // RN-DF21 e L-DF5: a quantidade de compra é rateada nas variações pela proporção histórica de venda da grade.
        // "Transferir": as variações e quantidades do gatilho (RN-DF16).
        IReadOnlyList<NewForecastRecommendationItemDto> items = plan is not null
            ? [.. plan.Items.Select(i => new NewForecastRecommendationItemDto(i.VariantId, i.Quantity,
                plan.Total > 0 ? Math.Round(i.Quantity / plan.Total * 100m, 2, MidpointRounding.AwayFromZero) : 0m))]
            : decision is { Action: RecommendationActionEnum.Buy, SuggestedQuantity: > 0 }
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
            // "Transferir" sai do cálculo (RN-DF29): o motivo calculado é o da transferência, não o do "Manter" que ela substituiu.
            plan is not null ? decision.Reason : Truncate(Truncate(result.Reason, ForecastGuardrails.MaxReasonLength) + storeText, ForecastGuardrails.MaxReasonLength),
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
            items)
        {
            TransferFromLocationId = plan?.FromLocationId,
            TransferToLocationId = plan?.ToLocationId,
            Stores = stores ?? [],
        });
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

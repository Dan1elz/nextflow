using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nextflow.Application.Forecast;
using Nextflow.Application.Utils;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>
/// Gera uma análise de previsão de demanda (spec seção 2):
/// rebuild do agregado mensal → cálculo determinístico → IA (com fallback) → guardrails →
/// persistência (run + recomendações + projeções + sazonalidade) → expira pendências de análises anteriores.
/// </summary>
public class GenerateForecastUseCase(
    IRebuildProductSalesMonthlyUseCase rebuildUseCase,
    IProductSalesMonthlyRepository salesMonthlyRepository,
    IProductRepository productRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IProductSeasonalityRepository seasonalityRepository,
    IForecastRunRepository runRepository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastProjectionRepository projectionRepository,
    IAiClient aiClient,
    IUnitOfWork unitOfWork,
    ILogger<GenerateForecastUseCase> logger
) : IGenerateForecastUseCase
{
    /// <summary>Pedidos recebidos usados para medir o lead time real de cada fornecedor.</summary>
    public const int LeadTimeSamplesPerSupplier = 5;

    /// <summary>Categoria "guarda-chuva": contém ≥ 80% dos produtos com vendas (e há pelo menos 5 deles).</summary>
    public const decimal UmbrellaCategoryShare = 0.8m;
    public const int UmbrellaCategoryMinProducts = 5;

    private const int MaxSummaryLength = 1000;

    public async Task<ForecastRunDto> Execute(Guid userId, GenerateForecastRequestDto dto, CancellationToken ct)
    {
        if (dto is null)
            throw new BadRequestException("O corpo da requisição é obrigatório.");

        dto.Validate();

        var stopwatch = Stopwatch.StartNew();
        var today = BusinessClock.Today;
        var ctx = new ForecastCalculationContext(today, dto.HorizonMonths);
        var run = new ForecastRun(userId, dto.HorizonMonths);

        // 1–3. Rebuild + cálculo determinístico (falha aqui = análise Failed)
        List<ProductForecastResult> results;
        try
        {
            var rows = await rebuildUseCase.Execute(ct);
            logger.LogInformation("Previsão: agregado mensal reconstruído ({Rows} linhas) em {Elapsed} ms.", rows, stopwatch.ElapsedMilliseconds);

            results = await CalculateAsync(ctx, ct);
            logger.LogInformation("Previsão: cálculo de {Count} produtos concluído em {Elapsed} ms.", results.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Previsão: falha no cálculo.");
            run.Fail($"Falha no cálculo da previsão: {ex.Message}", (int)stopwatch.ElapsedMilliseconds);
            await runRepository.AddAsync(run, ct);
            return await LoadRunAsync(run.Id, today, ct);
        }

        // 4. Payload + IA
        var events = CommercialCalendar.GetEventsBetween(ctx.Horizon[0].FirstDay, ctx.Horizon[^1].FirstDay.AddMonths(1).AddDays(-1));
        var aiProducts = ForecastAiPrompt.SelectProducts(results);
        var payload = ForecastAiPrompt.BuildPayload(ctx, aiProducts, events);
        run.SetInputSnapshot(payload);

        var (aiResponse, aiError) = await AskAiAsync(payload, aiProducts.Count, ct);

        // 5. Guardrails (IA × cálculo)
        var sentIds = aiProducts.Select(p => p.Input.ProductId).ToHashSet();
        var aiItems = new Dictionary<Guid, AiRecommendationItem>();
        foreach (var item in aiResponse?.Recomendacoes ?? [])
        {
            if (Guid.TryParse(item.ProdutoId, out var productId) && sentIds.Contains(productId))
                aiItems.TryAdd(productId, item);
        }

        var decisions = results
            .Select(r => (Result: r, Decision: aiResponse != null && aiItems.TryGetValue(r.Input.ProductId, out var item)
                ? ForecastGuardrails.Apply(r, item)
                : ForecastGuardrails.FromCalculation(r)))
            .ToList();

        var status = aiResponse != null ? ForecastRunStatus.Completed : ForecastRunStatus.CompletedWithoutAi;
        if (status == ForecastRunStatus.Completed)
            run.SetAiInfo(aiClient.ProviderName, aiClient.Model);

        var summary = !string.IsNullOrWhiteSpace(aiResponse?.ResumoGeral)
            ? Truncate(aiResponse.ResumoGeral.Trim(), MaxSummaryLength)
            : ForecastReasonTemplates.BuildSummary(ctx, results.Count, [.. decisions.Select(d => (d.Decision.Action, d.Decision.Priority))], events);

        // 6–7. Persistência atômica
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var previousPending = await recommendationRepository.GetAllAsync(
                r => r.Status == RecommendationStatus.Pending, 0, int.MaxValue, token);
            foreach (var pending in previousPending)
                pending.Expire();

            await UpsertSeasonalityAsync(results, token);

            run.Complete(status, summary, aiError, (int)stopwatch.ElapsedMilliseconds, results.Count);
            await runRepository.AddAsync(run, token);

            var recommendations = decisions.Select(d => new ForecastRecommendation(new CreateForecastRecommendationDto
            {
                ForecastRunId = run.Id,
                ProductId = d.Result.Input.ProductId,
                Action = d.Decision.Action,
                Priority = d.Decision.Priority,
                Confidence = d.Result.Confidence,
                SuggestedQuantity = d.Decision.SuggestedQuantity,
                SuggestedDiscountPercent = d.Decision.SuggestedDiscountPercent,
                SuggestedPromotionDays = d.Decision.SuggestedPromotionDays,
                Deadline = d.Decision.Deadline,
                Reason = d.Decision.Reason,
                CurrentStock = d.Result.Input.CurrentStock,
                IncomingStock = d.Result.Input.IncomingStock,
                ProjectedDemand = d.Result.ProjectedDemand,
                NextMonthDemand = d.Result.NextMonthDemand,
                CoverageDays = d.Result.CoverageDays,
                NextMonthSeasonalIndex = d.Result.NextMonthSeasonalIndex
            })).ToList();
            if (recommendations.Count > 0)
                await recommendationRepository.AddRangeAsync(recommendations, token);

            var projections = results
                .Where(r => r.HasSales)
                .SelectMany(r => r.Projections.Select(p =>
                    new ForecastProjection(run.Id, r.Input.ProductId, p.Month.Year, p.Month.Month, p.Quantity)))
                .ToList();
            if (projections.Count > 0)
                await projectionRepository.AddRangeAsync(projections, token);

            return true;
        }, ct);

        logger.LogInformation("Previsão {RunId}: {Status} em {Elapsed} ms ({Count} produtos, {AiCount} enviados à IA).",
            run.Id, status, run.DurationMs, results.Count, aiProducts.Count);

        return await LoadRunAsync(run.Id, today, ct);
    }

    // ------------------------------------------------------------------ IA

    private async Task<(AiForecastResponse? Response, string? Error)> AskAiAsync(string payload, int productsCount, CancellationToken ct)
    {
        const string fallback = "Recomendações geradas apenas pelo cálculo estatístico.";

        if (!aiClient.IsConfigured)
            return (null, $"IA não configurada (AI_API_KEY vazio). {fallback}");

        if (productsCount == 0)
            return (null, $"Nenhum produto com vendas para enviar à IA. {fallback}");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var raw = await aiClient.GenerateJsonAsync(
                ForecastAiPrompt.SystemPrompt,
                ForecastAiPrompt.BuildUserPrompt(payload),
                ForecastAiPrompt.ResponseSchema,
                ct);

            var response = ForecastAiPrompt.ParseResponse(raw);
            if (response.Recomendacoes == null || response.Recomendacoes.Count == 0)
                throw new FormatException("A IA não retornou recomendações.");

            logger.LogInformation("Previsão: IA respondeu {Count} recomendações em {Elapsed} ms (payload {Size} caracteres).",
                response.Recomendacoes.Count, stopwatch.ElapsedMilliseconds, payload.Length);
            return (response, null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Previsão: IA indisponível após {Elapsed} ms ({Type}: {Message}). Usando apenas o cálculo.",
                stopwatch.ElapsedMilliseconds, ex.GetType().Name, ex.Message);
            return (null, Truncate($"Falha ao consultar a IA ({aiClient.ProviderName} {aiClient.Model}): {ex.Message} {fallback}", 1000));
        }
    }

    // ------------------------------------------------------------------ cálculo

    private async Task<List<ProductForecastResult>> CalculateAsync(ForecastCalculationContext ctx, CancellationToken ct)
    {
        var products = (await productRepository.GetAllAsync(p => p.IsActive, 0, int.MaxValue, ct, query => query
                .Include(p => p.Supplier)
                .Include(p => p.CategoryProducts.Where(cp => cp.IsActive))
                    .ThenInclude(cp => cp.Category)
                .IncludeCurrentPromotions(ctx.Today)))
            .OrderBy(p => p.Name)
            .ToList();

        // Série mensal (24 meses completos) por produto, a partir do agregado recém-reconstruído
        var monthly = await salesMonthlyRepository.GetAllAsync(_ => true, 0, int.MaxValue, ct);
        var window = ForecastCalculationContext.HistoryWindowMonths;
        var firstWindowMonth = ctx.MonthAt(0);
        var series = new Dictionary<Guid, decimal[]>();
        var firstSale = new Dictionary<Guid, YearMonth>();
        var revenue12 = new Dictionary<Guid, decimal>();

        foreach (var row in monthly)
        {
            var month = new YearMonth(row.Year, row.Month);
            if (row.QuantitySold > 0 && month <= ctx.LastCompleteMonth &&
                (!firstSale.TryGetValue(row.ProductId, out var current) || month < current))
                firstSale[row.ProductId] = month;

            var index = YearMonth.MonthsBetween(firstWindowMonth, month);
            if (index < 0 || index >= window)
                continue; // fora da janela (inclui o mês corrente)

            if (!series.TryGetValue(row.ProductId, out var values))
                series[row.ProductId] = values = new decimal[window];
            values[index] += row.QuantitySold;

            if (index >= window - 12)
                revenue12[row.ProductId] = revenue12.GetValueOrDefault(row.ProductId) + row.Revenue;
        }

        int MonthsOfHistory(YearMonth? first) => first.HasValue
            ? Math.Clamp(YearMonth.MonthsBetween(first.Value, ctx.LastCompleteMonth) + 1, 0, window)
            : 0;

        // Categoria de referência (primeira categoria ativa em ordem alfabética, ignorando a "guarda-chuva")
        var productCategories = products.ToDictionary(
            p => p.Id,
            p => p.CategoryProducts
                .Where(cp => cp.IsActive && cp.Category is { IsActive: true })
                .Select(cp => cp.Category!)
                .DistinctBy(c => c.Id)
                .OrderBy(c => c.Description, StringComparer.InvariantCultureIgnoreCase)
                .ToList());

        var productsWithSales = products.Where(p => series.ContainsKey(p.Id) && series[p.Id].Any(q => q > 0)).Select(p => p.Id).ToHashSet();
        var umbrellaCategories = productCategories
            .Where(kv => productsWithSales.Contains(kv.Key))
            .SelectMany(kv => kv.Value.Select(c => c.Id))
            .GroupBy(id => id)
            .Where(g => productsWithSales.Count >= UmbrellaCategoryMinProducts && g.Count() >= UmbrellaCategoryShare * productsWithSales.Count)
            .Select(g => g.Key)
            .ToHashSet();

        var referenceCategory = productCategories.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.FirstOrDefault(c => !umbrellaCategories.Contains(c.Id)) ?? kv.Value.FirstOrDefault());

        var categorySeries = new Dictionary<Guid, (decimal[] Series, YearMonth? FirstSale)>();
        foreach (var category in referenceCategory.Values.Where(c => c != null).DistinctBy(c => c!.Id))
        {
            var members = productCategories.Where(kv => kv.Value.Any(c => c.Id == category!.Id)).Select(kv => kv.Key).ToList();
            var sum = new decimal[window];
            YearMonth? first = null;
            foreach (var member in members)
            {
                if (series.TryGetValue(member, out var values))
                    for (var i = 0; i < window; i++)
                        sum[i] += values[i];
                if (firstSale.TryGetValue(member, out var memberFirst) && (first == null || memberFirst < first.Value))
                    first = memberFirst;
            }
            categorySeries[category!.Id] = (sum, first);
        }

        // Lead time real por fornecedor (média dos últimos pedidos recebidos)
        var samples = await purchaseOrderRepository.GetReceivedLeadTimeSamplesAsync(DateTime.UtcNow.AddYears(-2), ct);
        var realLeadTimes = samples
            .GroupBy(s => s.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => (int)Math.Clamp(Math.Round(g
                    .OrderByDescending(s => s.ReceivedAt)
                    .Take(LeadTimeSamplesPerSupplier)
                    .Average(s => (s.ReceivedAt - s.CreateAt).TotalDays), MidpointRounding.AwayFromZero), 1, 180));

        var incoming = await purchaseOrderRepository.GetIncomingStockByProductAsync(ct);

        var results = new List<ProductForecastResult>(products.Count);
        foreach (var product in products)
        {
            var category = referenceCategory[product.Id];
            var categoryData = category != null && categorySeries.TryGetValue(category.Id, out var data) ? data : default;
            var promotion = product.GetActivePromotion(ctx.Today);
            var hasRealLeadTime = realLeadTimes.TryGetValue(product.SupplierId, out var realLeadTime);

            var input = new ForecastProductInput
            {
                ProductId = product.Id,
                Name = product.Name,
                ProductCode = product.ProductCode,
                Brand = product.Brand,
                UnitType = product.UnitType,
                ReferenceCategory = category?.Description,
                CurrentStock = product.Quantity,
                IncomingStock = incoming.GetValueOrDefault(product.Id),
                MinStock = product.MinStock,
                LeadTimeDays = hasRealLeadTime ? realLeadTime : Math.Max(1, product.Supplier?.LeadTimeDays ?? 7),
                LeadTimeFromHistory = hasRealLeadTime,
                Price = product.Price,
                CostPrice = product.CostPrice,
                Validity = product.Validity,
                ActivePromotionDiscountPercent = promotion?.DiscountPercent,
                ActivePromotionEndDate = promotion?.EndDate,
                MonthlySales = series.TryGetValue(product.Id, out var productSeries) ? productSeries : new decimal[window],
                MonthsOfHistory = MonthsOfHistory(firstSale.TryGetValue(product.Id, out var productFirst) ? productFirst : null),
                CategoryMonthlySales = categoryData.Series,
                CategoryMonthsOfHistory = MonthsOfHistory(categoryData.FirstSale)
            };

            var result = ForecastCalculator.Calculate(input, ctx);
            result.RevenueLast12Months = revenue12.GetValueOrDefault(product.Id);
            results.Add(result);
        }

        return results;
    }

    private async Task UpsertSeasonalityAsync(List<ProductForecastResult> results, CancellationToken ct)
    {
        var withSales = results.Where(r => r.HasSales).ToList();
        if (withSales.Count == 0)
            return;

        var ids = withSales.Select(r => r.Input.ProductId).ToList();
        var existing = (await seasonalityRepository.GetAllAsync(s => ids.Contains(s.ProductId), 0, int.MaxValue, ct))
            .ToDictionary(s => (s.ProductId, s.Month));

        var toAdd = new List<ProductSeasonality>();
        foreach (var result in withSales)
        {
            for (var month = 1; month <= 12; month++)
            {
                var index = result.SeasonalIndex[month - 1];
                if (existing.TryGetValue((result.Input.ProductId, month), out var row))
                    row.Update(index, result.MonthsOfHistory);
                else
                    toAdd.Add(new ProductSeasonality(result.Input.ProductId, month, index, result.MonthsOfHistory));
            }
        }

        if (toAdd.Count > 0)
            await seasonalityRepository.AddRangeAsync(toAdd, ct); // também grava as atualizações rastreadas
        else
            await seasonalityRepository.SaveAsync(ct);
    }

    private async Task<ForecastRunDto> LoadRunAsync(Guid runId, DateOnly today, CancellationToken ct)
    {
        var run = await runRepository.GetByIdAsync(runId, ct, query => ForecastMappings.IncludeDetails(query, today))
            ?? throw new NotFoundException("Análise de previsão não encontrada.");
        return new ForecastRunDto(run);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

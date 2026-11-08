using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Services;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Orquestra a IA da análise (RN-DF20): os produtos com venda vão em <b>lotes sequenciais</b> de até
/// <see cref="DefaultBatchSize"/>, o próximo só depois da resposta do anterior; um lote com falha deixa seus produtos no cálculo
/// sem derrubar a análise; o resumo geral sai numa chamada final com os resultados já fechados. A IA só prioriza e explica
/// (ADR 0005): o que ela devolve passa pelos guardrails do <see cref="ForecastGuardrails"/> antes de valer. Sem chave, nenhuma
/// chamada é feita (RN-DF13). Sem EF e sem relógio de negócio: só o cliente da IA.
/// </summary>
public class ForecastAiOrchestrator(IAiClient aiClient, ILogger<ForecastAiOrchestrator> logger)
{
    /// <summary>Tamanho inicial do lote (RN-DF20).</summary>
    public const int DefaultBatchSize = 40;

    private const int MaxSummaryLength = 1000;

    public bool IsConfigured => aiClient.IsConfigured;
    public string ProviderName => aiClient.ProviderName;
    public string Model => aiClient.Model;

    /// <summary>Divide a lista, já ordenada por <see cref="ForecastAiPrompt.OrderForAi"/>, em lotes de até <paramref name="batchSize"/>.</summary>
    public static List<List<ProductForecastResult>> Split(IReadOnlyList<ProductForecastResult> ordered, int batchSize = DefaultBatchSize)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "O lote precisa ter ao menos um produto.");

        return ordered.Chunk(batchSize).Select(chunk => chunk.ToList()).ToList();
    }

    /// <summary>
    /// Envia os lotes em sequência. <paramref name="onBatch"/> é chamado quando o lote é enviado e quando termina (concluído ou com
    /// falha), para o serviço gravar o progresso. Só cancelamento interrompe: qualquer outra falha vira falha do lote.
    /// </summary>
    public async Task<AiAnalysisResult> AnalyzeAsync(
        ForecastCalculationContext ctx,
        IReadOnlyList<IReadOnlyList<ProductForecastResult>> batches,
        IReadOnlyList<ForecastEvent> events,
        Func<AiBatchReport, CancellationToken, Task>? onBatch,
        CancellationToken ct)
    {
        const string fallback = "Recomendações geradas apenas pelo cálculo estatístico.";

        if (!aiClient.IsConfigured)
            return new AiAnalysisResult { Error = $"IA não configurada (Ai:ApiKey vazia). {fallback}" };

        if (batches.Count == 0)
            return new AiAnalysisResult { Error = $"Nenhum produto com vendas para enviar à IA. {fallback}" };

        var items = new Dictionary<Guid, AiRecommendationItem>();
        var reports = new List<AiBatchReport>(batches.Count);
        var errors = new List<string>();

        for (var index = 0; index < batches.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            var number = index + 1;
            var batch = batches[index];
            var ids = batch.Select(p => p.Input.ProductId).ToList();
            if (onBatch != null)
                await onBatch(new AiBatchReport(number, ids, AiBatchStatusEnum.Sent, null, 0), ct);

            var stopwatch = Stopwatch.StartNew();
            AiBatchReport report;
            try
            {
                var payload = ForecastAiPrompt.BuildPayload(ctx, [.. batch], events);
                var raw = await aiClient.GenerateJsonAsync(
                    ForecastAiPrompt.SystemPrompt,
                    ForecastAiPrompt.BuildUserPrompt(payload),
                    ForecastAiPrompt.ResponseSchema,
                    ct);

                var response = ForecastAiPrompt.ParseResponse(raw);
                if (response.Recomendacoes == null || response.Recomendacoes.Count == 0)
                    throw new FormatException("A IA não retornou recomendações.");

                // produtoId desconhecido (ou de outro lote) é ignorado; produto ausente fica com o cálculo (RN-DF10).
                var sent = ids.ToHashSet();
                foreach (var item in response.Recomendacoes)
                {
                    if (Guid.TryParse(item.ProdutoId, out var productId) && sent.Contains(productId))
                        items.TryAdd(productId, item);
                }

                report = new AiBatchReport(number, ids, AiBatchStatusEnum.Completed, null, (int)stopwatch.ElapsedMilliseconds);
                logger.LogInformation("Previsão: lote {Number} de {Total} respondeu {Count} recomendações em {Elapsed} ms.",
                    number, batches.Count, response.Recomendacoes.Count, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var message = $"{ex.GetType().Name}: {ex.Message}";
                report = new AiBatchReport(number, ids, AiBatchStatusEnum.Failed, message, (int)stopwatch.ElapsedMilliseconds);
                errors.Add($"Lote {number} de {batches.Count} ficou só com o cálculo ({message}).");
                logger.LogWarning("Previsão: lote {Number} de {Total} falhou após {Elapsed} ms ({Type}). Seus produtos seguem só com o cálculo.",
                    number, batches.Count, stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            }

            reports.Add(report);
            if (onBatch != null)
                await onBatch(report, ct);
        }

        return new AiAnalysisResult
        {
            Items = items,
            Batches = reports,
            Error = errors.Count == 0 ? null : Truncate(string.Join(" ", errors) + " " + fallback, 2000)
        };
    }

    /// <summary>
    /// Chamada final (RN-DF20): resumo geral a partir das decisões já fechadas. Devolve nulo em qualquer falha, e o serviço usa o
    /// resumo de template (RN-DF13).
    /// </summary>
    public async Task<string?> SummarizeAsync(
        ForecastCalculationContext ctx,
        IReadOnlyList<(string Name, RecommendationActionEnum Action, RecommendationPriorityEnum Priority, decimal? Quantity, decimal? Discount, string Reason)> decisions,
        int productsAnalyzed,
        IReadOnlyList<ForecastEvent> events,
        CancellationToken ct)
    {
        if (!aiClient.IsConfigured)
            return null;

        try
        {
            var payload = ForecastAiPrompt.BuildSummaryPayload(ctx, decisions, productsAnalyzed, events);
            var raw = await aiClient.GenerateJsonAsync(
                ForecastAiPrompt.SummarySystemPrompt,
                ForecastAiPrompt.BuildSummaryUserPrompt(payload),
                ForecastAiPrompt.SummaryResponseSchema,
                ct);

            var summary = ForecastAiPrompt.ParseResponse(raw).ResumoGeral;
            return string.IsNullOrWhiteSpace(summary) ? null : Truncate(summary.Trim(), MaxSummaryLength);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Previsão: o resumo da IA falhou ({Type}). Usando o resumo calculado.", ex.GetType().Name);
            return null;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

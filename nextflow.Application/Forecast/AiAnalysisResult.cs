namespace Nextflow.Application.Forecast;

/// <summary>
/// O que a IA devolveu para a análise: os itens por produto dos lotes que responderam, o andamento de cada lote e o motivo de a IA
/// não ter participado (sem chave, sem produto com venda ou lotes com falha). Lote com falha só deixa seus produtos no cálculo.
/// </summary>
public sealed class AiAnalysisResult
{
    public IReadOnlyDictionary<Guid, AiRecommendationItem> Items { get; init; } = new Dictionary<Guid, AiRecommendationItem>();
    public IReadOnlyList<AiBatchReport> Batches { get; init; } = [];

    /// <summary>Texto para <c>ForecastRun.ErrorMessage</c>: vazio quando todos os lotes responderam.</summary>
    public string? Error { get; init; }

    public int CompletedBatches => Batches.Count(b => b.Status == Nextflow.Domain.Enums.AiBatchStatusEnum.Completed);

    /// <summary>A IA participou de ao menos um lote (RN-DF13: senão a análise termina <c>CompletedWithoutAi</c>).</summary>
    public bool UsedAi => CompletedBatches > 0;
}

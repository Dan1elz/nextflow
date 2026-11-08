using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Lote da IA de uma análise (T-DF9, <c>GET /api/forecasts/{runId}/batches</c>).</summary>
public class ForecastAiBatchDto
{
    public int BatchNumber { get; set; }
    public int ProductsCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }

    public ForecastAiBatchDto() { }

    public ForecastAiBatchDto(ForecastAiBatch entity)
    {
        BatchNumber = entity.BatchNumber;
        ProductsCount = entity.ProductsCount;
        Status = entity.Status.GetApiName();
        ErrorMessage = entity.ErrorMessage;
        DurationMs = entity.DurationMs;
    }
}

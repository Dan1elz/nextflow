using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Andamento para o polling de 5 s da T-DF2 (L-DF1). <c>Percent</c>: lotes respondidos (com ou sem falha) sobre o total; sem lote
/// (sem IA, ou antes de o motor montar os lotes) é 0 em andamento e 100 ao terminar.
/// </summary>
public class ForecastRunProgressDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public ForecastBatchProgressDto Batches { get; set; } = new();
    public int Percent { get; set; }

    public ForecastRunProgressDto() { }

    public ForecastRunProgressDto(ForecastRun entity)
    {
        Id = entity.Id;
        Status = entity.Status.GetApiName();
        Batches = new ForecastBatchProgressDto(entity.BatchesTotal, entity.BatchesCompleted, entity.BatchesFailed);
        Percent = !entity.IsRunning
            ? 100
            : entity.BatchesTotal == 0
                ? 0
                : (int)Math.Floor(100.0 * (entity.BatchesCompleted + entity.BatchesFailed) / entity.BatchesTotal);
    }
}

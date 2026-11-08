using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>Lote de produtos enviado à IA (RN-DF20): sequencial, e um lote com falha não derruba a análise.</summary>
public class ForecastAiBatch : BaseModel
{
    public Guid ForecastRunId { get; private set; }
    public short BatchNumber { get; private set; }
    public int ProductsCount { get; private set; }
    /// <summary>Produtos do lote, para auditar o que a IA recebeu.</summary>
    public Guid[] ProductIds { get; private set; } = [];
    public AiBatchStatusEnum Status { get; private set; } = AiBatchStatusEnum.Pending;
    public string? ErrorMessage { get; private set; }
    public int? DurationMs { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    public virtual ForecastRun ForecastRun { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "lote da IA";
    public override string Plural => "lotes da IA";

    private ForecastAiBatch() { }

    public ForecastAiBatch(NewForecastAiBatchDto dto)
    {
        ForecastRunId = dto.ForecastRunId;
        BatchNumber = dto.BatchNumber;
        ProductIds = [.. dto.ProductIds];
        ProductsCount = ProductIds.Length;
    }

    public void MarkSent(DateTime utcNow)
    {
        Status = AiBatchStatusEnum.Sent;
        SentAt = utcNow;
    }

    public void MarkCompleted(int durationMs, DateTime utcNow)
    {
        Status = AiBatchStatusEnum.Completed;
        DurationMs = durationMs;
        FinishedAt = utcNow;
    }

    public void MarkFailed(string errorMessage, int durationMs, DateTime utcNow)
    {
        Status = AiBatchStatusEnum.Failed;
        ErrorMessage = errorMessage.Length <= 500 ? errorMessage : errorMessage[..499] + "…";
        DurationMs = durationMs;
        FinishedAt = utcNow;
    }
}

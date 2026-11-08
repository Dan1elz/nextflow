using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Cabeçalho da análise (T-DF2) e linha do histórico (T-DF9). O período do horizonte vai do mês seguinte à data de referência até o
/// fim do horizonte (RN-DF17); enquanto a análise roda e o horizonte ainda é zero, <c>HorizonStart</c> e <c>HorizonEnd</c> vêm nulos.
/// </summary>
public class ForecastRunSummaryDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateOnly ReferenceDate { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string? RequestedByName { get; set; }
    public int HorizonMonths { get; set; }
    /// <summary>Primeiro mês do horizonte, "aaaa-mm".</summary>
    public string? HorizonStart { get; set; }
    /// <summary>Último mês do horizonte, "aaaa-mm".</summary>
    public string? HorizonEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AiModel { get; set; }
    public string? Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public int ProductsAnalyzed { get; set; }
    public int ProductsSentToAi { get; set; }
    public int? DurationMs { get; set; }
    public DateTime? FinishedAt { get; set; }
    public ForecastBatchProgressDto Batches { get; set; } = new();
    public ForecastRecommendationCountsDto Counts { get; set; } = new();
    public DateTime? NextScheduledAt { get; set; }

    public ForecastRunSummaryDto() { }

    public ForecastRunSummaryDto(ForecastRun entity, ForecastRecommendationCountsDto? counts, DateTime? nextScheduledAt)
    {
        Id = entity.Id;
        CreatedAt = entity.StartedAt;
        ReferenceDate = entity.ReferenceDate;
        Trigger = entity.Trigger.GetApiName();
        RequestedByName = entity.RequestedBy != null ? entity.RequestedBy.FullName : null;
        HorizonMonths = entity.HorizonMonths;
        if (entity.HorizonMonths > 0)
        {
            var first = new DateOnly(entity.ReferenceDate.Year, entity.ReferenceDate.Month, 1).AddMonths(1);
            HorizonStart = first.ToString("yyyy-MM");
            HorizonEnd = first.AddMonths(entity.HorizonMonths - 1).ToString("yyyy-MM");
        }
        Status = entity.Status.GetApiName();
        AiModel = entity.AiModel;
        Summary = entity.Summary;
        ErrorMessage = entity.ErrorMessage;
        ProductsAnalyzed = entity.ProductsAnalyzed;
        ProductsSentToAi = entity.ProductsSentToAi;
        DurationMs = entity.DurationMs;
        FinishedAt = entity.FinishedAt;
        Batches = new ForecastBatchProgressDto(entity.BatchesTotal, entity.BatchesCompleted, entity.BatchesFailed);
        Counts = counts ?? new ForecastRecommendationCountsDto();
        NextScheduledAt = nextScheduledAt;
    }
}

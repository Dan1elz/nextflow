namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Visão geral da precisão (T-DF10) nos últimos N meses completos.</summary>
public class ForecastAccuracySummaryDto
{
    public List<ForecastAccuracyMonthDto> Monthly { get; set; } = [];
    public List<ForecastAccuracyClassDto> ByAbcClass { get; set; } = [];
    public List<ForecastAccuracyCategoryDto> ByCategory { get; set; } = [];

    /// <summary>% de produtos medidos com erro médio dentro de ±15%; nulo quando nenhum produto foi medido.</summary>
    public decimal? WithinTolerancePercent { get; set; }
}

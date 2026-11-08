namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Erro médio dos produtos de uma classe ABC (T-DF10).</summary>
public class ForecastAccuracyClassDto
{
    public string AbcClass { get; set; } = string.Empty;
    public decimal MapePercent { get; set; }
}

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Erro médio dos produtos de uma categoria principal (T-DF10), no formato "Pai &gt; Filha".</summary>
public class ForecastAccuracyCategoryDto
{
    public string CategoryPath { get; set; } = string.Empty;
    public decimal MapePercent { get; set; }
}

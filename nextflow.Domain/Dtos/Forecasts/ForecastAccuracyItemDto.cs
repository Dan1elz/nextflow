namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Um mês completo do produto na precisão (T-DF5): previsto × vendido; erro nulo quando o mês ficou fora da medição.</summary>
public class ForecastAccuracyItemDto
{
    /// <summary>"aaaa-mm".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal Projected { get; set; }
    public decimal Actual { get; set; }
    public decimal? ErrorPercent { get; set; }
}

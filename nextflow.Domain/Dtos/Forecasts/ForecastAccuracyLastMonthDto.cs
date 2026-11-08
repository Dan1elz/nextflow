namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Último mês medido do produto na tabela de precisão (T-DF10).</summary>
public class ForecastAccuracyLastMonthDto
{
    /// <summary>"aaaa-mm".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal Projected { get; set; }
    public decimal Actual { get; set; }
}

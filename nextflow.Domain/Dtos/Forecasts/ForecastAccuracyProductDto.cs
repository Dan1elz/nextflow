namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Linha da tabela de precisão por produto (T-DF10). Só entram produtos com algum mês medido.</summary>
public class ForecastAccuracyProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? AbcClass { get; set; }
    public int MonthsMeasured { get; set; }
    public decimal MapePercent { get; set; }

    /// <summary>"Under" (subestima), "Over" (superestima) ou "Balanced".</summary>
    public string Bias { get; set; } = string.Empty;
    public ForecastAccuracyLastMonthDto? LastMonth { get; set; }
}

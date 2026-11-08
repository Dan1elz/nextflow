namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Projeção de um mês do horizonte (T-DF3), da análise mais recente que projetou o produto.</summary>
public class ProductForecastProjectionDto
{
    /// <summary>"aaaa-mm".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal ProjectedQuantity { get; set; }
}

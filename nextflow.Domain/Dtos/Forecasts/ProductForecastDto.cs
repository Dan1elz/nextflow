namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Previsão do produto (T-DF3 e aba "Previsão" do produto, T-DF5): histórico mensal, projeção da última análise, sazonalidade,
/// eventos e estoque atual. <c>Accuracy</c> fica vazio até a F3.6 (opcional).
/// </summary>
public class ProductForecastDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SaleUnit { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public List<ProductForecastHistoryDto> History { get; set; } = [];
    public List<ProductForecastProjectionDto> Projection { get; set; } = [];
    public List<ProductForecastSeasonalityDto> Seasonality { get; set; } = [];
    public int MonthsOfHistory { get; set; }
    public List<ProductForecastEventDto> Events { get; set; } = [];
    public decimal CurrentStock { get; set; }
    public List<object> Accuracy { get; set; } = [];
    public Guid? LatestRecommendationId { get; set; }
}

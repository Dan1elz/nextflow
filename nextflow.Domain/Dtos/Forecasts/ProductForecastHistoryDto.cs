namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Um mês do gráfico da T-DF3 (dados derivados da F3.1); mês sem venda vem zerado.</summary>
public class ProductForecastHistoryDto
{
    /// <summary>"aaaa-mm".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal QuantitySoldOnPromo { get; set; }
    public int DaysOutOfStock { get; set; }
    public decimal LostDemandQty { get; set; }
    public decimal? AvgPrice { get; set; }
    public decimal Revenue { get; set; }
}

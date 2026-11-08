namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Parte de uma loja na compra recomendada (RN-DF27), gravada pela análise.</summary>
public class ForecastStoreQuantityDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal SharePercent { get; set; }
    public decimal Shortfall { get; set; }
    public decimal Quantity { get; set; }
}

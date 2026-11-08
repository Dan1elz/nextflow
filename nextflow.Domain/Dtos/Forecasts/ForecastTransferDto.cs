namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Transferência sugerida pela recomendação "Transferir" (RN-DF16): de onde, para onde e quanto (somando as variações).</summary>
public class ForecastTransferDto
{
    public Guid FromLocationId { get; set; }
    public string FromLocationName { get; set; } = string.Empty;
    public Guid ToLocationId { get; set; }
    public string ToLocationName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

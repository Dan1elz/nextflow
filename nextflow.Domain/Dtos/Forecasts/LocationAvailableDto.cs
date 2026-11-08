namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Saldo de uma variação num local ativo (T-DF3, rateio por variação).</summary>
public class LocationAvailableDto
{
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public decimal Available { get; set; }
}

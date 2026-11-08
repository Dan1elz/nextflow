namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Rateio da compra por variação (RN-DF21, L-DF5): proporção histórica, quantidade e saldo por local.</summary>
public class VariantSplitDto
{
    public Guid VariantId { get; set; }
    public string? VariantLabel { get; set; }
    public decimal SharePercent { get; set; }
    public decimal SuggestedQuantity { get; set; }
    public List<LocationAvailableDto> AvailableByLocation { get; set; } = [];
}

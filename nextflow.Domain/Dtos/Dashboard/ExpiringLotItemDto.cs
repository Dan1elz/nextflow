namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Item do bloco "Lotes vencendo" (T-DF1).</summary>
public class ExpiringLotItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public string? VariantLabel { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public int DaysToExpire { get; set; }
    public decimal Quantity { get; set; }
}

namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Item do bloco "Estoque baixo" (T-DF1).</summary>
public class LowStockItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid VariantId { get; set; }
    public string? VariantLabel { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public decimal Available { get; set; }
    public decimal MinQuantity { get; set; }
}

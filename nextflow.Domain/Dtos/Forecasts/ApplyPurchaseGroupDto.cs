namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Um fornecedor do dialog Comprar (T-DF4): local de entrega e as linhas por variação.</summary>
public class ApplyPurchaseGroupDto
{
    public Guid SupplierId { get; set; }
    public Guid ReceiveLocationId { get; set; }
    public string? Note { get; set; }
    public List<ApplyPurchaseItemDto> Items { get; set; } = [];
}

namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Nova transferência (RN-CE62): origem, destino e as variações com a quantidade.</summary>
public class StockTransferRequestDto
{
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public string? Note { get; set; }
    public List<StockTransferItemRequestDto> Items { get; set; } = [];
}

public class StockTransferItemRequestDto
{
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
}

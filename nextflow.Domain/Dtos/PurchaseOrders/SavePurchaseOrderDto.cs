namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Cabeçalho e itens do pedido de compra (T-CP2). <see cref="Version"/> é obrigatória no update.</summary>
public class SavePurchaseOrderDto
{
    public Guid SupplierId { get; set; }
    public Guid ReceiveLocationId { get; set; }
    public string? Note { get; set; }
    public uint? Version { get; set; }
    /// <summary>Reenvio depois do aviso de mínimo, múltiplo ou fora de linha (L-CP2, RN-CE19).</summary>
    public bool ConfirmWarnings { get; set; }
    public List<SavePurchaseItemDto> Items { get; set; } = [];
}

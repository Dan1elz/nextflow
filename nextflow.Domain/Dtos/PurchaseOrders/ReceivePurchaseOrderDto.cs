namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Recebimento (T-CP4): versão lida e cada item do pedido uma vez (sem recebimento parcial, RN-CP04).</summary>
public class ReceivePurchaseOrderDto
{
    public uint? Version { get; set; }
    public List<ReceivePurchaseItemDto> Items { get; set; } = [];
}

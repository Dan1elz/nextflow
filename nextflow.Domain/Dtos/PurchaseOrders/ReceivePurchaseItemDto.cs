namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Item recebido (T-CP4). <see cref="Lots"/> é recusado até a F1.7 (lotes).</summary>
public class ReceivePurchaseItemDto
{
    public Guid ItemId { get; set; }
    public List<ReceivePurchaseLotDto>? Lots { get; set; }
}

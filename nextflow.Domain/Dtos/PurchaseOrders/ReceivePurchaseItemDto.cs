namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Item recebido (T-CP4). <see cref="Lots"/>: obrigatório se o produto controla lote, somando a quantidade do item (F1.7).</summary>
public class ReceivePurchaseItemDto
{
    public Guid ItemId { get; set; }
    public List<ReceivePurchaseLotDto>? Lots { get; set; }
}

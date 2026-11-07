namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Linha do pedido, sempre na unidade de venda (RN-CE30). Com <see cref="Id"/>, atualiza a linha existente.</summary>
public class SavePurchaseItemDto
{
    public Guid? Id { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

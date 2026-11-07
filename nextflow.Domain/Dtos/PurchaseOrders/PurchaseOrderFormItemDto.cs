using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Linha do formulário (T-CP2): o que foi salvo, a sugestão da variação e o subtotal calculado na API.</summary>
public class PurchaseOrderFormItemDto : PurchaseItemSuggestionDto
{
    public Guid Id { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Subtotal { get; set; }

    public PurchaseOrderFormItemDto() { }

    /// <param name="item">Com variação, produto, vínculos e grade carregados.</param>
    public PurchaseOrderFormItemDto(PurchaseItem item, Guid supplierId, decimal availableAtStore, decimal incomingQuantity)
        : base(item.Variant, supplierId, availableAtStore, incomingQuantity)
    {
        Id = item.Id;
        Quantity = item.Quantity;
        UnitCost = item.UnitCost;
        Subtotal = item.Subtotal;
    }
}

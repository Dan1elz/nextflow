using Nextflow.Domain.Dtos.PurchaseOrders;

namespace Nextflow.Domain.Models;

/// <summary>Linha do pedido de compra: uma por variação, na unidade de venda (RN-CE30, RN-CE34). Subtotal em centavos.</summary>
public class PurchaseItem : BaseModel
{
    public Guid PurchaseOrderId { get; private set; }
    public Guid VariantId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Subtotal { get; private set; }
    public short SortOrder { get; private set; }

    public virtual PurchaseOrder PurchaseOrder { get; private set; } = null!;
    public virtual ProductVariant Variant { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "item do pedido de compra";
    public override string Plural => "itens do pedido de compra";

    private PurchaseItem() { }

    public PurchaseItem(SavePurchaseItemDto dto, Guid purchaseOrderId, short sortOrder)
    {
        PurchaseOrderId = purchaseOrderId;
        Update(dto, sortOrder);
    }

    public void Update(SavePurchaseItemDto dto, short sortOrder)
    {
        VariantId = dto.VariantId;
        Quantity = dto.Quantity;
        UnitCost = dto.UnitCost;
        Subtotal = Math.Round(dto.Quantity * dto.UnitCost, 2, MidpointRounding.AwayFromZero);
        SortOrder = sortOrder;
    }
}

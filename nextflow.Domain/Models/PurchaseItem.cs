using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("purchase_items")]
public class PurchaseItem : BaseModel
{
    [ForeignKey("purchase_orders"), Required(ErrorMessage = "O Pedido de Compra é obrigatório.")]
    public Guid PurchaseOrderId { get; private set; }
    public virtual PurchaseOrder? PurchaseOrder { get; private set; }

    [ForeignKey("products"), Required(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; private set; }

    [Required(ErrorMessage = "A quantidade é obrigatória.")]
    public double Quantity { get; private set; }

    [Required(ErrorMessage = "O desconto é obrigatório.")]
    public decimal Discount { get; private set; }

    [Required(ErrorMessage = "O preço de custo é obrigatório.")]
    public decimal CostPrice { get; private set; }

    public override string Preposition => "o";
    public override string Singular => "item do pedido de compra";
    public override string Plural => "itens do pedido de compra";

    private PurchaseItem() : base() { }

    public PurchaseItem(Guid purchaseOrderId, Guid productId, double quantity, decimal discount, decimal costPrice) : base()
    {
        PurchaseOrderId = purchaseOrderId;
        ProductId = productId;
        Quantity = quantity;
        Discount = discount;
        CostPrice = costPrice;
    }
}

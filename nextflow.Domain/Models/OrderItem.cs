using Nextflow.Domain.Dtos.Orders;

namespace Nextflow.Domain.Models;

/// <summary>
/// Item do pedido, sempre de uma variação (RN-CE34). Grava o preço de tabela e o cobrado do dia (RN-VD02/RN-VD19), a
/// promoção aplicada e o custo médio do momento, base da margem. Total da linha = bruto − desconto em R$ (RN-VD03).
/// </summary>
public class OrderItem : BaseModel
{
    public Guid OrderId { get; private set; }
    /// <summary>Ordem da linha no pedido, a partir de 0.</summary>
    public int Position { get; private set; }
    public Guid VariantId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal ListPrice { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Discount { get; private set; }
    public decimal LineTotal { get; private set; }
    public Guid? PromotionId { get; private set; }
    public decimal? UnitCost { get; private set; }

    public virtual Order Order { get; private set; } = null!;
    public virtual ProductVariant Variant { get; private set; } = null!;
    public virtual Promotion? Promotion { get; private set; }

    public override string Preposition => "o";
    public override string Singular => "item do pedido";
    public override string Plural => "itens do pedido";

    internal void PlaceAt(int position) => Position = position;

    public decimal GrossTotal => GrossOf(UnitPrice, Quantity);

    private OrderItem() { }

    public OrderItem(QuickSaleItemDto dto, ProductVariant variant, decimal unitPrice, Promotion? promotion)
    {
        VariantId = variant.Id;
        Quantity = dto.Quantity;
        ListPrice = variant.Price;
        UnitPrice = unitPrice;
        Discount = dto.Discount;
        PromotionId = promotion?.Id;
        UnitCost = variant.AverageCost;
        LineTotal = GrossTotal - Discount;
    }

    /// <summary>Preço × quantidade em duas casas, meio para cima.</summary>
    public static decimal GrossOf(decimal unitPrice, decimal quantity)
        => Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
}
